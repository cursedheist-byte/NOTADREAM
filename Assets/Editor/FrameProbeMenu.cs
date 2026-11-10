#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NyxoPerf
{
    [InitializeOnLoad]
    public static class FrameProbeMenu
    {
        private const string RestoreKey = "NyxoProbe.RestoreGameView";
        private const string PreviousKey = "NyxoProbe.GameViewWasMaximized";

        static FrameProbeMenu()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Perf Probe/Scene 1 Opening (single)")]
        public static void RunScene1()
        {
            Start(new ProbeRequest { mode = "scene1", warmup = 4f, duration = 10f });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 Evening (+World_Master)")]
        public static void RunScene2()
        {
            Start(new ProbeRequest { mode = "scene2", warmup = 5f, duration = 10f });
        }

        [MenuItem("Tools/Perf Probe/World_Master only")]
        public static void RunWorld()
        {
            Start(new ProbeRequest { mode = "world", warmup = 4f, duration = 10f });
        }

        [MenuItem("Tools/Perf Probe/Scene 1 -> Scene 2 Transition")]
        public static void RunTransition()
        {
            Start(new ProbeRequest { mode = "transition", duration = 10f, transitionTimeout = 240f });
        }

        [MenuItem("Tools/Perf Probe/Scene 1 -> Scene 2 Handoff (fast)")]
        public static void RunHandoffFast()
        {
            Start(new ProbeRequest
            {
                mode = "transition", duration = 10f, transitionTimeout = 120f, transitionFast = true
            });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Camera Pan (cold/warm)")]
        public static void RunPan()
        {
            Start(new ProbeRequest { mode = "pan", warmup = 5f, duration = 10f, panSeconds = 8f });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Camera Pan (culler off)")]
        public static void RunPanNoCuller()
        {
            Start(new ProbeRequest
            {
                mode = "pan", warmup = 5f, duration = 10f, panSeconds = 8f, noVegetationCuller = true
            });
        }

        [MenuItem("Tools/Perf Probe/Repeat Ending Composite")]
        public static void RunRepeat()
        {
            Start(new ProbeRequest { mode = "repeat", warmup = 5f, duration = 10f });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Diagnostic No Shadows")]
        public static void RunScene2NoShadows()
        {
            Start(new ProbeRequest { mode = "scene2", warmup = 5f, duration = 10f, disableShadows = true });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Diagnostic No Point Lights")]
        public static void RunScene2NoPointLights()
        {
            Start(new ProbeRequest { mode = "scene2", warmup = 5f, duration = 10f, disablePointLights = true });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Diagnostic Real No Shadows")]
        public static void RunScene2RealNoShadows()
        {
            Start(new ProbeRequest
            {
                mode = "scene2", warmup = 5f, duration = 10f, realShadows = true, screenshot = true
            });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Screenshot Baseline")]
        public static void RunScene2Screenshot()
        {
            Start(new ProbeRequest { mode = "scene2", warmup = 5f, duration = 10f, screenshot = true });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Vegetation Cutoff 80m")]
        public static void RunScene2Vegetation80()
        {
            Start(new ProbeRequest
            {
                mode = "scene2", warmup = 5f, duration = 10f, vegetationCutoff = 80f, screenshot = true
            });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Profiler Counters")]
        public static void RunScene2ProfilerCounters()
        {
            Start(new ProbeRequest
            {
                mode = "scene2", warmup = 5f, duration = 10f, enableProfiler = true, screenshot = true
            });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Diagnostic Vegetation Root Off")]
        public static void RunScene2VegetationRootOff()
        {
            Start(new ProbeRequest
            {
                mode = "scene2", warmup = 5f, duration = 10f, disableVegetation = true, screenshot = true
            });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Diagnostic Directional Shadows Off")]
        public static void RunScene2DirectionalShadowsOff()
        {
            Start(new ProbeRequest
            {
                mode = "scene2", warmup = 5f, duration = 10f, disableDirectionalShadows = true, screenshot = true
            });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Diagnostic Post Volumes Off")]
        public static void RunScene2VolumesOff()
        {
            Start(new ProbeRequest
            {
                mode = "scene2", warmup = 5f, duration = 10f, disableVolume = true, screenshot = true
            });
        }

        [MenuItem("Tools/Perf Probe/Scene 2 - Vegetation Shadow Cutoff 45m")]
        public static void RunScene2VegetationShadow45()
        {
            Start(new ProbeRequest
            {
                mode = "scene2", warmup = 5f, duration = 10f, vegetationShadowCutoff = 45f, screenshot = true
            });
        }

        private static void Start(ProbeRequest request)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("FrameProbe: stop Play Mode before starting a probe.");
                return;
            }

            RestoreGameView();

            var gameView = FindGameView();
            if (gameView != null)
            {
                SessionState.SetBool(PreviousKey, gameView.maximized);
                SessionState.SetBool(RestoreKey, true);
                gameView.maximized = true;
            }

            string root = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(root, "Temp", "frame_probe_request.json");
            Directory.CreateDirectory(Path.Combine(root, "Temp"));
            string json = JsonUtility.ToJson(request);
            File.WriteAllText(path, json);
            Debug.Log("FrameProbe: starting " + json);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreGameView();
            }
        }

        private static void RestoreGameView()
        {
            if (!SessionState.GetBool(RestoreKey, false))
            {
                return;
            }

            var gameView = FindGameView();
            if (gameView != null)
            {
                gameView.maximized = SessionState.GetBool(PreviousKey, false);
            }

            SessionState.EraseBool(RestoreKey);
            SessionState.EraseBool(PreviousKey);
        }

        private static EditorWindow FindGameView()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] != null && windows[i].GetType().FullName == "UnityEditor.GameView")
                {
                    return windows[i];
                }
            }

            return null;
        }
    }
}
#endif
