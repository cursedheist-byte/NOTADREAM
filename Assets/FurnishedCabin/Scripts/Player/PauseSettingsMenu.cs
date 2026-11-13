using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed class PauseSettingsMenu : MonoBehaviour
{
    private enum MenuPage
    {
        Main,
        Video,
        Audio,
        Controls
    }

    [SerializeField] private SimpleFirstPersonController playerController;

    private MenuPage currentPage;
    private GraphicsPreset graphicsPreset = GraphicsPreset.High;
    private Resolution[] availableResolutions;
    private AudioSource[] soundSources;
    private float[] originalSourceVolumes;
    private int resolutionIndex;
    private Vector2 scrollPosition;

    private bool fullscreen;
    private bool showFps;
    private bool noProps;
    private int renderDistanceIndex;
    private float masterVolume = 1f;
    private float sfxVolume = 1f;
    private float previousTimeScale = 1f;
    private bool isOpen;
    private bool leaveRequested;
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

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<SimpleFirstPersonController>();

        graphicsPreset = GraphicsPresetController.Current;

        availableResolutions = Screen.resolutions;
        if (availableResolutions == null || availableResolutions.Length == 0)
            availableResolutions = new[] { Screen.currentResolution };

        resolutionIndex = FindCurrentResolution();
        fullscreen = Screen.fullScreen;
        showFps = FpsCounter.StoredValue;
        noProps = NoPropsMode.StoredValue;
        renderDistanceIndex = RenderDistanceSettings.StoredIndex;
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

        ApplyGraphicsPreset(graphicsPreset);
        CloseMenu();
    }

    private void Update()
    {
        if (leaveRequested)
        {
            leaveRequested = false;
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            return;
        }

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

    // The pause menu freezes time and disables the player, so both have to be restored before
    // the menu scene loads or the main menu would come up frozen with a dead cursor. The load
    // itself is deferred to Update because the button is clicked inside OnGUI.
    private void LeaveGame()
    {
        if (standaloneMenuHost)
        {
            CloseMenu();
            return;
        }

        PlayMenuClick();
        StopRebinding();
        isOpen = false;
        leaveRequested = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (playerController != null)
            playerController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
        bool roomySidebar = sidebar.height > 370f;
        bool showLeave = !standaloneMenuHost && roomySidebar;
        float footerSpace = roomySidebar ? 132f : sidebar.height > 310f ? 82f : 34f;
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
            Rect resumeRect = new Rect(navX, sidebar.yMax - (showLeave ? 132f : 82f), navItemWidth, showLeave ? 46f : 50f);
            if (GUI.Button(resumeRect, standaloneMenuHost ? "BACK TO MENU" : "RESUME GAME", menuButtonStyle))
                CloseMenu();

            if (showLeave)
            {
                Rect leaveRect = new Rect(navX, sidebar.yMax - 80f, navItemWidth, 46f);
                if (GUI.Button(leaveRect, "LEAVE GAME", menuButtonStyle))
                    LeaveGame();
            }
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

        if (!standaloneMenuHost)
        {
            GUILayout.Space(8f);
            if (GUILayout.Button("LEAVE GAME — RETURN TO MAIN MENU", menuButtonStyle, GUILayout.Height(52f)))
                LeaveGame();
            GUILayout.Space(6f);
            GUILayout.Label("Closes this session and takes you back to the main menu immediately.", labelStyle, GUILayout.Height(32f));
        }
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

        GUILayout.Space(8f);
        bool noPropsNow = GUILayout.Toggle(noProps, "  NO PROPS — low-end mode (hides all trees + grass)", GUILayout.Height(32f));
        if (noPropsNow != noProps)
        {
            noProps = noPropsNow;
            NoPropsMode.SetActive(noProps);
        }

        GUILayout.Label(
            "Removes every tree, fern and grass patch from the map and survives restarts. This is the biggest FPS gain available; switch it off to bring the vegetation back.",
            labelStyle, GUILayout.Height(46f));

        GUILayout.Space(16f);
        GUILayout.Label("RENDER DISTANCE", sectionStyle, GUILayout.ExpandWidth(true), GUILayout.Height(30f));
        GUILayout.Label("Scenery past this distance is not drawn. Lower it if the game stutters; raise it to see farther. Low presets cap it.", labelStyle, GUILayout.Height(46f));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", menuButtonStyle, GUILayout.Width(70f), GUILayout.Height(40f)))
            SetRenderDistanceIndex(renderDistanceIndex - 1);
        int chosenDistance = RenderDistanceSettings.Distances[renderDistanceIndex];
        float capped = Mathf.Min(chosenDistance, GraphicsPresetController.MaxRenderDistance);
        string distanceLabel = Mathf.Approximately(capped, chosenDistance)
            ? chosenDistance + " m"
            : chosenDistance + " m  (capped to " + capped + " m by preset)";
        GUILayout.Button(distanceLabel, menuButtonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(40f));
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
        GraphicsPresetController.Apply(preset);
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
        return GraphicsPresetController.DisplayName(preset);
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

    private static void DestroyUiTexture(Texture2D texture)
    {
        if (texture != null)
            UnityEngine.Object.Destroy(texture);
    }
}
