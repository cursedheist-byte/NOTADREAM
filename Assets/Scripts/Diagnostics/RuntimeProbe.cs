using System;
using System.Collections;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Diagnostic-only logger, activated from the command line:
//   NOT A DREAM.exe -nyxoProbe [-nyxoScene world|scene2|scene1] [-nyxoSweep 25]
//                [-nyxoNoVeg] [-nyxoNoLights] [-nyxoNoShadows]
//                [-nyxoNoProps] [-nyxoLiminal [-nyxoLiminalTimeout 180]] [-nyxoLeaveTest]
// It writes one line per second with the real frame budget split (main thread, present wait,
// GPU, batches, culling, scripts) so a slow machine can be diagnosed from its log.
public sealed class RuntimeProbe : MonoBehaviour
{
    private const float LogInterval = 1f;
    private const string WorldMaster = "World_Master";
    private const string Scene2 = "Scene2_Evening";
    private const string Scene1 = "Scene1_Opening";
    private const string RepeatScene = "Scene1_Opening_Repeat";

    private static bool enabled;
    private static string sceneOverride;
    private static float sweepDegreesPerSecond;
    private static bool noVegetation;
    private static bool noLights;
    private static bool noShadows;
    private static bool noProps;
    private static bool liminalTest;
    private static float liminalTimeout = 150f;
    private static bool leaveTest;

    private float nextLogTime;
    private int windowFrames;
    private float windowDt;
    private long lastHeapBytes;
    private int lastGcCollections;
    private int rendererCount;
    private int shadowCasterCount;

    private FrameTiming[] frameTimings = new FrameTiming[1];
    private ProfilerRecorder playerLoop;
    private ProfilerRecorder presentWait;
    private ProfilerRecorder behaviour;
    private ProfilerRecorder batches;
    private ProfilerRecorder setPass;
    private ProfilerRecorder triangles;
    private ProfilerRecorder cameraRender;
    private ProfilerRecorder culling;
    private ProfilerRecorder drawLoop;
    private ProfilerRecorder shadowAtlas;
    private ProfilerRecorder physics;
    private ProfilerRecorder canvas;
    private Camera sweepCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        ParseCommandLine();
        if (!enabled && string.IsNullOrEmpty(sceneOverride) && !liminalTest && !leaveTest)
            return;

