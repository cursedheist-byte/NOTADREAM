#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NyxoPerf
{
    [Serializable]
    public class ProbeRequest
    {
        public string mode;
        public float warmup = 4f;
        public float duration = 10f;
        public bool disableShadows;
        public bool disablePointLights;
        public bool realShadows;
        public float vegetationCutoff;
        public float vegetationShadowCutoff;
        public bool screenshot;
        public bool enableProfiler;
        public bool disableVegetation;
        public bool disableDirectionalShadows;
        public bool disableVolume;
        public float transitionTimeout = 240f;
        public bool transitionFast;
        public bool noVegetationCuller;
        public float panSeconds = 8f;
    }

    public static class FrameProbeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(root, "Temp", "frame_probe_request.json");
            if (!File.Exists(path))
            {
                return;
            }

            ProbeRequest request;
            try
            {
                request = JsonUtility.FromJson<ProbeRequest>(File.ReadAllText(path));
                File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("FrameProbe: could not read request: " + e.Message);
                return;
            }

            if (request == null || string.IsNullOrEmpty(request.mode))
            {
                return;
            }

            var host = new GameObject("~FrameProbe");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<FrameProbeRunner>().Begin(request);
        }
    }

    public sealed class FrameProbeRunner : MonoBehaviour
    {
        private const string WorldMaster = "Assets/Scenes/World_Master.unity";
        private const string Scene2 = "Assets/Scenes/Scene2_Evening.unity";
        private const string Scene1 = "Assets/Scenes/Scene1_Opening.unity";
        private const string Repeat = "Assets/Scenes/Scene1_Opening_Repeat.unity";

        private ProbeRequest request;
        private readonly List<Counter> counters = new List<Counter>();
        private UnityEngine.ShadowQuality originalShadows;
        private readonly List<Light> disabledPointLights = new List<Light>();
        private readonly List<Renderer> shadowRestored = new List<Renderer>();
        private readonly List<ShadowCastingMode> shadowRestoredModes = new List<ShadowCastingMode>();
        private readonly List<Light> shadowRestoredLights = new List<Light>();
        private readonly List<LightShadows> shadowRestoredLightModes = new List<LightShadows>();
        private readonly List<Renderer> vegetationDisabled = new List<Renderer>();
        private readonly List<Volume> mutedVolumes = new List<Volume>();
        private readonly List<float> mutedVolumeWeights = new List<float>();
        private Transform vegetationRoot;
        private bool vegetationRootDeactivated;
        private int censusRenderers;
        private double[] bandDistances = new double[0];
        private int[] bandTriangles = new int[0];
        private bool[] bandCasters = new bool[0];

        public void Begin(ProbeRequest probeRequest)
        {
            request = probeRequest;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Application.runInBackground = true;
            yield return null;

            if (request.mode == "transition")
            {
                yield return RunTransition();
                yield break;
            }

            if (request.mode == "pan")
            {
                yield return RunPan();
                yield break;
            }

            var report = new StringBuilder();
            var startedUtc = DateTime.UtcNow;
            float loadStartedAt = Time.realtimeSinceStartup;

            Scene startScene = SceneManager.GetActiveScene();
            if (request.mode == "scene1")
            {
                yield return Load(Scene1, LoadSceneMode.Single);
            }
            else if (request.mode == "world")
            {
                yield return Load(WorldMaster, LoadSceneMode.Additive);
                SceneManager.SetActiveScene(SceneManager.GetSceneByPath(WorldMaster));
                yield return Unload(startScene);
            }
            else if (request.mode == "scene2")
            {
                yield return Load(WorldMaster, LoadSceneMode.Additive);
                yield return Load(Scene2, LoadSceneMode.Additive);
                SceneManager.SetActiveScene(SceneManager.GetSceneByPath(Scene2));
                yield return Unload(startScene);
            }
            else if (request.mode == "repeat")
            {
                yield return Load(WorldMaster, LoadSceneMode.Additive);
                yield return Load(Scene2, LoadSceneMode.Additive);
                SceneManager.SetActiveScene(SceneManager.GetSceneByPath(Scene2));
                yield return Unload(startScene);
                yield return new WaitForSecondsRealtime(3f);
                yield return Load(Repeat, LoadSceneMode.Additive);
            }
            else
            {
                Debug.LogError("FrameProbe: unknown mode " + request.mode);
                yield break;
            }

            float loadSeconds = Time.realtimeSinceStartup - loadStartedAt;

            yield return new WaitForSecondsRealtime(request.warmup);
            yield return null;

            // Engine markers (Culling, RenderLoop.Draw, ...) only emit values while the
            // Profiler is enabled. Counters only: no call stacks, no binary log.
            bool profilerWasEnabled = Profiler.enabled;
            if (request.enableProfiler)
            {
                Profiler.enabled = true;
            }

            if (request.disableShadows)
            {
                originalShadows = QualitySettings.shadows;
                QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
            }

            if (request.realShadows)
            {
                var allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < allRenderers.Length; i++)
                {
                    var renderer = allRenderers[i];
                    if (renderer == null)
                    {
                        continue;
                    }

                    shadowRestored.Add(renderer);
                    shadowRestoredModes.Add(renderer.shadowCastingMode);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                var allLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < allLights.Length; i++)
                {
                    var light = allLights[i];
                    if (light == null)
                    {
                        continue;
                    }

                    shadowRestoredLights.Add(light);
                    shadowRestoredLightModes.Add(light.shadows);
                    light.shadows = LightShadows.None;
                }
            }

            ApplyVegetationDiagnostics();

            if (request.disableDirectionalShadows)
            {
                var allLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < allLights.Length; i++)
                {
                    var light = allLights[i];
                    if (light != null && light.type == LightType.Directional)
                    {
                        shadowRestoredLights.Add(light);
                        shadowRestoredLightModes.Add(light.shadows);
                        light.shadows = LightShadows.None;
                    }
                }
            }

            if (request.disableVegetation && vegetationRoot != null)
            {
                vegetationRootDeactivated = true;
                vegetationRoot.gameObject.SetActive(false);
            }

            if (request.disableVolume)
            {
                var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < volumes.Length; i++)
                {
                    var volume = volumes[i];
                    if (volume == null)
                    {
                        continue;
                    }

                    mutedVolumes.Add(volume);
                    mutedVolumeWeights.Add(volume.weight);
                    volume.weight = 0f;
                }
            }

            if (request.disablePointLights)
            {
                var allLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < allLights.Length; i++)
                {
                    var light = allLights[i];
                    if (light != null && light.enabled &&
                        (light.type == LightType.Point || light.type == LightType.Spot))
                    {
                        disabledPointLights.Add(light);
                        light.enabled = false;
                    }
                }
            }

            foreach (var counter in counters)
            {
                counter.Dispose();
            }

            counters.Clear();
            counters.Add(new Counter(ProfilerCategory.Internal, "CPU Main Thread Frame Time"));
            counters.Add(new Counter(ProfilerCategory.Internal, "CPU Render Thread Frame Time"));
            counters.Add(new Counter(ProfilerCategory.Internal, "CPU Total Frame Time"));
            counters.Add(new Counter(ProfilerCategory.Internal, "GPU Frame Time"));
            counters.Add(new Counter(ProfilerCategory.Render, "Batches Count"));
            counters.Add(new Counter(ProfilerCategory.Render, "SetPass Calls Count"));
            counters.Add(new Counter(ProfilerCategory.Render, "Triangles Count"));
            counters.Add(new Counter(ProfilerCategory.Render, "Shadow Casters Count"));
            counters.Add(new Counter(ProfilerCategory.Memory, "GC Allocated In Frame"));
            counters.Add(new Counter(ProfilerCategory.Internal, "PlayerLoop"));
            counters.Add(new Counter(ProfilerCategory.Scripts, "BehaviourUpdate"));
            counters.Add(new Counter(ProfilerCategory.Render, "Camera.Render"));
            counters.Add(new Counter(ProfilerCategory.Render, "Culling"));
            counters.Add(new Counter(ProfilerCategory.Render, "RenderLoop.Draw"));
            counters.Add(new Counter(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread"));
            counters.Add(new Counter(ProfilerCategory.Physics, "Physics.Processing"));
            counters.Add(new Counter(ProfilerCategory.Render, "RenderShadowAtlas"));
            counters.Add(new Counter(ProfilerCategory.Animation, "Animation.Update"));
            counters.Add(new Counter(ProfilerCategory.Animation, "Animators.Update"));
            counters.Add(new Counter(ProfilerCategory.Gui, "Canvas.SendWillRenderCanvases"));
            counters.Add(new Counter(ProfilerCategory.Audio, "Audio.Update"));
            counters.Add(new Counter(ProfilerCategory.Internal, "FixedUpdate.PhysicsFixedUpdate"));
            counters.Add(new Counter(ProfilerCategory.Internal, "PostLateUpdate.PlayerRenderLoop"));
            counters.Add(new Counter(ProfilerCategory.Internal, "PostLateUpdate.FinishFrameRendering"));
            counters.Add(new Counter(ProfilerCategory.Internal, "PreLateUpdate.DirectorUpdate"));
            counters.Add(new Counter(ProfilerCategory.Internal, "Camera.Render"));
            counters.Add(new Counter(ProfilerCategory.Internal, "Culling"));
            counters.Add(new Counter(ProfilerCategory.Internal, "RenderLoop.Draw"));

            yield return null;
            yield return null;

            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int activeRenderers = 0;
            int visibleRenderers = 0;
            int shadowCasters = 0;
            int skinnedRenderers = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    activeRenderers++;
                }

                if (renderer.isVisible)
                {
                    visibleRenderers++;
                }

                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    shadowCasters++;
                }

                if (renderer is SkinnedMeshRenderer)
                {
                    skinnedRenderers++;
                }
            }

            var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int shadowLights = 0;
            int directional = 0;
            int point = 0;
            int spot = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null || !light.enabled)
                {
                    continue;
                }

                if (light.shadows != LightShadows.None)
                {
                    shadowLights++;
                }

                if (light.type == LightType.Directional)
                {
                    directional++;
                }
                else if (light.type == LightType.Point)
                {
                    point++;
                }
                else if (light.type == LightType.Spot)
                {
                    spot++;
                }
            }

            Camera[] cameras = Camera.allCameras;
            int audioListeners = 0;
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && listeners[i].enabled && listeners[i].gameObject.activeInHierarchy)
                {
                    audioListeners++;
                }
            }

            var frameTimes = new List<double>(1024);
            double previous = Time.realtimeSinceStartupAsDouble;
            double deadline = previous + request.duration;
            double firstSample = previous;
            int frameCount = 0;

            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                double ms = (now - previous) * 1000.0;
                previous = now;
                if (ms <= 0.0 || ms > 5000.0)
                {
                    continue;
                }

                frameTimes.Add(ms);
                frameCount++;
                for (int i = 0; i < counters.Count; i++)
                {
                    counters[i].Sample();
                }
            }

            double sampledSeconds = Time.realtimeSinceStartupAsDouble - firstSample;

            FrameTimingManager.CaptureFrameTimings();
            var timing = new FrameTiming[1];
            uint timingCount = FrameTimingManager.GetLatestTimings(1, timing);

            var frameArray = frameTimes.ToArray();
            Array.Sort(frameArray);

            double sum = 0;
            for (int i = 0; i < frameTimes.Count; i++)
            {
                sum += frameTimes[i];
            }

            double avgMs = frameTimes.Count > 0 ? sum / frameTimes.Count : 0;
            double variance = 0;
            for (int i = 0; i < frameTimes.Count; i++)
            {
                double d = frameTimes[i] - avgMs;
                variance += d * d;
            }

            double stdMs = frameTimes.Count > 0 ? Math.Sqrt(variance / frameTimes.Count) : 0;
            int over33 = 0;
            int over50 = 0;
            for (int i = 0; i < frameTimes.Count; i++)
            {
                if (frameTimes[i] > 33.34)
                {
                    over33++;
                }

                if (frameTimes[i] > 50.0)
                {
                    over50++;
                }
            }

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            string device = SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType;
            string scenes = string.Empty;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (i > 0)
                {
                    scenes += "|";
                }

                scenes += SceneManager.GetSceneAt(i).name;
            }

            int quality = QualitySettings.GetQualityLevel();
            string qualityName = quality >= 0 && quality < QualitySettings.names.Length
                ? QualitySettings.names[quality]
                : "?";

            report.Append("{");
            Text(report, "mode", request.mode);
            Text(report, "utc", startedUtc.ToString("o", CultureInfo.InvariantCulture));
            Number(report, "loadSeconds", loadSeconds);
            Number(report, "sampledSeconds", sampledSeconds);
            Number(report, "warmupSeconds", request.warmup);
            Number(report, "requestedDuration", request.duration);
            Text(report, "scenes", scenes);
            Text(report, "unity", Application.unityVersion);
            Text(report, "device", device);
            Text(report, "os", SystemInfo.operatingSystem);
            Number(report, "screenWidth", Screen.width);
            Number(report, "screenHeight", Screen.height);
            Number(report, "refreshHz", Screen.currentResolution.refreshRateRatio.value);
            Number(report, "qualityLevel", quality);
            Text(report, "qualityName", qualityName);
            Number(report, "vSync", QualitySettings.vSyncCount);
            Number(report, "targetFrameRate", Application.targetFrameRate);
            Number(report, "qualityShadowDistance", QualitySettings.shadowDistance);
            Number(report, "qualityAntiAliasing", QualitySettings.antiAliasing);
            Number(report, "qualityLodBias", QualitySettings.lodBias);
            Number(report, "urpMsaa", urp != null ? urp.msaaSampleCount : -1);
            Number(report, "urpRenderScale", urp != null ? urp.renderScale : -1f);
            Number(report, "urpShadowDistance", urp != null ? urp.shadowDistance : -1f);
            Number(report, "timeScale", Time.timeScale);
            Number(report, "probeDisableShadows", request.disableShadows ? 1 : 0);
            Number(report, "probeDisablePointLights", request.disablePointLights ? 1 : 0);
            Number(report, "probeRealShadows", request.realShadows ? 1 : 0);
            Number(report, "probeVegetationCutoff", request.vegetationCutoff);
            Number(report, "probeVegetationShadowCutoff", request.vegetationShadowCutoff);
            Number(report, "probeEnableProfiler", request.enableProfiler ? 1 : 0);
            Number(report, "probeDisableVegetation", request.disableVegetation ? 1 : 0);
            Number(report, "probeDisableDirectionalShadows", request.disableDirectionalShadows ? 1 : 0);
            Number(report, "probeDisableVolume", request.disableVolume ? 1 : 0);
            Number(report, "vegetationCensusRenderers", censusRenderers);
            Number(report, "frames", frameTimes.Count);
            Number(report, "frameMsAvg", avgMs);
            Number(report, "frameMsMedian", Percentile(frameArray, 0.50));
            Number(report, "frameMsP95", Percentile(frameArray, 0.95));
            Number(report, "frameMsP99", Percentile(frameArray, 0.99));
            Number(report, "frameMsMin", frameArray.Length > 0 ? frameArray[0] : 0);
            Number(report, "frameMsMax", frameArray.Length > 0 ? frameArray[frameArray.Length - 1] : 0);
            Number(report, "frameMsStd", stdMs);
            Number(report, "fpsAvg", avgMs > 0 ? 1000.0 / avgMs : 0);
            Number(report, "fpsMedian", 1000.0 / Math.Max(0.0001, Percentile(frameArray, 0.50)));
            Number(report, "fpsLow1", 1000.0 / Math.Max(0.0001, Percentile(frameArray, 0.99)));
            Number(report, "framesOver33ms", over33);
            Number(report, "framesOver50ms", over50);
            Number(report, "renderersActive", activeRenderers);
            Number(report, "renderersVisible", visibleRenderers);
            Number(report, "renderersShadowCasting", shadowCasters);
            Number(report, "renderersSkinned", skinnedRenderers);
            Number(report, "lightsEnabled", lights.Length);
            Number(report, "lightsShadowCasting", shadowLights);
            Number(report, "lightsDirectional", directional);
            Number(report, "lightsPoint", point);
            Number(report, "lightsSpot", spot);
            Number(report, "camerasEnabled", cameras.Length);
            Number(report, "audioListeners", audioListeners);
            Number(report, "allocatedMB", UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576.0);
            Number(report, "reservedMB", UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576.0);
            Number(report, "frameTimingEnabled", FrameTimingManager.IsFeatureEnabled() ? 1 : 0);
            ArrayNumber(report, "ftCpuMs", timingCount > 0 ? timing[0].cpuFrameTime : 0);
            ArrayNumber(report, "ftGpuMs", timingCount > 0 ? timing[0].gpuFrameTime : 0);
            ArrayNumber(report, "ftMainMs", timingCount > 0 ? timing[0].cpuMainThreadFrameTime : 0);
            ArrayNumber(report, "ftRenderMs", timingCount > 0 ? timing[0].cpuRenderThreadFrameTime : 0);
            ArrayNumber(report, "ftPresentWaitMs", timingCount > 0 ? timing[0].cpuMainThreadPresentWaitTime : 0);

            AppendBreakdown(report, renderers);
            AppendVegetationBands(report);

            report.Append("\"counters\":[");
            for (int i = 0; i < counters.Count; i++)
            {
                if (i > 0)
                {
                    report.Append(",");
                }

                var counter = counters[i];
                var values = counter.ValuesSorted();
                double cAvg = counter.Average();
                report.Append("{");
                Text(report, "name", counter.StatName);
                Text(report, "category", counter.Category.ToString());
                Text(report, "unit", counter.Recorder.Valid ? counter.Recorder.UnitType.ToString() : "invalid");
                Number(report, "valid", counter.Recorder.Valid ? 1 : 0);
                Number(report, "avg", cAvg);
                Number(report, "p95", Percentile(values, 0.95));
                Number(report, "max", values.Length > 0 ? values[values.Length - 1] : 0);
                Number(report, "samples", values.Length);
                report.Append("}");
            }

            report.Append("],");
            report.Append("\"frameTimesMs\":[");
            for (int i = 0; i < frameTimes.Count; i++)
            {
                if (i > 0)
                {
                    report.Append(",");
                }

                report.Append(frameTimes[i].ToString("0.###", CultureInfo.InvariantCulture));
            }

            report.Append("]}");

            if (request.enableProfiler)
            {
                Profiler.enabled = profilerWasEnabled;
            }

            var countersDisposed = false;
            foreach (var counter in counters)
            {
                counter.Dispose();
            }

            countersDisposed = true;
            counters.Clear();

            string screenshotPath = null;
            if (request.screenshot)
            {
                string baseDir = Directory.GetParent(Application.dataPath).FullName;
                string tag = "base";
                if (request.vegetationCutoff > 0f)
                {
                    tag = "vegcut" + request.vegetationCutoff.ToString("0", CultureInfo.InvariantCulture);
                }
                else if (request.realShadows)
                {
                    tag = "noshadow";
                }

                screenshotPath = Path.Combine(baseDir, "Temp",
                    "frame_probe_" + request.mode + "_" + tag + "_" + DateTime.Now.ToString("HHmmss") + ".png");
                ScreenCapture.CaptureScreenshot(screenshotPath);
                yield return null;
                yield return null;
            }

            for (int i = 0; i < vegetationDisabled.Count; i++)
            {
                if (vegetationDisabled[i] != null)
                {
                    vegetationDisabled[i].enabled = true;
                }
            }

            vegetationDisabled.Clear();

            for (int i = 0; i < shadowRestored.Count; i++)
            {
                if (shadowRestored[i] != null)
                {
                    shadowRestored[i].shadowCastingMode = shadowRestoredModes[i];
                }
            }

            shadowRestored.Clear();
            shadowRestoredModes.Clear();

            for (int i = 0; i < shadowRestoredLights.Count; i++)
            {
                if (shadowRestoredLights[i] != null)
                {
                    shadowRestoredLights[i].shadows = shadowRestoredLightModes[i];
                }
            }

            shadowRestoredLights.Clear();
            shadowRestoredLightModes.Clear();

            if (vegetationRootDeactivated && vegetationRoot != null)
            {
                vegetationRoot.gameObject.SetActive(true);
                vegetationRootDeactivated = false;
            }

            for (int i = 0; i < mutedVolumes.Count; i++)
            {
                if (mutedVolumes[i] != null)
                {
                    mutedVolumes[i].weight = mutedVolumeWeights[i];
                }
            }

            mutedVolumes.Clear();
            mutedVolumeWeights.Clear();

            if (request.disableShadows)
            {
                QualitySettings.shadows = originalShadows;
            }

            for (int i = 0; i < disabledPointLights.Count; i++)
            {
                if (disabledPointLights[i] != null)
                {
                    disabledPointLights[i].enabled = true;
                }
            }

            disabledPointLights.Clear();

            string root = Directory.GetParent(Application.dataPath).FullName;
            string output = Path.Combine(root, "Temp",
                "frame_probe_" + request.mode + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            try
            {
                string json = report.ToString().Replace(",}", "}").Replace(",]", "]");
                File.WriteAllText(output, json);
                Debug.Log("FrameProbe: wrote " + output + " frames=" + frameTimes.Count + " avgMs=" +
                          avgMs.ToString("0.00", CultureInfo.InvariantCulture) + " countersDisposed=" +
                          countersDisposed);
            }
            catch (Exception e)
            {
                Debug.LogError("FrameProbe: write failed: " + e.Message);
            }

            yield return new WaitForSecondsRealtime(0.5f);
            EditorApplication.isPlaying = false;
        }

        // Plays the real opening and records every frame across the Scene 1 -> Scene 2 handoff,
        // so load/activation/unload stalls can be told apart from steady-state cost.
        private IEnumerator RunTransition()
        {
            float timeout = request.transitionTimeout > 0f ? request.transitionTimeout : 240f;
            float startedAt = Time.realtimeSinceStartup;
            yield return Load(Scene1, LoadSceneMode.Single);

            if (request.transitionFast)
            {
                yield return SkipOpeningIntoHandoff();
            }

            var times = new List<float>();
            var phases = new List<int>();
            var offsets = new List<float>();
            float previous = Time.realtimeSinceStartup;
            float scene2ActiveSince = -1f;
            int currentPhase = -1;
            float phaseStart = previous;
            var phaseDurations = new List<float>();
            bool coverSeen = false;
            float nextCoverCheck = previous;

            while (true)
            {
                yield return null;
                float now = Time.realtimeSinceStartup;
                if (!coverSeen && now >= nextCoverCheck)
                {
                    nextCoverCheck = now + 1f;
                    coverSeen = GameObject.Find("SceneHandoffCover") != null;
                }

                times.Add((now - previous) * 1000f);
                offsets.Add(now - startedAt);
                previous = now;

                var scene2 = SceneManager.GetSceneByPath(Scene2);
                int phase = !scene2.IsValid() || !scene2.isLoaded
                    ? 0
                    : SceneManager.GetActiveScene() == scene2 ? 2 : 1;
                if (phase != currentPhase)
                {
                    if (currentPhase >= 0)
                    {
                        phaseDurations.Add(now - phaseStart);
                    }

                    currentPhase = phase;
                    phaseStart = now;
                }

                phases.Add(phase);
                if (phase == 2 && scene2ActiveSince < 0f)
                {
                    scene2ActiveSince = now;
                }

                if (scene2ActiveSince > 0f && now - scene2ActiveSince >= request.duration)
                {
                    break;
                }

                if (now - startedAt > timeout)
                {
                    Debug.LogWarning("FrameProbe: transition timeout after " + timeout + "s, phase=" + currentPhase);
                    break;
                }
            }

            phaseDurations.Add(Time.realtimeSinceStartup - phaseStart);

            var report = new StringBuilder();
            report.Append("{\n");
            report.Append("\"mode\":\"transition\",\n");
            report.Append("\"utc\":\"").Append(DateTime.UtcNow.ToString("o")).Append("\",\n");
            report.Append("\"totalSeconds\":")
                .Append((Time.realtimeSinceStartup - startedAt).ToString("0.00", CultureInfo.InvariantCulture)).Append(",\n");
            report.Append("\"phaseDurations\":[");
            for (int i = 0; i < phaseDurations.Count; i++)
            {
                if (i > 0)
                {
                    report.Append(',');
                }

                report.Append(phaseDurations[i].ToString("0.00", CultureInfo.InvariantCulture));
            }

            report.Append("],\n\"phases\":[");
            for (int phase = 0; phase <= 2; phase++)
            {
                var phaseTimes = new List<float>();
                for (int i = 0; i < times.Count; i++)
                {
                    if (phases[i] == phase)
                    {
                        phaseTimes.Add(times[i]);
                    }
                }

                if (phase > 0)
                {
                    report.Append(',');
                }

                report.Append("{");
                report.Append("\"phase\":").Append(phase).Append(',');
                report.Append("\"label\":\"").Append(phase == 0 ? "opening" : phase == 1 ? "scene2-loading" : "scene2-active").Append("\",");
                if (phaseTimes.Count == 0)
                {
                    report.Append("\"frames\":0,\"avgMs\":0,\"p95Ms\":0,\"maxMs\":0,\"over33\":0,\"over50\":0,\"over100\":0,\"over250\":0}");
                    continue;
                }

                phaseTimes.Sort();
                double sum = 0;
                int over33 = 0, over50 = 0, over100 = 0, over250 = 0;
                for (int i = 0; i < phaseTimes.Count; i++)
                {
                    sum += phaseTimes[i];
                    if (phaseTimes[i] > 33.3f) over33++;
                    if (phaseTimes[i] > 50f) over50++;
                    if (phaseTimes[i] > 100f) over100++;
                    if (phaseTimes[i] > 250f) over250++;
                }

                report.Append("\"frames\":").Append(phaseTimes.Count).Append(',');
                report.Append("\"avgMs\":").Append((sum / phaseTimes.Count).ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
                report.Append("\"p95Ms\":").Append(phaseTimes[(int)(phaseTimes.Count * 0.95f)].ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
                report.Append("\"maxMs\":").Append(phaseTimes[phaseTimes.Count - 1].ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
                report.Append("\"over33\":").Append(over33).Append(',');
                report.Append("\"over50\":").Append(over50).Append(',');
                report.Append("\"over100\":").Append(over100).Append(',');
                report.Append("\"over250\":").Append(over250).Append('}');
            }

            report.Append("],\n\"perSecond\":[");
            if (scene2ActiveSince > 0f)
            {
                int bucket = 0;
                bool first = true;
                while (scene2ActiveSince + bucket < Time.realtimeSinceStartup)
                {
                    float from = scene2ActiveSince + bucket;
                    float to = from + 1f;
                    double sum = 0;
                    int frames = 0;
                    float max = 0f;
                    int over33 = 0;
                    for (int i = 0; i < times.Count; i++)
                    {
                        float absolute = startedAt + offsets[i];
                        if (absolute < from || absolute >= to)
                        {
                            continue;
                        }

                        sum += times[i];
                        frames++;
                        if (times[i] > max) max = times[i];
                        if (times[i] > 33.3f) over33++;
                    }

                    if (frames > 0)
                    {
                        if (!first) report.Append(',');
                        first = false;
                        report.Append("{\"second\":").Append(bucket + 1);
                        report.Append(",\"frames\":").Append(frames);
                        report.Append(",\"avgMs\":").Append((sum / frames).ToString("0.00", CultureInfo.InvariantCulture));
                        report.Append(",\"maxMs\":").Append(max.ToString("0.00", CultureInfo.InvariantCulture));
                        report.Append(",\"over33\":").Append(over33).Append('}');
                    }

                    bucket++;
                }
            }

            report.Append("],\n\"handoffCoverSeen\":").Append(coverSeen ? "true" : "false");
            report.Append(",\n\"handoffCoverLeftover\":").Append(GameObject.Find("SceneHandoffCover") != null ? "true" : "false");

            var order = new List<int>();
            for (int i = 0; i < times.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) => times[b].CompareTo(times[a]));
            report.Append(",\n\"slowest\":[");
            int count = Mathf.Min(20, order.Count);
            for (int i = 0; i < count; i++)
            {
                int index = order[i];
                if (i > 0)
                {
                    report.Append(',');
                }

                report.Append("{\"at\":").Append(offsets[index].ToString("0.0", CultureInfo.InvariantCulture));
                report.Append(",\"phase\":").Append(phases[index]);
                report.Append(",\"ms\":").Append(times[index].ToString("0.00", CultureInfo.InvariantCulture)).Append('}');
            }

            report.Append("]\n}\n");
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp",
                "frame_probe_transition_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            try
            {
                File.WriteAllText(output, report.ToString());
                Debug.Log("FrameProbe: wrote " + output + " frames=" + times.Count);
            }
            catch (Exception e)
            {
                Debug.LogError("FrameProbe: write failed: " + e.Message);
            }

            yield return new WaitForSecondsRealtime(0.5f);
            EditorApplication.isPlaying = false;
        }

        // Two identical 360-degree sweeps from the Scene 2 spawn: the first one turns toward
        // objects that have never been rendered, the second one repeats the same path. A
        // difference between them is first-use cost (shader/texture warm-up); no difference
        // means turning costs the same as standing still.
        private IEnumerator RunPan()
        {
            var startedUtc = DateTime.UtcNow;
            float startedAt = Time.realtimeSinceStartup;
            var startScene = SceneManager.GetActiveScene();
            yield return Load(WorldMaster, LoadSceneMode.Additive);
            yield return Load(Scene2, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(Scene2));
            yield return Unload(startScene);

            if (request.noVegetationCuller)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] != null && behaviours[i].GetType().Name == "VegetationCuller")
                    {
                        Destroy(behaviours[i].gameObject);
                    }
                }
            }

            yield return new WaitForSecondsRealtime(request.warmup);

            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("FrameProbe: pan needs a main camera.");
                EditorApplication.isPlaying = false;
                yield break;
            }

            var report = new StringBuilder();
            report.Append("{\n\"mode\":\"pan\",\n\"utc\":\"").Append(startedUtc.ToString("o")).Append("\",\n");
            report.Append("\"loadSeconds\":")
                .Append((Time.realtimeSinceStartup - startedAt).ToString("0.00", CultureInfo.InvariantCulture)).Append(",\n");
            report.Append("\"cullerRemoved\":").Append(request.noVegetationCuller ? "true" : "false").Append(",\n");
            report.Append("\"camera\":\"").Append(camera.name).Append("\",\n");
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            report.Append("\"renderersActive\":").Append(renderers.Length).Append(",\n");
            report.Append("\"sweeps\":[");

            Quaternion original = camera.transform.rotation;
            for (int sweep = 0; sweep < 2; sweep++)
            {
                var times = new List<float>();
                var yawProgress = new List<float>();
                float previous = Time.realtimeSinceStartup;
                float elapsed = 0f;
                while (elapsed < request.panSeconds)
                {
                    yield return null;
                    float now = Time.realtimeSinceStartup;
                    float delta = now - previous;
                    previous = now;
                    elapsed += delta;
                    float t = Mathf.Clamp01(elapsed / request.panSeconds);
                    camera.transform.rotation = original * Quaternion.Euler(0f, t * 360f, 0f);
                    times.Add(delta * 1000f);
                    yawProgress.Add(t);
                }

                camera.transform.rotation = original;
                var sorted = new List<float>(times);
                sorted.Sort();
                double sum = 0;
                int over33 = 0, over50 = 0, over100 = 0;
                for (int i = 0; i < times.Count; i++)
                {
                    sum += times[i];
                    if (times[i] > 33.3f) over33++;
                    if (times[i] > 50f) over50++;
                    if (times[i] > 100f) over100++;
                }

                if (sweep > 0)
                {
                    report.Append(',');
                }

                report.Append("{\"sweep\":\"").Append(sweep == 0 ? "cold" : "warm").Append('"');
                report.Append(",\"frames\":").Append(times.Count);
                report.Append(",\"fps\":").Append((1000.0 * times.Count / Math.Max(0.001, times.Count * (sum / times.Count)))
                    .ToString("0.00", CultureInfo.InvariantCulture));
                report.Append(",\"avgMs\":").Append((sum / times.Count).ToString("0.00", CultureInfo.InvariantCulture));
                report.Append(",\"p95Ms\":").Append(sorted[Mathf.Min(sorted.Count - 1, (int)(sorted.Count * 0.95f))]
                    .ToString("0.00", CultureInfo.InvariantCulture));
                report.Append(",\"maxMs\":").Append(sorted[sorted.Count - 1].ToString("0.00", CultureInfo.InvariantCulture));
                report.Append(",\"over33\":").Append(over33).Append(",\"over50\":").Append(over50)
                    .Append(",\"over100\":").Append(over100).Append('}');
                yield return new WaitForSecondsRealtime(2f);
            }

            report.Append("]\n}\n");
            string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp",
                "frame_probe_pan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            try
            {
                File.WriteAllText(output, report.ToString());
                Debug.Log("FrameProbe: wrote " + output);
            }
            catch (Exception e)
            {
                Debug.LogError("FrameProbe: write failed: " + e.Message);
            }

            yield return new WaitForSecondsRealtime(0.5f);
            EditorApplication.isPlaying = false;
        }

        // Jumps straight to the Scene 1 -> Scene 2 handoff so a verification run costs ~20 s
        // instead of playing the whole opening cinematic first.
        private IEnumerator SkipOpeningIntoHandoff()
        {
            MonoBehaviour manager = null;
            for (int attempt = 0; attempt < 240 && manager == null; attempt++)
            {
                var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    var candidate = behaviours[i];
                    if (candidate != null && candidate.GetType().Name == "Scene1OpeningManager" &&
                        candidate.gameObject.scene.path == Scene1)
                    {
                        manager = candidate;
                        break;
                    }
                }

                if (manager == null)
                {
                    yield return null;
                }
            }

            if (manager == null)
            {
                Debug.LogError("FrameProbe: Scene1OpeningManager not found, running the normal opening.");
                yield break;
            }

            var type = manager.GetType();
            var prepare = type.GetMethod("PrepareScene2Transition", BindingFlags.NonPublic | BindingFlags.Instance);
            var transition = type.GetMethod("TransitionToScene2", BindingFlags.NonPublic | BindingFlags.Instance);
            var loadField = type.GetField("scene2TransitionLoadOperation", BindingFlags.NonPublic | BindingFlags.Instance);
            if (prepare == null || transition == null || loadField == null)
            {
                Debug.LogError("FrameProbe: transition methods not found, running the normal opening.");
                yield break;
            }

            manager.StopAllCoroutines();
            yield return manager.StartCoroutine((IEnumerator)prepare.Invoke(manager, null));
            var operation = loadField.GetValue(manager) as AsyncOperation;
            if (operation != null)
            {
                operation.allowSceneActivation = true;
                while (!operation.isDone)
                {
                    yield return null;
                }
            }

            yield return manager.StartCoroutine((IEnumerator)transition.Invoke(manager, null));
        }

        private void ApplyVegetationDiagnostics()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var allObjects = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allObjects.Length; i++)
            {
                var transform = allObjects[i];
                if (transform != null && transform.name == "Vegetation" && transform.childCount > 100)
                {
                    vegetationRoot = transform;
                    break;
                }
            }

            if (vegetationRoot == null)
            {
                return;
            }

            var renderers = vegetationRoot.GetComponentsInChildren<Renderer>(false);
            var distances = new double[renderers.Length];
            var triangles = new int[renderers.Length];
            var casters = new bool[renderers.Length];
            Vector3 cameraPosition = camera.transform.position;
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    distances[i] = -1;
                    continue;
                }

                distances[i] = Math.Sqrt((renderer.bounds.center - cameraPosition).sqrMagnitude);
                casters[i] = renderer.shadowCastingMode != ShadowCastingMode.Off;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh != null)
                {
                    int count = 0;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        count += (int)(mesh.GetIndexCount(sub) / 3);
                    }

                    triangles[i] = count;
                }
            }

            bandDistances = distances;
            bandTriangles = triangles;
            bandCasters = casters;
            censusRenderers = renderers.Length;

            if (request.vegetationCutoff > 0f)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && distances[i] > request.vegetationCutoff)
                    {
                        vegetationDisabled.Add(renderers[i]);
                        renderers[i].enabled = false;
                    }
                }
            }

            if (request.vegetationShadowCutoff > 0f)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null || renderer.shadowCastingMode == ShadowCastingMode.Off)
                    {
                        continue;
                    }

                    if (distances[i] > request.vegetationShadowCutoff)
                    {
                        shadowRestored.Add(renderer);
                        shadowRestoredModes.Add(renderer.shadowCastingMode);
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }
        }

        private IEnumerator Load(string path, LoadSceneMode mode)
        {
            var existing = SceneManager.GetSceneByPath(path);
            if (existing.IsValid() && existing.isLoaded)
            {
                yield break;
            }

            var operation = SceneManager.LoadSceneAsync(path, mode);
            if (operation == null)
            {
                Debug.LogError("FrameProbe: could not load " + path);
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private IEnumerator Unload(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield break;
            }

            var operation = SceneManager.UnloadSceneAsync(scene);
            if (operation == null)
            {
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private void AppendBreakdown(StringBuilder report, Renderer[] renderers)
        {
            var roots = new Dictionary<string, Aggregate>(64);
            var meshes = new Dictionary<Mesh, Aggregate>(512);
            var meshTriangles = new Dictionary<Mesh, int>(512);
            var layers = new Dictionary<string, int>(32);
            int nullMesh = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Mesh mesh = null;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    mesh = filter.sharedMesh;
                }
                else if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }

                int triangles = 0;
                if (mesh != null)
                {
                    if (!meshTriangles.TryGetValue(mesh, out triangles))
                    {
                        triangles = 0;
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            triangles += (int)(mesh.GetIndexCount(sub) / 3);
                        }

                        meshTriangles[mesh] = triangles;
                    }

                    if (!meshes.TryGetValue(mesh, out Aggregate meshAggregate))
                    {
                        meshAggregate = new Aggregate { Name = mesh.name };
                        meshes[mesh] = meshAggregate;
                    }

                    meshAggregate.Renderers++;
                    meshAggregate.Triangles += triangles;
                }
                else
                {
                    nullMesh++;
                }

                var root = renderer.transform.root;
                string rootName = (root.gameObject.scene.IsValid() ? root.gameObject.scene.name : "?") + "/" + root.name;
                if (!roots.TryGetValue(rootName, out Aggregate rootAggregate))
                {
                    rootAggregate = new Aggregate { Name = rootName };
                    roots[rootName] = rootAggregate;
                }

                rootAggregate.Renderers++;
                rootAggregate.Triangles += triangles;
                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    rootAggregate.Casters++;
                }

                if (renderer.isVisible)
                {
                    rootAggregate.Visible++;
                }

                string layerName = LayerMask.LayerToName(renderer.gameObject.layer);
                if (string.IsNullOrEmpty(layerName))
                {
                    layerName = "layer" + renderer.gameObject.layer;
                }

                layers.TryGetValue(layerName, out int layerCount);
                layers[layerName] = layerCount + 1;
            }

            var rootList = new List<Aggregate>(roots.Values);
            rootList.Sort((a, b) => b.Triangles.CompareTo(a.Triangles));
            var meshList = new List<Aggregate>(meshes.Values);
            meshList.Sort((a, b) => b.Triangles.CompareTo(a.Triangles));

            report.Append("\"nullMeshRenderers\":").Append(nullMesh).Append(",");
            report.Append("\"components\":{");
            Number(report, "terrains", FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length);
            Number(report, "particleSystems", FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length);
            Number(report, "audioSources", FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length);
            Number(report, "animators", FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length);
            Number(report, "colliders", FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length);
            Number(report, "meshColliders", FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length);
            report.Append("},");

            report.Append("\"topRoots\":[");
            int rootLimit = Math.Min(25, rootList.Count);
            for (int i = 0; i < rootLimit; i++)
            {
                if (i > 0)
                {
                    report.Append(",");
                }

                var item = rootList[i];
                report.Append("{");
                Text(report, "name", item.Name);
                Number(report, "renderers", item.Renderers);
                Number(report, "casters", item.Casters);
                Number(report, "visible", item.Visible);
                Number(report, "tris", item.Triangles);
                report.Append("}");
            }

            report.Append("],\"topMeshes\":[");
            int meshLimit = Math.Min(20, meshList.Count);
            for (int i = 0; i < meshLimit; i++)
            {
                if (i > 0)
                {
                    report.Append(",");
                }

                var item = meshList[i];
                report.Append("{");
                Text(report, "name", item.Name);
                Number(report, "instances", item.Renderers);
                Number(report, "tris", item.Triangles);
                report.Append("}");
            }

            report.Append("],\"layers\":[");
            bool firstLayer = true;
            foreach (var pair in layers)
            {
                if (!firstLayer)
                {
                    report.Append(",");
                }

                firstLayer = false;
                report.Append("{");
                Text(report, "name", pair.Key);
                Number(report, "renderers", pair.Value);
                report.Append("}");
            }

            report.Append("],");
        }

        private void AppendVegetationBands(StringBuilder report)
        {
            double[] edges = { 0, 15, 30, 45, 60, 75, 100, 150, 200, 300, 400, double.MaxValue };
            report.Append("\"vegetationBands\":[");
            for (int band = 0; band < edges.Length - 1; band++)
            {
                int count = 0;
                int casters = 0;
                long tris = 0;
                for (int i = 0; i < bandDistances.Length; i++)
                {
                    double d = bandDistances[i];
                    if (d < 0 || d < edges[band] || d >= edges[band + 1])
                    {
                        continue;
                    }

                    count++;
                    if (bandCasters[i])
                    {
                        casters++;
                    }

                    tris += bandTriangles[i];
                }

                if (band > 0)
                {
                    report.Append(",");
                }

                report.Append("{");
                Number(report, "from", edges[band]);
                Number(report, "to", edges[band + 1] > 100000 ? -1 : edges[band + 1]);
                Number(report, "renderers", count);
                Number(report, "casters", casters);
                Number(report, "tris", tris);
                report.Append("}");
            }

            report.Append("],");
        }

        private sealed class Aggregate
        {
            public string Name;
            public int Renderers;
            public int Casters;
            public int Visible;
            public long Triangles;
        }

        private static double Percentile(double[] sorted, double p)
        {
            if (sorted == null || sorted.Length == 0)
            {
                return 0;
            }

            int index = (int)Math.Round((sorted.Length - 1) * p);
            if (index < 0)
            {
                index = 0;
            }

            if (index >= sorted.Length)
            {
                index = sorted.Length - 1;
            }

            return sorted[index];
        }

        private static void Text(StringBuilder builder, string name, string value)
        {
            builder.Append("\"").Append(name).Append("\":\"");
            if (value != null)
            {
                builder.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
            }

            builder.Append("\",");
        }

        private static void Number(StringBuilder builder, string name, double value)
        {
            builder.Append("\"").Append(name).Append("\":")
                .Append(value.ToString("0.######", CultureInfo.InvariantCulture)).Append(",");
        }

        private static void ArrayNumber(StringBuilder builder, string name, double value)
        {
            Number(builder, name, value);
        }

        private sealed class Counter
        {
            public readonly string StatName;
            public readonly ProfilerCategory Category;
            public readonly ProfilerRecorder Recorder;
            private readonly List<double> values = new List<double>(4096);

            public Counter(ProfilerCategory category, string statName)
            {
                Category = category;
                StatName = statName;
                Recorder = ProfilerRecorder.StartNew(category, statName, 4096);
            }

            public void Sample()
            {
                if (Recorder.Valid)
                {
                    values.Add(Recorder.LastValueAsDouble);
                }
            }

            public double Average()
            {
                if (values.Count == 0)
                {
                    return 0;
                }

                double sum = 0;
                for (int i = 0; i < values.Count; i++)
                {
                    sum += values[i];
                }

                return sum / values.Count;
            }

            public double[] ValuesSorted()
            {
                var copy = values.ToArray();
                Array.Sort(copy);
                return copy;
            }

            public void Dispose()
            {
                if (Recorder.Valid)
                {
                    Recorder.Dispose();
                }
            }
        }
    }
}
#endif
