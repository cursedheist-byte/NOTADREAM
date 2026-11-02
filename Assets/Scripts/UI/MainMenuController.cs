using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    private struct UiRect
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 Position;
        public Vector2 Size;
        public Vector2 OffsetMin;
        public Vector2 OffsetMax;
    }

    [SerializeField] private Texture2D backgroundImage;
    [SerializeField] private AudioClip ambientClip;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private Camera menuCamera;

    private readonly string[] gameplayTips =
    {
        "Some answers are better left unopened.",
        "Listen carefully. The house is never truly quiet.",
        "Keep your phone close. Messages may matter.",
        "If something feels wrong, trust your instincts."
    };

    private Canvas menuCanvas;
    private CanvasGroup menuGroup;
    private CanvasGroup loadingGroup;
    private CanvasGroup fadeGroup;
    private Image progressFill;
    private TextMeshProUGUI progressLabel;
    private TextMeshProUGUI tipLabel;
    private TextMeshProUGUI loadingStatus;
    private AudioSource ambientSource;
    private AudioSource uiAudioSource;
    private AudioListener menuListener;
    private PauseSettingsMenu settingsMenu;
    private Scene menuScene;
    private TMP_FontAsset fontAsset;
    private bool transitionStarted;
    private int currentTip = -1;

    private void Start()
    {
        menuScene = gameObject.scene;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (menuCamera == null)
            menuCamera = Camera.main;
        if (menuCamera != null)
            menuListener = menuCamera.GetComponent<AudioListener>();

        BuildUi();
        BuildAudio();
        BuildSettingsHost();
    }

    private void BuildUi()
    {
        fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        EnsureEventSystem();

        var canvasObject = new GameObject("Main Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        menuCanvas = canvasObject.GetComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 100;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var background = CreateImage("Cinematic Background", canvasObject.transform, StretchRect(), new Color(1f, 1f, 1f, 1f));
        if (backgroundImage != null)
        {
            background.sprite = Sprite.Create(backgroundImage, new Rect(0f, 0f, backgroundImage.width, backgroundImage.height), new Vector2(0.5f, 0.5f));
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
        }

        // A light full-screen grade keeps the sunset readable without boxing off the menu.
        CreateImage("Full Screen Shade", canvasObject.transform, StretchRect(), new Color(0.005f, 0.008f, 0.014f, 0.18f));

        var menuObject = new GameObject("Menu Content", typeof(RectTransform), typeof(CanvasGroup));
        menuObject.transform.SetParent(canvasObject.transform, false);
        menuObject.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        menuObject.GetComponent<RectTransform>().anchorMax = Vector2.one;
        menuObject.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        menuObject.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        menuGroup = menuObject.GetComponent<CanvasGroup>();

        CreateImage("Title Accent", menuObject.transform,
            AnchoredRect(new Vector2(0.075f, 0.725f), new Vector2(0.075f, 0.725f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 68f)),
            new Color(1f, 0.30f, 0.16f, 1f));
        var title = CreateText("Game Title", menuObject.transform, "NOT A DREAM",
            new Vector2(0.095f, 0.725f), new Vector2(0f, 0.5f), new Vector2(820f, 92f),
            72f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        title.characterSpacing = 3f;

        CreateMenuButton(menuObject.transform, "PLAY", new Vector2(0.095f, 0.535f), OnPlayPressed);
        CreateMenuButton(menuObject.transform, "SETTINGS", new Vector2(0.095f, 0.445f), OnSettingsPressed);
        CreateMenuButton(menuObject.transform, "QUIT GAME", new Vector2(0.095f, 0.355f), OnQuitPressed);

        var loadingObject = new GameObject("Loading Screen", typeof(RectTransform), typeof(CanvasGroup));
        loadingObject.transform.SetParent(canvasObject.transform, false);
        var loadingRect = loadingObject.GetComponent<RectTransform>();
        loadingRect.anchorMin = Vector2.zero;
        loadingRect.anchorMax = Vector2.one;
        loadingRect.offsetMin = Vector2.zero;
        loadingRect.offsetMax = Vector2.zero;
        loadingGroup = loadingObject.GetComponent<CanvasGroup>();
        loadingGroup.alpha = 0f;
        loadingGroup.interactable = false;
        loadingGroup.blocksRaycasts = false;

        CreateImage("Loading Background", loadingObject.transform, StretchRect(), Color.white).sprite = background.sprite;
        CreateImage("Loading Shade", loadingObject.transform, StretchRect(), new Color(0.003f, 0.005f, 0.010f, 0.76f));
        CreateImage("Loading Accent", loadingObject.transform,
            AnchoredRect(new Vector2(0.09f, 0.245f), new Vector2(0.09f, 0.245f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 42f)),
            new Color(0.88f, 0.08f, 0.20f, 1f));
        CreateText("Loading Title", loadingObject.transform, "LOADING...",
            new Vector2(0.105f, 0.28f), new Vector2(0f, 0.5f), new Vector2(620f, 52f),
            28f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);

        var barBackground = CreateImage("Progress Track", loadingObject.transform,
            AnchoredRect(new Vector2(0.105f, 0.225f), new Vector2(0.63f, 0.225f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 5f)),
            new Color(0.82f, 0.86f, 0.90f, 0.18f));
        progressFill = CreateImage("Progress Fill", loadingObject.transform,
            AnchoredRect(new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero),
            new Color(0.90f, 0.10f, 0.23f, 1f));
        progressFill.transform.SetParent(barBackground.transform, false);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
        progressLabel = CreateText("Progress Percentage", loadingObject.transform, "0%",
            new Vector2(0.66f, 0.225f), new Vector2(0f, 0.5f), new Vector2(100f, 32f),
            18f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        tipLabel = CreateText("Gameplay Tip", loadingObject.transform, string.Empty,
            new Vector2(0.105f, 0.155f), new Vector2(0f, 0.5f), new Vector2(980f, 58f),
            17f, new Color(0.78f, 0.82f, 0.88f, 0.90f), FontStyles.Normal, TextAlignmentOptions.Left);
        loadingStatus = CreateText("Loading Status", loadingObject.transform, string.Empty,
            new Vector2(0.105f, 0.095f), new Vector2(0f, 0.5f), new Vector2(840f, 30f),
            14f, new Color(1f, 0.48f, 0.52f, 1f), FontStyles.Normal, TextAlignmentOptions.Left);

        var fadeObject = new GameObject("Transition Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        fadeObject.transform.SetParent(canvasObject.transform, false);
        fadeGroup = fadeObject.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        var fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;
        ApplyRect(StretchRect(), fadeObject.GetComponent<RectTransform>());
        fadeObject.transform.SetAsLastSibling();
    }

    private void BuildAudio()
    {
        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.clip = ambientClip;
        ambientSource.loop = true;
        ambientSource.playOnAwake = false;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = 0.12f;
        if (ambientClip != null)
            ambientSource.Play();

        uiAudioSource = gameObject.AddComponent<AudioSource>();
        uiAudioSource.playOnAwake = false;
        uiAudioSource.spatialBlend = 0f;
        uiAudioSource.volume = 0.55f;
    }

    private void BuildSettingsHost()
    {
        var host = new GameObject("Main Menu Settings Host");
        host.transform.SetParent(transform, false);
        host.SetActive(false);
        settingsMenu = host.AddComponent<PauseSettingsMenu>();
        settingsMenu.ConfigureForMainMenu();
        host.SetActive(true);
    }

    private void EnsureEventSystem()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("Main Menu EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        var uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (uiModule == null)
            uiModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        if (uiModule.actionsAsset == null)
            uiModule.AssignDefaultActions();
    }

    private void CreateMenuButton(Transform parent, string label, Vector2 anchor, UnityEngine.Events.UnityAction action)
    {
        var buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(360f, 58f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            if (uiAudioSource != null && clickClip != null)
                uiAudioSource.PlayOneShot(clickClip, 0.42f);
            action.Invoke();
        });

        var labelText = CreateText("Label", buttonObject.transform, label,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(310f, 40f),
            24f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
        labelText.rectTransform.anchoredPosition = new Vector2(10f, 0f);
        var arrow = CreateImage("Arrow", buttonObject.transform,
            AnchoredRect(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-25f, 0f), new Vector2(12f, 12f)),
            new Color(1f, 0.65f, 0.42f, 0.85f));
        var hover = buttonObject.AddComponent<MainMenuButtonHover>();
        hover.Configure(labelText, arrow);
    }

    private void OnPlayPressed()
    {
        if (!transitionStarted)
            StartCoroutine(LoadSceneWithScreen("Assets/Scenes/Scene1_Opening.unity"));
    }

    private void OnSettingsPressed()
    {
        if (settingsMenu == null)
            return;
        StartCoroutine(FadeGroup(menuGroup, 0f, 0.22f));
        menuGroup.interactable = false;
        menuGroup.blocksRaycasts = false;
        settingsMenu.OpenFromMainMenu(() => StartCoroutine(ReturnFromSettings()));
    }

    private IEnumerator ReturnFromSettings()
    {
        yield return FadeGroup(menuGroup, 1f, 0.22f);
        menuGroup.interactable = true;
        menuGroup.blocksRaycasts = true;
    }

    private void OnQuitPressed()
    {
        StartCoroutine(QuitAfterFade());
    }

    private IEnumerator QuitAfterFade()
    {
        yield return FadeGroup(fadeGroup, 1f, 0.35f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator LoadSceneWithScreen(string scenePath)
    {
        transitionStarted = true;
        Time.timeScale = 1f;
        yield return FadeGroup(menuGroup, 0f, 0.3f);
        menuGroup.interactable = false;
        menuGroup.blocksRaycasts = false;
        currentTip = -1;
        SetNextTip();
        loadingStatus.text = string.Empty;
        progressFill.fillAmount = 0f;
        progressLabel.text = "0%";
        yield return FadeGroup(loadingGroup, 1f, 0.35f);

        // Keep rendering the full-screen loading canvas until the gameplay camera has
        // finished loading. Disabling the menu camera here causes Unity's Game view to
        // briefly report "Display 1 / No camera rendering" during the transition.
        if (menuListener != null)
            menuListener.enabled = false;

        var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
        if (operation == null)
        {
            loadingStatus.text = "Unable to load the opening scene. Check the Build Settings scene list.";
            yield return FadeGroup(loadingGroup, 0f, 0.35f);
            if (menuListener != null) menuListener.enabled = true;
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
            yield return FadeGroup(menuGroup, 1f, 0.3f);
            transitionStarted = false;
            yield break;
        }

        float tipTimer = 0f;
        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            progressFill.fillAmount = progress;
            progressLabel.text = Mathf.RoundToInt(progress * 100f) + "%";
            tipTimer += Time.unscaledDeltaTime;
            if (tipTimer >= 5f)
            {
                tipTimer = 0f;
                SetNextTip();
            }
            yield return null;
        }

        progressFill.fillAmount = 1f;
        progressLabel.text = "100%";
        if (menuCamera != null)
            menuCamera.enabled = false;
        var gameplayScene = SceneManager.GetSceneByPath(scenePath);
        if (gameplayScene.IsValid() && gameplayScene.isLoaded)
            SceneManager.SetActiveScene(gameplayScene);

        yield return new WaitForSecondsRealtime(0.15f);
        yield return FadeGroup(loadingGroup, 0f, 0.65f);
        if (menuScene.IsValid() && menuScene.isLoaded)
            SceneManager.UnloadSceneAsync(menuScene);
    }

    private void SetNextTip()
    {
        if (tipLabel == null || gameplayTips.Length == 0)
            return;
        int next = Random.Range(0, gameplayTips.Length);
        if (gameplayTips.Length > 1 && next == currentTip)
            next = (next + 1) % gameplayTips.Length;
        currentTip = next;
        tipLabel.text = "TIP  /  " + gameplayTips[currentTip];
    }

    private IEnumerator FadeGroup(CanvasGroup group, float target, float duration)
    {
        if (group == null)
            yield break;
        float start = group.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            group.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }
        group.alpha = target;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string value, Vector2 anchor, Vector2 pivot, Vector2 size, float fontSize, Color color, FontStyles style, TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        var text = textObject.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null)
            text.font = fontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private Image CreateImage(string name, Transform parent, UiRect rect, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var imageRect = imageObject.GetComponent<RectTransform>();
        ApplyRect(rect, imageRect);
        var image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static UiRect StretchRect()
    {
        return new UiRect
        {
            AnchorMin = Vector2.zero,
            AnchorMax = Vector2.one,
            Pivot = new Vector2(0.5f, 0.5f),
            Position = Vector2.zero,
            Size = Vector2.zero,
            OffsetMin = Vector2.zero,
            OffsetMax = Vector2.zero
        };
    }

    private static UiRect AnchoredRect(Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        return new UiRect
        {
            AnchorMin = min,
            AnchorMax = max,
            Pivot = pivot,
            Position = position,
            Size = size,
            OffsetMin = Vector2.zero,
            OffsetMax = Vector2.zero
        };
    }

    private static void ApplyRect(UiRect source, RectTransform destination)
    {
        destination.anchorMin = source.AnchorMin;
        destination.anchorMax = source.AnchorMax;
        destination.pivot = source.Pivot;
        destination.anchoredPosition = source.Position;
        destination.sizeDelta = source.Size;
        destination.offsetMin = source.OffsetMin;
        destination.offsetMax = source.OffsetMax;
    }

    private void OnDestroy()
    {
        if (menuCanvas != null)
        {
            var images = menuCanvas.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var sprite = images[i].sprite;
                if (sprite != null && sprite.texture == backgroundImage)
                    Destroy(sprite);
            }
        }
    }
}

/// <summary>Small, smooth mouse hover treatment for runtime-built main menu buttons.</summary>
public sealed class MainMenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TextMeshProUGUI label;
    private Image arrow;
    private RectTransform rectTransform;
    private RectTransform arrowRect;
    private Vector3 restingScale;
    private Vector2 arrowRestingPosition;
    private float hover;
    private bool isHovered;

    public void Configure(TextMeshProUGUI buttonLabel, Image buttonArrow)
    {
        label = buttonLabel;
        arrow = buttonArrow;
    }

    private void Start()
    {
        rectTransform = transform as RectTransform;
        restingScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
        if (arrow != null)
        {
            arrowRect = arrow.rectTransform;
            arrowRestingPosition = arrowRect.anchoredPosition;
        }
    }

    private void Update()
    {
        hover = Mathf.MoveTowards(hover, isHovered ? 1f : 0f, Time.unscaledDeltaTime * 5.5f);
        if (rectTransform != null)
            rectTransform.localScale = Vector3.Lerp(restingScale, restingScale * 1.035f, Mathf.SmoothStep(0f, 1f, hover));
        if (label != null)
            label.color = Color.Lerp(Color.white, new Color(1f, 0.68f, 0.47f, 1f), hover);
        if (arrow != null)
            arrow.color = Color.Lerp(new Color(1f, 0.65f, 0.42f, 0.72f), new Color(1f, 0.36f, 0.20f, 1f), hover);
        if (arrowRect != null)
            arrowRect.anchoredPosition = Vector2.Lerp(arrowRestingPosition, arrowRestingPosition + Vector2.right * 8f, hover);
    }

    public void OnPointerEnter(PointerEventData eventData) => isHovered = true;
    public void OnPointerExit(PointerEventData eventData) => isHovered = false;
}