        var host = new GameObject("RuntimeProbe");
        DontDestroyOnLoad(host);
        host.AddComponent<RuntimeProbe>();
    }

    private static void ParseCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-nyxoProbe":
                    enabled = true;
                    break;
                case "-nyxoScene":
                    if (i + 1 < args.Length)
                        sceneOverride = args[i + 1];
                    break;
                case "-nyxoSweep":
                    if (i + 1 < args.Length && float.TryParse(args[i + 1], out float speed))
                        sweepDegreesPerSecond = speed;
                    break;
                case "-nyxoNoVeg":
                    noVegetation = true;
                    break;
                case "-nyxoNoLights":
                    noLights = true;
                    break;
                case "-nyxoNoShadows":
                    noShadows = true;
                    break;
                case "-nyxoNoProps":
                    noProps = true;
                    break;
                case "-nyxoLiminal":
                    liminalTest = true;
                    break;
                case "-nyxoLeaveTest":
                    leaveTest = true;
                    break;
                case "-nyxoLiminalTimeout":
                    if (i + 1 < args.Length && float.TryParse(args[i + 1], out float timeout))
                        liminalTimeout = timeout;
                    break;
            }
        }
    }

    private void Start()
    {
        // A standalone player stops updating when it loses focus, which would freeze the
        // measurement the moment the harness clicked elsewhere.
        Application.runInBackground = true;

        if (enabled)
        {
            Debug.Log("[NyxoProbe] start cpu=" + SystemInfo.processorType + " cores=" + SystemInfo.processorCount
                + " gpu=" + SystemInfo.graphicsDeviceName + " api=" + SystemInfo.graphicsDeviceType
                + " vram=" + SystemInfo.graphicsMemorySize + "MB screen=" + Screen.width + "x" + Screen.height
                + " refresh=" + Screen.currentResolution.refreshRateRatio.value);
            LogRenderSetup();

            playerLoop = StartRecorder(ProfilerCategory.Internal, "PlayerLoop");
            presentWait = StartRecorder(ProfilerCategory.Internal, "Gfx.WaitForPresentOnGfxThread");
            behaviour = StartRecorder(ProfilerCategory.Scripts, "BehaviourUpdate");
            batches = StartRecorder(ProfilerCategory.Render, "Batches Count");
            setPass = StartRecorder(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = StartRecorder(ProfilerCategory.Render, "Triangles Count");
            cameraRender = StartRecorder(ProfilerCategory.Render, "Camera.Render");
            culling = StartRecorder(ProfilerCategory.Render, "Culling");
            drawLoop = StartRecorder(ProfilerCategory.Render, "RenderLoop.Draw");
            shadowAtlas = StartRecorder(ProfilerCategory.Render, "RenderShadowAtlas");
            physics = StartRecorder(ProfilerCategory.Physics, "Physics.Processing");
            canvas = StartRecorder(ProfilerCategory.Gui, "Canvas.SendWillRenderCanvases");
        }

        lastHeapBytes = Profiler.GetTotalAllocatedMemoryLong();
        lastGcCollections = GC.CollectionCount(0);
        CountRenderers();
        nextLogTime = Time.unscaledTime + LogInterval;

        if (!string.IsNullOrEmpty(sceneOverride))
            StartCoroutine(LoadScene());

        if (liminalTest)
            StartCoroutine(RunLiminalTest());

        if (leaveTest)
            StartCoroutine(RunLeaveTest());
    }

    // Drives the pause menu without synthetic OS input: opens it the same way the Esc key does,
    // presses LEAVE GAME through its own method, then confirms the active scene actually became
    // the main menu with a running clock and a visible cursor.
    private IEnumerator RunLeaveTest()
    {
        yield return null;
        yield return null;

        yield return Load(WorldMaster, LoadSceneMode.Single);
        yield return null;
        yield return null;

        PauseSettingsMenu[] menus = FindObjectsByType<PauseSettingsMenu>(FindObjectsSortMode.None);
        PauseSettingsMenu menu = menus.Length > 0 ? menus[0] : null;
        if (menu == null)
        {
            Debug.LogError("[NyxoLeave] no PauseSettingsMenu in " + WorldMaster);
            yield break;
        }

        const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo openMethod = typeof(PauseSettingsMenu).GetMethod("OpenMenu", Priv);
        MethodInfo leaveMethod = typeof(PauseSettingsMenu).GetMethod("LeaveGame", Priv);
        FieldInfo openField = typeof(PauseSettingsMenu).GetField("isOpen", Priv);
        if (openMethod == null || leaveMethod == null || openField == null)
        {
            Debug.LogError("[NyxoLeave] pause menu internals changed, test aborted");
            yield break;
        }

        openMethod.Invoke(menu, null);
        yield return null;
        yield return null;
        Debug.Log("[NyxoLeave] opened page stack isOpen=" + openField.GetValue(menu)
            + " timeScale=" + Time.timeScale
            + " cursor=" + Cursor.lockState + "/" + Cursor.visible);

        yield return new WaitForSecondsRealtime(0.7f);
        Debug.Log("[NyxoLeave] pressing leave game");
        leaveMethod.Invoke(menu, null);
        yield return null;

        float start = Time.realtimeSinceStartup;
        while (SceneManager.GetActiveScene().name != "MainMenu" && Time.realtimeSinceStartup - start < 60f)
            yield return null;

        yield return null;
        yield return null;
        string scene = SceneManager.GetActiveScene().name;
        MainMenuController[] controllers = FindObjectsByType<MainMenuController>(FindObjectsSortMode.None);
        Debug.Log("[NyxoLeave] result scene=" + scene
            + " after=" + (Time.realtimeSinceStartup - start).ToString("0.00") + "s"
            + " mainMenuController=" + (controllers.Length > 0)
            + " timeScale=" + Time.timeScale
            + " cursor=" + Cursor.lockState + "/" + Cursor.visible);

        // The desktop may be busy, so pull the frame straight from the player instead of
        // relying on a screen grab of whatever window happens to be on top.
        yield return new WaitForSecondsRealtime(1f);
        string capturePath = Application.persistentDataPath + "/nyxo_menu_capture.png";
        ScreenCapture.CaptureScreenshot(capturePath);
        Debug.Log("[NyxoLeave] capture=" + capturePath);
        yield return new WaitForSecondsRealtime(1.5f);
    }

    private static void LogRenderSetup()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        string mode = "?";
        string grd = "?";
        if (pipeline != null)
        {
            grd = pipeline.gpuResidentDrawerMode.ToString();
            if (pipeline.rendererDataList.Length > 0 && pipeline.rendererDataList[0] is UniversalRendererData data)
                mode = data.renderingMode.ToString();
        }

        Debug.Log("[NyxoProbe] renderer=" + mode + " grd=" + grd
            + " renderScale=" + (pipeline != null ? pipeline.renderScale : -1f)
            + " msaa=" + (pipeline != null ? pipeline.msaaSampleCount : -1)
            + " hdr=" + (pipeline != null && pipeline.supportsHDR)
            + " additionalLights=" + (pipeline != null ? pipeline.additionalLightsRenderingMode + "/" + pipeline.maxAdditionalLightsCount : "?")
            + " shadows=" + QualitySettings.shadows
            + " shadowDist=" + QualitySettings.shadowDistance
            + " preset=" + GraphicsPresetController.Current);
    }

    private ProfilerRecorder StartRecorder(ProfilerCategory category, string marker)
    {
        try
        {
            return ProfilerRecorder.StartNew(category, marker, 1);
        }
        catch (Exception)
        {
            return default;
        }
    }

    private IEnumerator LoadScene()
    {
        yield return null;
        yield return null;

        string requested = sceneOverride.ToLowerInvariant();
        if (requested == "scene1")
        {
            yield return Load(Scene1, LoadSceneMode.Single);
        }
        else
        {
            yield return Load(WorldMaster, LoadSceneMode.Additive);
            Scene world = SceneManager.GetSceneByName(WorldMaster);
            if (world.IsValid())
                SceneManager.SetActiveScene(world);

            if (requested == "scene2")
            {
                yield return Load(Scene2, LoadSceneMode.Additive);
                Scene evening = SceneManager.GetSceneByName(Scene2);
                if (evening.IsValid())
                    SceneManager.SetActiveScene(evening);
            }

            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                bool keep = scene.name == WorldMaster || scene.name == Scene2;
                if (!keep && scene.isLoaded)
                    yield return Unload(scene);
            }
        }

        ApplyToggles();
        CountRenderers();
        sweepCamera = null;
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        int capsules = 0;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] is CapsuleCollider)
                capsules++;
        }

        Debug.Log("[NyxoProbe] loaded scene=" + SceneManager.GetActiveScene().name
            + " renderers=" + rendererCount + " colliders=" + colliders.Length + " capsules=" + capsules
            + " mainCamera=" + (Camera.main != null ? Camera.main.name : "none"));
    }

    private IEnumerator RunLiminalTest()
    {
        yield return null;
        yield return null;

        yield return Load(WorldMaster, LoadSceneMode.Additive);
        Scene world = SceneManager.GetSceneByName(WorldMaster);
        if (world.IsValid())
            SceneManager.SetActiveScene(world);

        yield return Load(RepeatScene, LoadSceneMode.Additive);

        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            bool keep = scene.name == WorldMaster || scene.name == RepeatScene;
            if (!keep && scene.isLoaded)
                yield return Unload(scene);
        }

        Scene1OpeningManager manager = null;
        Scene1OpeningManager[] managers = FindObjectsByType<Scene1OpeningManager>(FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length && manager == null; i++)
        {
            if (managers[i] != null && managers[i].gameObject.scene.name == RepeatScene)
                manager = managers[i];
        }

        if (manager == null)
        {
            Debug.LogError("[NyxoLiminal] no Scene1OpeningManager inside " + RepeatScene);
            yield break;
        }

        Debug.Log("[NyxoLiminal] start accepted=" + manager.DebugStartLiminalEndingTest()
            + " timeout=" + liminalTimeout.ToString("0"));

        float elapsed = 0f;
        Transform environment = null;
        while (elapsed < liminalTimeout)
        {
            GameObject found = GameObject.Find("LiminalEndingEnvironment");
            if (found != null && found.activeInHierarchy)
            {
                environment = found.transform;
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (environment == null)
        {
            Debug.LogError("[NyxoLiminal] environment never became active within " + liminalTimeout.ToString("0") + "s");
            yield break;
        }

        Debug.Log("[NyxoLiminal] environment active at " + Fmt(environment.position)
            + " after " + elapsed.ToString("0.0") + "s; letting the player settle");
        yield return new WaitForSecondsRealtime(3f);

        for (int sample = 0; sample < 12; sample++)
        {
            LogLiminalSample(environment, sample);
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private void LogLiminalSample(Transform environment, int sample)
    {
        CharacterController controller = FindLiminalController(environment);
        Vector3 position = controller != null ? controller.transform.position : environment.position;
        string grounded = controller != null ? controller.isGrounded.ToString() : "noController";

        string under = "none";
        if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out RaycastHit underHit, 140f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            under = underHit.collider.name + "@y" + underHit.point.y.ToString("0.00")
                + " gap=" + (position.y - underHit.point.y).ToString("0.00");
        }

        const int Grid = 7;
        const float Span = 14f;
        int hits = 0;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int ix = 0; ix < Grid; ix++)
        {
            float x = position.x + (ix / (Grid - 1f) - 0.5f) * Span * 2f;
            for (int iz = 0; iz < Grid; iz++)
            {
                float z = position.z + (iz / (Grid - 1f) - 0.5f) * Span * 2f;
                var origin = new Vector3(x, position.y + 40f, z);
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 160f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;

                hits++;
                minY = Mathf.Min(minY, hit.point.y);
                maxY = Mathf.Max(maxY, hit.point.y);
            }
        }

        Debug.Log("[NyxoLiminal] sample=" + sample + " pos=" + Fmt(position)
            + " grounded=" + grounded
            + " below=" + under
            + " grid=" + hits + "/" + (Grid * Grid)
            + " groundY=" + (hits > 0 ? minY.ToString("0.00") + ".." + maxY.ToString("0.00") : "none"));
    }

    private static CharacterController FindLiminalController(Transform environment)
    {
        CharacterController best = null;
        float bestDistance = float.MaxValue;
        CharacterController[] controllers = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            CharacterController candidate = controllers[i];
            if (candidate == null || !candidate.enabled)
                continue;

            float distance = Vector3.Distance(candidate.transform.position, environment.position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = candidate;
        }

        return bestDistance < 500f ? best : null;
    }

    private static string Fmt(Vector3 value)
    {
        return value.x.ToString("0.00") + "," + value.y.ToString("0.00") + "," + value.z.ToString("0.00");
    }

    private void ApplyToggles()
    {
        if (noProps)
        {
            NoPropsMode.SetActive(true, false);
            Debug.Log("[NyxoProbe] no props mode on");
        }
        else if (NoPropsMode.StoredValue)
        {
            // A previous run may have persisted the toggle; probe baselines have to start clean.
            NoPropsMode.SetActive(false, false);
            Debug.Log("[NyxoProbe] no props mode forced off for baseline");
        }

        if (noShadows)
            QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;

        if (noVegetation)
        {
            Renderer[] all = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            Transform vegetation = null;
            for (int i = 0; i < all.Length && vegetation == null; i++)
            {
                Transform root = all[i].transform.root;
                if (root != null && root.name == "Vegetation")
                    vegetation = root;
            }

            if (vegetation != null)
            {
                vegetation.gameObject.SetActive(false);
                Debug.Log("[NyxoProbe] vegetation root disabled, children=" + vegetation.childCount);
            }
        }

        if (noLights)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            int disabled = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type != LightType.Directional)
                {
                    lights[i].enabled = false;
                    disabled++;
                }
            }

            Debug.Log("[NyxoProbe] non-directional lights disabled=" + disabled);
        }
    }

    private static IEnumerator Load(string name, LoadSceneMode mode)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(name, mode);
        if (operation == null)
        {
            Debug.LogError("[NyxoProbe] could not load " + name);
            yield break;
        }

        while (!operation.isDone)
            yield return null;
    }

    private static IEnumerator Unload(Scene scene)
    {
        AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
        if (operation == null)
            yield break;

        while (!operation.isDone)
            yield return null;
    }

    private void Update()
    {
        windowFrames++;
        windowDt += Time.unscaledDeltaTime;

        if (sweepDegreesPerSecond != 0f)
        {
            if (sweepCamera == null || !sweepCamera.isActiveAndEnabled)
                sweepCamera = Camera.main;

            if (sweepCamera != null)
                sweepCamera.transform.Rotate(Vector3.up, sweepDegreesPerSecond * Time.unscaledDeltaTime, Space.World);
        }

        if (Time.unscaledTime < nextLogTime)
            return;

        nextLogTime = Time.unscaledTime + LogInterval;
        LogWindow();
    }

    private void LogWindow()
    {
        float seconds = Mathf.Max(0.0001f, windowDt);
        float fps = windowFrames / seconds;
        float frameMs = seconds * 1000f / Mathf.Max(1, windowFrames);

        long heap = Profiler.GetTotalAllocatedMemoryLong();
        long gcBytes = heap - lastHeapBytes;
        lastHeapBytes = heap;
        int gcCollections = GC.CollectionCount(0);
        int gcDelta = gcCollections - lastGcCollections;
        lastGcCollections = gcCollections;

        string cpuFrame = "?";
        string gpuFrame = "?";
        try
        {
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0)
            {
                cpuFrame = frameTimings[0].cpuFrameTime.ToString("0.0");
                gpuFrame = frameTimings[0].gpuFrameTime.ToString("0.0");
            }
        }
        catch (Exception)
        {
            // FrameTimingManager is not available on every platform.
        }

        var camera = sweepCamera != null ? sweepCamera : Camera.main;
        Vector3 position = camera != null ? camera.transform.position : Vector3.zero;

        var line = new StringBuilder(340);
        line.Append("[NyxoProbe] t=").Append(Time.unscaledTime.ToString("0"))
            .Append(" scene=").Append(SceneManager.GetActiveScene().name)
            .Append(" focused=").Append(Application.isFocused ? 1 : 0)
            .Append(" fps=").Append(fps.ToString("0"))
            .Append(" frameMs=").Append(frameMs.ToString("0.0"))
            .Append(" cpu=").Append(cpuFrame)
            .Append(" gpu=").Append(gpuFrame)
            .Append(" loop=").Append(Ms(playerLoop))
            .Append(" present=").Append(Ms(presentWait))
            .Append(" behave=").Append(Ms(behaviour))
            .Append(" cull=").Append(Ms(culling))
            .Append(" draw=").Append(Ms(drawLoop))
            .Append(" camRender=").Append(Ms(cameraRender))
            .Append(" shadowPass=").Append(Ms(shadowAtlas))
            .Append(" phys=").Append(Ms(physics))
            .Append(" canvas=").Append(Ms(canvas))
            .Append(" batches=").Append(Count(batches))
            .Append(" setPass=").Append(Count(setPass))
            .Append(" tris=").Append(Count(triangles))
            .Append(" gcKB=").Append((gcBytes / 1024).ToString())
            .Append(" gc0=").Append(gcDelta)
            .Append(" rnd=").Append(rendererCount)
            .Append(" casters=").Append(shadowCasterCount)
            .Append(" pos=").Append(position.x.ToString("0")).Append(',').Append(position.y.ToString("0"))
            .Append(',').Append(position.z.ToString("0"));

        Debug.Log(line.ToString());

        windowFrames = 0;
        windowDt = 0f;
    }

    private static string Ms(ProfilerRecorder recorder)
    {
        if (!recorder.Valid)
            return "na";

        return (recorder.LastValue / 1000000f).ToString("0.0");
    }

    private static string Count(ProfilerRecorder recorder)
    {
        if (!recorder.Valid)
            return "na";

        return recorder.LastValue.ToString();
    }

    private void CountRenderers()
    {
        var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        rendererCount = renderers.Length;
        int casters = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].shadowCastingMode != ShadowCastingMode.Off)
                casters++;
        }

        shadowCasterCount = casters;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CountRenderers();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        Dispose(playerLoop);
        Dispose(presentWait);
        Dispose(behaviour);
        Dispose(batches);
        Dispose(setPass);
        Dispose(triangles);
        Dispose(cameraRender);
        Dispose(culling);
        Dispose(drawLoop);
        Dispose(shadowAtlas);
        Dispose(physics);
        Dispose(canvas);
    }

    private static void Dispose(ProfilerRecorder recorder)
    {
        if (recorder.Valid)
            recorder.Dispose();
    }
}
