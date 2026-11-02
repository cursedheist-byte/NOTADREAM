using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class PauseSettingsMenu : MonoBehaviour
{
    private enum MenuPage
    {
        Main,
        Video,
        Audio,
        Controls
    }

    private enum GraphicsPreset
    {
        Potato,
        Low,
        Medium,
        High,
        Max
    }

    private enum LoadMode
    {
        Cpu,
        Gpu,
        Mix,
        OnlyGpu
    }

    private const string LoadModePrefKey = "Nyxo.LoadMode";

    [SerializeField] private SimpleFirstPersonController playerController;
    [SerializeField] private Volume highQualityVolume;

    private MenuPage currentPage;
    private GraphicsPreset graphicsPreset = GraphicsPreset.High;
    private LoadMode loadMode = LoadMode.Mix;
    private Resolution[] availableResolutions;
    private AudioSource[] soundSources;
    private float[] originalSourceVolumes;
    private int resolutionIndex;
    private Vector2 scrollPosition;

    private bool fullscreen;
    private bool showFps;
    private int renderDistanceIndex;
    private float masterVolume = 1f;
    private float sfxVolume = 1f;
    private float previousTimeScale = 1f;
    private bool isOpen;
    private bool standaloneMenuHost;
    private System.Action mainMenuClosedCallback;
    private string rebindingName;
    private string rebindStatus;
    private InputAction rebindAction;
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;
    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle labelStyle;
    private GUIStyle pageTitleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle navStyle;
    private GUIStyle selectedNavStyle;
    private GUIStyle menuButtonStyle;
    private GUIStyle footerStyle;
    private Texture2D panelTexture;
    private Texture2D navHoverTexture;
    private Texture2D navSelectedTexture;
    private Texture2D buttonTexture;
    private Texture2D buttonHoverTexture;
    private Texture2D fireIconTexture;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<SimpleFirstPersonController>();

        if (highQualityVolume == null)
            highQualityVolume = FindAnyObjectByType<Volume>();

        availableResolutions = Screen.resolutions;
        if (availableResolutions == null || availableResolutions.Length == 0)
            availableResolutions = new[] { Screen.currentResolution };

        resolutionIndex = FindCurrentResolution();
        fullscreen = Screen.fullScreen;
        showFps = FpsCounter.StoredValue;
        renderDistanceIndex = RenderDistanceSettings.StoredIndex;
        loadMode = (LoadMode)Mathf.Clamp(PlayerPrefs.GetInt(LoadModePrefKey, (int)LoadMode.Mix), 0, 3);
        CacheAudioSources();
    }

    private void Start()
    {
        if (standaloneMenuHost)
        {
            isOpen = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (playerController == null)
        {
            enabled = false;
            return;
        }

        ApplyGraphicsPreset(GraphicsPreset.High);
        CloseMenu();
    }

    private void Update()
    {
        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (rebindOperation != null)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isOpen)
            {
                PlayMenuClick();
                CloseMenu();
            }
            else if (!standaloneMenuHost)
                OpenMenu();
        }
    }

    public void ConfigureForMainMenu()
    {
        standaloneMenuHost = true;
    }

    public void OpenFromMainMenu(System.Action onClosed)
    {
        standaloneMenuHost = true;
        mainMenuClosedCallback = onClosed;
        isOpen = true;
        currentPage = MenuPage.Main;
        scrollPosition = Vector2.zero;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OpenMenu()
    {
        if (isOpen)
            return;

        PlayMenuClick();
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isOpen = true;
        currentPage = MenuPage.Main;
        if (playerController != null)
            playerController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseMenu()
    {
        isOpen = false;
        if (standaloneMenuHost)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var callback = mainMenuClosedCallback;
            mainMenuClosedCallback = null;
            if (callback != null)
                callback();
            return;
        }

        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        if (playerController != null)
            playerController.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnGUI()
    {
        if (!isOpen)
            return;

        EnsureStyles();
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            PlayMenuClick();

        Color oldColor = GUI.color;
        Color oldContentColor = GUI.contentColor;
        GUI.color = new Color(0.008f, 0.012f, 0.022f, 0.88f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.contentColor = Color.white;

        Rect shell = new Rect(Screen.width * 0.025f, Screen.height * 0.035f, Screen.width * 0.95f, Screen.height * 0.93f);
        DrawUiRect(shell, new Color(0.035f, 0.045f, 0.063f, 0.99f));

        float headerHeight = Mathf.Clamp(Screen.height * 0.105f, 68f, 104f);
        Rect header = new Rect(shell.x, shell.y, shell.width, headerHeight);
        DrawUiRect(header, new Color(0.055f, 0.069f, 0.092f, 1f));
        DrawUiRect(new Rect(shell.x, shell.y, 5f, headerHeight), new Color(0.94f, 0.12f, 0.35f, 1f));
        GUI.Label(new Rect(shell.x + 30f, shell.y + 12f, 50f, headerHeight - 24f), "◆", titleStyle);
        GUI.Label(new Rect(shell.x + 88f, shell.y + 10f, 520f, 42f), "SYSTEM SETTINGS", titleStyle);
        GUI.Label(new Rect(shell.x + 90f, shell.y + 49f, 520f, 25f), "NYXO  /  GAME PAUSED", subtitleStyle);
        GUI.Label(new Rect(shell.xMax - 220f, shell.y + 20f, 180f, 32f), "●  PAUSED", footerStyle);
        DrawUiRect(new Rect(shell.x, header.yMax, shell.width, 1f), new Color(0.20f, 0.25f, 0.32f, 1f));

        float navWidth = Mathf.Clamp(shell.width * 0.205f, 190f, 268f);
        Rect sidebar = new Rect(shell.x, header.yMax + 1f, navWidth, shell.height - headerHeight - 1f);
        DrawUiRect(sidebar, new Color(0.026f, 0.035f, 0.051f, 1f));
        Rect content = new Rect(sidebar.xMax + 1f, sidebar.y, shell.width - navWidth - 1f, sidebar.height);
        DrawUiRect(new Rect(sidebar.xMax, sidebar.y, 1f, sidebar.height), new Color(0.17f, 0.21f, 0.28f, 1f));

        float navX = sidebar.x + 18f;
        float navItemWidth = sidebar.width - 36f;
        float footerSpace = sidebar.height > 310f ? 82f : 34f;
        float navTop = sidebar.y + 20f;
        float navAvailable = Mathf.Max(150f, sidebar.height - footerSpace - 28f);
        float navSpacing = Mathf.Clamp((navAvailable - 42f) / 3f, 42f, 62f);
        float navHeight = Mathf.Clamp(navSpacing - 8f, 34f, 50f);
        DrawNavButton(new Rect(navX, navTop, navItemWidth, navHeight), "OVERVIEW", MenuPage.Main);
        DrawNavButton(new Rect(navX, navTop + navSpacing, navItemWidth, navHeight), "DISPLAY", MenuPage.Video);
        DrawNavButton(new Rect(navX, navTop + navSpacing * 2f, navItemWidth, navHeight), "AUDIO", MenuPage.Audio);
        DrawNavButton(new Rect(navX, navTop + navSpacing * 3f, navItemWidth, navHeight), "CONTROLS", MenuPage.Controls);

        if (sidebar.height > 310f)
        {
            Rect resumeRect = new Rect(navX, sidebar.yMax - 82f, navItemWidth, 50f);
            if (GUI.Button(resumeRect, standaloneMenuHost ? "BACK TO MENU" : "RESUME GAME", menuButtonStyle))
                CloseMenu();
        }
        GUI.Label(new Rect(navX, sidebar.yMax - 30f, navItemWidth, 20f),
            standaloneMenuHost ? "ESC  /  BACK TO MENU" : "ESC  /  BACK TO GAME", footerStyle);

        string heading = currentPage == MenuPage.Main ? "OVERVIEW" :
                         currentPage == MenuPage.Video ? "DISPLAY SETTINGS" :
                         currentPage == MenuPage.Audio ? "AUDIO SETTINGS" : "CONTROLS";
        string description = currentPage == MenuPage.Main ? "Manage your game settings from one place." :
                            currentPage == MenuPage.Video ? "Choose visual quality and display mode." :
                            currentPage == MenuPage.Audio ? "Balance the game and sound effects." :
                            "Review or change your keyboard bindings.";
        GUI.Label(new Rect(content.x + 42f, content.y + 24f, content.width - 84f, 40f), heading, pageTitleStyle);
        GUI.Label(new Rect(content.x + 42f, content.y + 64f, content.width - 84f, 28f), description, subtitleStyle);
        DrawUiRect(new Rect(content.x + 42f, content.y + 102f, content.width - 84f, 1f), new Color(0.20f, 0.25f, 0.32f, 1f));

        GUIStyle oldButtonStyle = GUI.skin.button;
        GUIStyle oldLabelStyle = GUI.skin.label;
        GUI.skin.button = menuButtonStyle;
        GUI.skin.label = labelStyle;
        Rect contentArea = new Rect(content.x + 42f, content.y + 120f, content.width - 84f, content.height - 150f);
        GUILayout.BeginArea(contentArea);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(contentArea.width), GUILayout.Height(contentArea.height));
        if (currentPage == MenuPage.Main)
            DrawMainPage();
        else if (currentPage == MenuPage.Video)
            DrawVideoPage();
        else if (currentPage == MenuPage.Audio)
            DrawAudioPage();
        else
            DrawControlsPage();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.skin.button = oldButtonStyle;
        GUI.skin.label = oldLabelStyle;
        GUI.color = oldColor;
        GUI.contentColor = oldContentColor;
    }

    private void DrawNavButton(Rect rect, string label, MenuPage page)
    {
        GUIStyle style = currentPage == page ? selectedNavStyle : navStyle;
        bool selected = currentPage == page;
        bool hovered = rect.Contains(Event.current.mousePosition);
        if (GUI.Button(rect, label, style))
        {
            StopRebinding();
            currentPage = page;
            scrollPosition = Vector2.zero;
        }

        if (selected)
            DrawUiRect(new Rect(rect.x, rect.y, 4f, rect.height), new Color(0.96f, 0.12f, 0.37f, 1f));
        else if (hovered)
            DrawHoverOutline(rect);
    }

    private static void DrawHoverOutline(Rect rect)
    {
        Color accent = new Color(0.96f, 0.12f, 0.37f, 1f);
        DrawUiRect(new Rect(rect.x, rect.y, rect.width, 2f), accent);
        DrawUiRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), accent);
        DrawUiRect(new Rect(rect.x, rect.y, 2f, rect.height), accent);
        DrawUiRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), accent);
    }

    private static void DrawUiRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private void DrawMainPage()
    {
        GUILayout.Space(8f);
        GUILayout.Label("WELCOME BACK", sectionStyle, GUILayout.Height(30f));
        GUILayout.Label("Your session is paused. Choose a settings category from the navigation panel, or resume the game when you are ready.", labelStyle, GUILayout.Height(54f));
        GUILayout.Space(16f);
        GUILayout.Label("QUICK ACCESS", sectionStyle, GUILayout.Height(28f));
        GUILayout.Space(8f);
        if (GUILayout.Button("DISPLAY SETTINGS", menuButtonStyle, GUILayout.Height(52f)))
            currentPage = MenuPage.Video;
        GUILayout.Space(8f);
        if (GUILayout.Button("AUDIO SETTINGS", menuButtonStyle, GUILayout.Height(52f)))
            currentPage = MenuPage.Audio;
        GUILayout.Space(8f);
        if (GUILayout.Button("KEY BINDINGS", menuButtonStyle, GUILayout.Height(52f)))
            currentPage = MenuPage.Controls;
        GUILayout.Space(18f);
        if (GUILayout.Button("RESUME GAME", menuButtonStyle, GUILayout.Height(52f)))
            CloseMenu();
    }

    private void DrawControlsPage()
    {
        GUILayout.Label("Select an action, then press its new key.", labelStyle, GUILayout.Height(32f));
        DrawBindingRow("Move Forward", "Forward");
        DrawBindingRow("Move Backward", "Backward");
        DrawBindingRow("Move Left", "Left");
        DrawBindingRow("Move Right", "Right");
        DrawBindingRow("Jump", "Jump");
        DrawBindingRow("Sprint", "Sprint");
        DrawBindingRow("Interact / Ride", "Interact");

        if (!string.IsNullOrEmpty(rebindStatus))
            GUILayout.Label(rebindStatus, labelStyle, GUILayout.Height(28f));

        GUILayout.Space(12f);
        if (GUILayout.Button("Restore Default Keys", menuButtonStyle, GUILayout.Height(38f)))
        {
            WorldMasterKeyBindings.ResetToDefaults();
            rebindStatus = "Default keys restored.";
        }

        GUILayout.Space(12f);
        if (GUILayout.Button("Back", menuButtonStyle, GUILayout.Height(38f)))
        {
            StopRebinding();
            currentPage = MenuPage.Main;
        }
    }

    private void DrawBindingRow(string label, string bindingName)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, labelStyle, GUILayout.Width(190f), GUILayout.Height(38f));
        string buttonText = rebindingName == bindingName
            ? "Press a key..."
            : WorldMasterKeyBindings.GetKey(bindingName).ToString();
        if (GUILayout.Button(buttonText, menuButtonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(38f)))
            BeginRebinding(bindingName);
        GUILayout.EndHorizontal();
    }

    private void BeginRebinding(string bindingName)
    {
        StopRebinding();
        rebindingName = bindingName;
        rebindStatus = "Waiting for a key (Esc cancels).";
        rebindAction = new InputAction("Rebind", InputActionType.Button, "<Keyboard>/anyKey");
        rebindOperation = rebindAction.PerformInteractiveRebinding()
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithControlsExcluding("<Keyboard>/escape")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(operation =>
            {
                UnityEngine.InputSystem.Controls.KeyControl keyControl =
                    operation.selectedControl as UnityEngine.InputSystem.Controls.KeyControl;
                if (keyControl != null && WorldMasterKeyBindings.TrySetKey(rebindingName, keyControl.keyCode))
                    rebindStatus = "Bound " + rebindingName + " to " + keyControl.keyCode + ".";
                else
                    rebindStatus = "That key is already assigned.";

                StopRebinding();
            })
            .OnCancel(operation =>
            {
                rebindStatus = "Key binding cancelled.";
                StopRebinding();
            })
            .Start();
    }

    private void StopRebinding()
    {
        if (rebindOperation != null)
        {
            rebindOperation.Dispose();
            rebindOperation = null;
        }

        if (rebindAction != null)
        {
            rebindAction.Disable();
            rebindAction.Dispose();
            rebindAction = null;
        }

        rebindingName = null;
    }

    private void OnDestroy()
    {
        StopRebinding();
        DestroyUiTexture(panelTexture);
        DestroyUiTexture(navHoverTexture);
        DestroyUiTexture(navSelectedTexture);
        DestroyUiTexture(buttonTexture);
        DestroyUiTexture(buttonHoverTexture);
        DestroyUiTexture(fireIconTexture);
    }

    private void DrawVideoPage()
    {
        GUILayout.Label("GRAPHICS QUALITY", sectionStyle, GUILayout.Height(30f));
        GUILayout.Label(PresetName(graphicsPreset), labelStyle, GUILayout.ExpandWidth(true), GUILayout.Height(32f));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", menuButtonStyle, GUILayout.Width(70f), GUILayout.Height(42f)))
            SetGraphicsPreset((int)graphicsPreset - 1);
        if (GUILayout.Button("Apply " + PresetName(graphicsPreset), menuButtonStyle, GUILayout.Height(42f)))
            ApplyGraphicsPreset(graphicsPreset);
        if (GUILayout.Button("▶", menuButtonStyle, GUILayout.Width(70f), GUILayout.Height(42f)))
            SetGraphicsPreset((int)graphicsPreset + 1);
        GUILayout.EndHorizontal();
        GUILayout.Space(14f);

        GUILayout.Label("LOAD MODE", sectionStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
        GUILayout.Label(LoadModeName(loadMode), labelStyle, GUILayout.ExpandWidth(true), GUILayout.Height(28f));
        GUILayout.Label(LoadModeHint(loadMode), labelStyle, GUILayout.ExpandWidth(true), GUILayout.Height(42f));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LoadModeButton("CPU", LoadMode.Cpu), menuButtonStyle, GUILayout.Height(40f)))
            SetLoadMode(LoadMode.Cpu);
        if (GUILayout.Button(LoadModeButton("GPU", LoadMode.Gpu), menuButtonStyle, GUILayout.Height(40f)))
            SetLoadMode(LoadMode.Gpu);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(LoadModeButton("MIX", LoadMode.Mix), menuButtonStyle, GUILayout.Height(40f)))
            SetLoadMode(LoadMode.Mix);
        if (GUILayout.Button(
                new GUIContent(LoadModeButton("ONLY GPU", LoadMode.OnlyGpu), fireIconTexture),
                menuButtonStyle,
                GUILayout.Height(40f)))
            SetLoadMode(LoadMode.OnlyGpu);
        GUILayout.EndHorizontal();
        GUILayout.Space(14f);

        GUILayout.Label("RESOLUTION", sectionStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", menuButtonStyle, GUILayout.Width(70f), GUILayout.Height(40f)))
            resolutionIndex = (resolutionIndex + availableResolutions.Length - 1) % availableResolutions.Length;
        GUILayout.Button(ResolutionLabel(availableResolutions[resolutionIndex]), menuButtonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(40f));
        if (GUILayout.Button("▶", menuButtonStyle, GUILayout.Width(70f), GUILayout.Height(40f)))
            resolutionIndex = (resolutionIndex + 1) % availableResolutions.Length;
        GUILayout.EndHorizontal();

        fullscreen = GUILayout.Toggle(fullscreen, "  Fullscreen", GUILayout.Height(32f));
        if (GUILayout.Button("Apply Display Settings", menuButtonStyle, GUILayout.Height(42f)))
        {
            Resolution resolution = availableResolutions[resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        GUILayout.Space(16f);
        GUILayout.Label("PERFORMANCE", sectionStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
        bool showFpsNow = GUILayout.Toggle(showFps, "  Show FPS counter (top-left)", GUILayout.Height(32f));
        if (showFpsNow != showFps)
        {
            showFps = showFpsNow;
            FpsCounter.SetVisible(showFps);
        }

        GUILayout.Space(16f);
        GUILayout.Label("RENDER DISTANCE", sectionStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
        GUILayout.Label("Scenery past this distance is not drawn. Lower it if the game stutters; raise it to see farther.", labelStyle, GUILayout.Height(46f));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", menuButtonStyle, GUILayout.Width(70f), GUILayout.Height(40f)))
            SetRenderDistanceIndex(renderDistanceIndex - 1);
        GUILayout.Button(RenderDistanceSettings.Distances[renderDistanceIndex] + " m", menuButtonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(40f));
        if (GUILayout.Button("▶", menuButtonStyle, GUILayout.Width(70f), GUILayout.Height(40f)))
            SetRenderDistanceIndex(renderDistanceIndex + 1);
        GUILayout.EndHorizontal();

        GUILayout.Space(16f);
        if (GUILayout.Button("Back", menuButtonStyle, GUILayout.Height(38f)))
            currentPage = MenuPage.Main;
    }

    private void SetRenderDistanceIndex(int index)
    {
        int count = RenderDistanceSettings.Distances.Length;
        renderDistanceIndex = ((index % count) + count) % count;
        RenderDistanceSettings.SetIndex(renderDistanceIndex);
    }

    private void SetLoadMode(LoadMode mode)
    {
        loadMode = mode;
        PlayerPrefs.SetInt(LoadModePrefKey, (int)mode);
        PlayerPrefs.Save();
        // Re-apply the quality preset first so a previous mode's overrides are cleared, then
        // ApplyLoadMode layers the new mode on top.
        ApplyGraphicsPreset(graphicsPreset);
    }

    private string LoadModeButton(string caption, LoadMode mode)
    {
        return loadMode == mode ? "[ " + caption + " ]" : caption;
    }

    private static string LoadModeName(LoadMode mode)
    {
        switch (mode)
        {
            case LoadMode.Cpu: return "CPU  •  GPU work reduced, CPU drives the frame";
            case LoadMode.Gpu: return "GPU  •  higher GPU quality, culling moved to the GPU";
            case LoadMode.OnlyGpu: return "ONLY GPU  •  maximum GPU load, CPU kept minimal";
            default: return "MIX  •  balanced CPU + GPU (default)";
        }
    }

    private static string LoadModeHint(LoadMode mode)
    {
        switch (mode)
        {
            case LoadMode.Cpu:
                return "Render scale, MSAA, shadows and post-processing are lowered so the GPU has almost nothing to do and the CPU becomes the bottleneck.";
            case LoadMode.Gpu:
                return "Render scale, MSAA, shadow range and lighting are pushed up; GPU occlusion culling and vsync-off move work from the CPU to the GPU.";
            case LoadMode.OnlyGpu:
                return "Maximum GPU preset (150% render scale, 4x MSAA, 4K shadows, GPU culling) with CPU extras trimmed. Heavy — expect high GPU usage.";
            default:
                return "Uses exactly the quality preset above. Nothing is shifted; this is the normal balance.";
        }
    }

    private void ApplyLoadMode()
    {
        if (loadMode == LoadMode.Mix)
        {
            // Balanced mode keeps the authored look, including SSAO.
            SetAmbientOcclusion(true);
            return;
        }

        UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        Camera playerCamera = playerController != null ? playerController.GetComponentInChildren<Camera>() : null;
        UniversalAdditionalCameraData cameraData = playerCamera != null
            ? playerCamera.GetComponent<UniversalAdditionalCameraData>()
            : null;

        if (loadMode == LoadMode.Cpu)
        {
            // Make the GPU cheap so the CPU is the limiting side of the frame.
            SetAmbientOcclusion(false);
            ScalableBufferManager.ResizeBuffers(0.85f, 0.85f);
            QualitySettings.vSyncCount = 1;
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = UnityEngine.ShadowQuality.HardOnly;
            QualitySettings.shadowDistance = 25f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.lodBias = 0.8f;
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.particleRaycastBudget = 4096;
            if (pipeline != null)
            {
                pipeline.renderScale = 0.8f;
                pipeline.msaaSampleCount = 1;
                pipeline.supportsHDR = false;
                pipeline.supportsCameraDepthTexture = false;
                pipeline.supportsCameraOpaqueTexture = false;
                pipeline.shadowDistance = 25f;
                pipeline.shadowCascadeCount = 1;
                pipeline.mainLightShadowmapResolution = 1024;
                pipeline.additionalLightsShadowmapResolution = 1024;
                pipeline.maxAdditionalLightsCount = 4;
                pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras = false;
            }

            if (highQualityVolume != null)
            {
                highQualityVolume.enabled = false;
            }

            if (playerCamera != null)
            {
                playerCamera.allowHDR = false;
                playerCamera.allowMSAA = false;
            }

            if (cameraData != null)
            {
                cameraData.renderPostProcessing = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.renderShadows = true;
            }

            return;
        }

        // GPU / Only GPU: push the expensive work onto the GPU and trim CPU-side extras.
        bool onlyGpu = loadMode == LoadMode.OnlyGpu;
        float renderScale = onlyGpu ? 1.5f : 1.25f;
        SetAmbientOcclusion(true);
        ScalableBufferManager.ResizeBuffers(1f, 1f);
        QualitySettings.vSyncCount = 0;
        QualitySettings.antiAliasing = 4;
        QualitySettings.shadows = UnityEngine.ShadowQuality.All;
        QualitySettings.shadowDistance = onlyGpu ? 120f : 80f;
        QualitySettings.shadowCascades = onlyGpu ? 4 : 2;
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
        QualitySettings.lodBias = onlyGpu ? 2f : 1.4f;
        QualitySettings.maximumLODLevel = 0;
        QualitySettings.particleRaycastBudget = onlyGpu ? 32 : 128;
        QualitySettings.asyncUploadTimeSlice = onlyGpu ? 8 : 4;
        QualitySettings.asyncUploadBufferSize = onlyGpu ? 128 : 64;
        if (pipeline != null)
        {
            pipeline.renderScale = renderScale;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = true;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.shadowDistance = onlyGpu ? 120f : 80f;
            pipeline.shadowCascadeCount = onlyGpu ? 4 : 2;
            pipeline.mainLightShadowmapResolution = onlyGpu ? 4096 : 2048;
            pipeline.additionalLightsShadowmapResolution = onlyGpu ? 2048 : 1024;
            pipeline.maxAdditionalLightsCount = 8;
            // Occlusion culling is computed on the GPU instead of the CPU.
            pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras = true;
        }

        if (highQualityVolume != null)
        {
            highQualityVolume.enabled = true;
        }

        if (playerCamera != null)
        {
            playerCamera.allowHDR = true;
            playerCamera.allowMSAA = true;
        }

        if (cameraData != null)
        {
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.renderShadows = true;
        }
    }

    // SSAO is a renderer feature, not a post-processing volume effect, so the quality presets do
    // not touch it. The load modes switch it because it is one of the most expensive full-screen
    // passes: off makes the GPU cheap, on makes the GPU do more.
    private static void SetAmbientOcclusion(bool active)
    {
        UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null)
        {
            return;
        }

        foreach (ScriptableRendererData rendererData in pipeline.rendererDataList)
        {
            if (rendererData == null || rendererData.rendererFeatures == null)
            {
                continue;
            }

            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion)
                {
                    feature.SetActive(active);
                }
            }
        }
    }

    private void DrawAudioPage()
    {
        GUILayout.Label("MASTER VOLUME", sectionStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
        masterVolume = DrawVolumeSlider(masterVolume);
        AudioListener.volume = masterVolume;

        GUILayout.Space(12f);
        GUILayout.Label("SOUND EFFECTS", sectionStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
        sfxVolume = DrawVolumeSlider(sfxVolume);
        ApplySfxVolume();

        GUILayout.Space(16f);
        if (GUILayout.Button("Back", menuButtonStyle, GUILayout.Height(38f)))
            currentPage = MenuPage.Main;
    }

    private float DrawVolumeSlider(float value)
    {
        GUILayout.BeginHorizontal();
        float updatedValue = GUILayout.HorizontalSlider(value, 0f, 1f, GUILayout.Height(28f));
        GUILayout.Button(Mathf.RoundToInt(updatedValue * 100f) + "%", menuButtonStyle, GUILayout.Width(55f), GUILayout.Height(28f));
        GUILayout.EndHorizontal();
        return updatedValue;
    }

private void ApplyGraphicsPreset(GraphicsPreset preset)
    {
        graphicsPreset = preset;
        bool potato = preset == GraphicsPreset.Potato;
        bool low = preset == GraphicsPreset.Low;
        bool medium = preset == GraphicsPreset.Medium;
        bool high = preset == GraphicsPreset.High;
        bool max = preset == GraphicsPreset.Max;
        bool postProcessing = !potato && !low;

        QualitySettings.SetQualityLevel(potato || low ? 0 : 1, true);
        QualitySettings.vSyncCount = potato || low ? 0 : 1;
        QualitySettings.antiAliasing = max ? 4 : potato || low ? 0 : 2;
        QualitySettings.shadows = potato ? UnityEngine.ShadowQuality.Disable : low ? UnityEngine.ShadowQuality.HardOnly : UnityEngine.ShadowQuality.All;
        QualitySettings.shadowResolution = max || high ? UnityEngine.ShadowResolution.VeryHigh : medium ? UnityEngine.ShadowResolution.High : UnityEngine.ShadowResolution.Low;
        QualitySettings.shadowDistance = potato ? 0f : low ? 25f : medium ? 45f : high ? 60f : 120f;
        QualitySettings.shadowCascades = max ? 4 : high ? 2 : 1;
        QualitySettings.shadowProjection = max ? UnityEngine.ShadowProjection.CloseFit : UnityEngine.ShadowProjection.StableFit;
        QualitySettings.shadowNearPlaneOffset = max ? 1f : 3f;
        QualitySettings.lodBias = potato ? 0.5f : low ? 0.8f : medium ? 1.1f : high ? 1f : 2f;
        QualitySettings.maximumLODLevel = potato ? 1 : 0;
        QualitySettings.anisotropicFiltering = potato ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
        QualitySettings.realtimeReflectionProbes = postProcessing;
        QualitySettings.billboardsFaceCameraPosition = max;
        QualitySettings.softParticles = !potato && !low;
        QualitySettings.particleRaycastBudget = max ? 4096 : potato ? 64 : low ? 256 : medium ? 1024 : 2048;
        QualitySettings.asyncUploadTimeSlice = max ? 4 : 2;
        QualitySettings.asyncUploadBufferSize = max ? 64 : 16;
        QualitySettings.asyncUploadPersistentBuffer = max;
        QualitySettings.skinWeights = SkinWeights.FourBones;
        QualitySettings.streamingMipmapsActive = max || high;
        QualitySettings.streamingMipmapsMemoryBudget = max ? 1024f : 512f;
        QualitySettings.streamingMipmapsMaxLevelReduction = 1;
        RenderSettings.ambientIntensity = max ? 1.18f : 1.05f;
        ScalableBufferManager.ResizeBuffers(potato ? 0.65f : low ? 0.8f : medium ? 0.9f : 1f, potato ? 0.65f : low ? 0.8f : medium ? 0.9f : 1f);

        UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null)
        {
            pipeline.renderScale = max ? 1.5f : potato ? 0.75f : low ? 0.85f : medium ? 0.9f : 1f;
            pipeline.msaaSampleCount = max ? 4 : potato || low ? 1 : 2;
            pipeline.shadowDistance = potato ? 0f : low ? 25f : medium ? 45f : high ? 60f : 120f;
            pipeline.shadowCascadeCount = max ? 4 : high ? 2 : 1;
            pipeline.supportsHDR = postProcessing;
            pipeline.maxAdditionalLightsCount = max ? 8 : 4;
            pipeline.supportsCameraDepthTexture = postProcessing;
            // Nothing in the project samples _CameraOpaqueTexture, so the full-screen color copy is pure cost.
            pipeline.supportsCameraOpaqueTexture = false;

            // AAA-only: crank the actual shadowmap resolution to 4K.
            if (max)
            {
                pipeline.mainLightShadowmapResolution = 4096;
                pipeline.additionalLightsShadowmapResolution = 2048;
            }
            else if (high)
            {
                pipeline.mainLightShadowmapResolution = 2048;
                pipeline.additionalLightsShadowmapResolution = 1024;
            }
            else
            {
                pipeline.mainLightShadowmapResolution = 1024;
                pipeline.additionalLightsShadowmapResolution = 1024;
            }
        }

        if (highQualityVolume != null)
        {
            highQualityVolume.enabled = postProcessing;
            VolumeProfile profile = highQualityVolume.sharedProfile;
            if (profile != null)
            {
                if (profile.TryGet(out ColorAdjustments colorAdjustments))
                {
                    colorAdjustments.postExposure.value = max ? 0.18f : 0.1f;
                    colorAdjustments.contrast.value = max ? 18f : 12f;
                    colorAdjustments.saturation.value = max ? 14f : 8f;
                    colorAdjustments.colorFilter.value = max
                        ? new Color(1f, 0.9f, 0.78f, 1f)
                        : new Color(1f, 0.98f, 0.95f, 1f);
                }

                if (profile.TryGet(out Bloom bloom))
                {
                    bloom.intensity.value = max ? 0.5f : 0.25f;
                    bloom.threshold.value = max ? 0.9f : 1.1f;
                    bloom.scatter.value = max ? 0.75f : 0.65f;
                    bloom.highQualityFiltering.value = max;
                }

                if (profile.TryGet(out FilmGrain filmGrain))
                {
                    filmGrain.active = max;
                    filmGrain.intensity.value = 0.06f;
                    filmGrain.response.value = 0.8f;
                }

                if (profile.TryGet(out ChromaticAberration chromaticAberration))
                {
                    chromaticAberration.active = max;
                    chromaticAberration.intensity.value = 0.02f;
                }

                if (profile.TryGet(out Vignette vignette))
                {
                    vignette.active = max;
                    vignette.intensity.value = max ? 0.24f : 0.18f;
                    vignette.smoothness.value = 0.5f;
                }

                // Neutral ACES-style tonemapping reads as "real camera" footage (AAA).
                if (profile.TryGet(out Tonemapping tonemapping))
                {
                    tonemapping.active = postProcessing;
                    tonemapping.mode.value = max ? TonemappingMode.ACES : TonemappingMode.Neutral;
                }
            }
        }

        Camera playerCamera = playerController != null ? playerController.GetComponentInChildren<Camera>() : null;
        if (playerCamera != null)
        {
            playerCamera.allowHDR = postProcessing;
            playerCamera.allowMSAA = postProcessing;
            UniversalAdditionalCameraData cameraData = playerCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = postProcessing;
                cameraData.antialiasing = max
                    ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                    : postProcessing ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
                cameraData.antialiasingQuality = max
                    ? AntialiasingQuality.High
                    : AntialiasingQuality.Medium;
                cameraData.renderShadows = !potato;
                cameraData.dithering = max;
            }
        }

        // The load mode is applied on top of the preset, so "Mix" leaves the preset untouched.
        ApplyLoadMode();
    }

    private void SetGraphicsPreset(int value)
    {
        graphicsPreset = (GraphicsPreset)Mathf.Clamp(value, 0, 4);
        ApplyGraphicsPreset(graphicsPreset);
    }

    private void CacheAudioSources()
    {
        soundSources = FindObjectsByType<AudioSource>();
        originalSourceVolumes = new float[soundSources.Length];
        for (int i = 0; i < soundSources.Length; i++)
            originalSourceVolumes[i] = soundSources[i] != null ? soundSources[i].volume : 0f;
    }

    private static void PlayMenuClick()
    {
        var sceneAudio = FindAnyObjectByType<Scene2EveningManager>();
        if (sceneAudio != null)
            sceneAudio.PlayUiClick();
    }

    private void ApplySfxVolume()
    {
        for (int i = 0; i < soundSources.Length; i++)
        {
            if (soundSources[i] != null)
                soundSources[i].volume = originalSourceVolumes[i] * sfxVolume;
        }
    }

    private int FindCurrentResolution()
    {
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            if (availableResolutions[i].width == Screen.width && availableResolutions[i].height == Screen.height)
                return i;
        }
        return availableResolutions.Length - 1;
    }

    private static string ResolutionLabel(Resolution resolution)
    {
        return resolution.width + " × " + resolution.height;
    }

private static string PresetName(GraphicsPreset preset)
    {
        switch (preset)
        {
            case GraphicsPreset.Potato: return "Potato  •  effects off / minimal detail";
            case GraphicsPreset.Low: return "Low  •  lightweight shadows";
            case GraphicsPreset.Medium: return "Medium  •  balanced detail";
            case GraphicsPreset.High: return "High  •  full lighting / post-processing";
            default: return "MAX  •  ultra shadows / 125% render / cinematic grade";
        }
    }

private void EnsureStyles()
    {
        if (titleStyle != null)
            return;

    panelTexture = CreateUiTexture(new Color(0.035f, 0.045f, 0.063f, 1f));
    navHoverTexture = CreateUiTexture(new Color(0.10f, 0.13f, 0.18f, 1f));
    navSelectedTexture = CreateUiTexture(new Color(0.105f, 0.13f, 0.18f, 1f));
    buttonTexture = CreateUiTexture(new Color(0.075f, 0.095f, 0.13f, 1f));
    buttonHoverTexture = CreateUiTexture(new Color(0.16f, 0.21f, 0.29f, 1f));
    fireIconTexture = CreateFireIcon();
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(0.88f, 0.95f, 1f);
        titleStyle.onNormal.textColor = titleStyle.normal.textColor;
        titleStyle.hover.textColor = titleStyle.normal.textColor;
        titleStyle.active.textColor = titleStyle.normal.textColor;
        titleStyle.focused.textColor = titleStyle.normal.textColor;
        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        sectionStyle.normal.textColor = new Color(0.58f, 0.82f, 1f);
        sectionStyle.onNormal.textColor = sectionStyle.normal.textColor;
        sectionStyle.hover.textColor = sectionStyle.normal.textColor;
        sectionStyle.active.textColor = sectionStyle.normal.textColor;
        sectionStyle.focused.textColor = sectionStyle.normal.textColor;
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 15
        };
        labelStyle.normal.textColor = new Color(0.9f, 0.94f, 1f);
        labelStyle.onNormal.textColor = labelStyle.normal.textColor;
        labelStyle.hover.textColor = labelStyle.normal.textColor;
        labelStyle.active.textColor = labelStyle.normal.textColor;
        labelStyle.focused.textColor = labelStyle.normal.textColor;

        pageTitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 26,
            fontStyle = FontStyle.Bold
        };
        pageTitleStyle.normal.textColor = new Color(0.95f, 0.97f, 1f);
        pageTitleStyle.onNormal.textColor = pageTitleStyle.normal.textColor;
        pageTitleStyle.hover.textColor = pageTitleStyle.normal.textColor;
        pageTitleStyle.active.textColor = pageTitleStyle.normal.textColor;
        pageTitleStyle.focused.textColor = pageTitleStyle.normal.textColor;

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 14
        };
        subtitleStyle.normal.textColor = new Color(0.64f, 0.70f, 0.79f);
        subtitleStyle.onNormal.textColor = subtitleStyle.normal.textColor;
        subtitleStyle.hover.textColor = subtitleStyle.normal.textColor;
        subtitleStyle.active.textColor = subtitleStyle.normal.textColor;
        subtitleStyle.focused.textColor = subtitleStyle.normal.textColor;

        footerStyle = new GUIStyle(subtitleStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };
        footerStyle.normal.textColor = new Color(0.68f, 0.74f, 0.83f);
        footerStyle.onNormal.textColor = footerStyle.normal.textColor;
        footerStyle.hover.textColor = footerStyle.normal.textColor;
        footerStyle.active.textColor = footerStyle.normal.textColor;
        footerStyle.focused.textColor = footerStyle.normal.textColor;

        navStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(16, 10, 8, 8),
            normal = { background = panelTexture, textColor = new Color(0.68f, 0.74f, 0.83f) },
            hover = { background = navHoverTexture, textColor = Color.white },
            onHover = { background = navHoverTexture, textColor = Color.white },
            active = { background = navSelectedTexture, textColor = Color.white },
            focused = { background = navSelectedTexture, textColor = Color.white }
        };

        selectedNavStyle = new GUIStyle(navStyle)
        {
            normal = { background = navSelectedTexture, textColor = Color.white },
            hover = { background = navSelectedTexture, textColor = Color.white },
            active = { background = navSelectedTexture, textColor = Color.white },
            focused = { background = navSelectedTexture, textColor = Color.white }
        };

        menuButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(16, 14, 8, 8),
            border = new RectOffset(1, 1, 1, 1),
            normal = { background = buttonTexture, textColor = new Color(0.91f, 0.94f, 0.98f) },
            hover = { background = buttonHoverTexture, textColor = Color.white },
            onHover = { background = buttonHoverTexture, textColor = Color.white },
            active = { background = navSelectedTexture, textColor = Color.white },
            focused = { background = navSelectedTexture, textColor = Color.white }
        };
    }

    private static Texture2D CreateUiTexture(Color color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    // The IMGUI font has no emoji glyph, so the "Only GPU" flame is painted into a tiny texture
    // instead of pasting a character that would render as a missing-glyph box.
    private static Texture2D CreateFireIcon()
    {
        const int size = 24;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[size * size];
        var outer = new Color(1f, 0.32f, 0.04f, 1f);
        var inner = new Color(1f, 0.86f, 0.22f, 1f);
        var core = new Color(1f, 0.97f, 0.72f, 1f);
        for (int y = 0; y < size; y++)
        {
            float t = y / (float)(size - 1);
            float center = size * 0.5f + Mathf.Sin(t * Mathf.PI * 1.7f) * size * 0.15f * (1f - t);
            float halfWidth = size * 0.40f * Mathf.Pow(1f - t, 0.6f) * Mathf.SmoothStep(0f, 1f, t * 5f);
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Abs(x + 0.5f - center) / Mathf.Max(0.001f, halfWidth);
                if (distance >= 1f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                Color color = Color.Lerp(outer, inner, Mathf.Clamp01(1f - t));
                color = Color.Lerp(color, core, Mathf.Clamp01(1f - distance * 2.4f));
                color.a = Mathf.Clamp01((1f - distance) * 4f);
                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static void DestroyUiTexture(Texture2D texture)
    {
        if (texture != null)
            UnityEngine.Object.Destroy(texture);
    }
}
