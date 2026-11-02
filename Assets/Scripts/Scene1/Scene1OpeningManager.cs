using System.Collections;
using TMPro;

using UnityEngine.Video;
using UnityEngine;

public sealed class Scene1OpeningManager : MonoBehaviour
{
    public enum OpeningState
    {
        OpeningBlack,
        FadeIn,
        FirstDialogue,
        SecondDialogue,
        LookAtFred,
        TitleCard,
        SceneComplete
    }

    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera recordingCamera;
    [SerializeField] private Material nightSkyboxMaterial;
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private Scene1SubtitleController subtitles;
    [SerializeField] private Scene1TitleCardController titleCard;

    [SerializeField] private Light recordingFill;
    [SerializeField] private Light overheadLight;
    [SerializeField] private AudioSource roomAmbience;
    [SerializeField] private AudioSource electricalHum;
    [SerializeField] private AudioSource monitorNoise;
    [SerializeField] private AudioSource lightBuzz;
    [SerializeField] private AudioSource distantBuildingNoise;
    [SerializeField] private AudioSource airMovement;
    [SerializeField] private AudioSource subtitleCue;
    [SerializeField] private AudioSource titleCardCue;
    [SerializeField] private AudioSource characterPresence;
    [SerializeField] private Behaviour[] playerControls;
    [SerializeField] private bool playOnStart = true;

    private AudioSource suspiciousAmbienceSource;
    [SerializeField] private AudioClip suspiciousAmbienceClip;

    private Vector3 cameraStartPosition;
    private Quaternion wakeUpBaseCameraRotation;
    private GameObject wakeUpEyesCanvas;
    private RectTransform wakeUpTopLid;
    private RectTransform wakeUpBottomLid;
    private bool sequenceRunning;
    private Transform repeatPlayerRoot;
    private Transform repeatFred;
    private Transform repeatCameraTransform;
    private Vector3 repeatCameraPosition;
    private Quaternion repeatCameraRotation;
    private bool repeatCameraCaptured;
    private bool dreamWakeSequenceRunning;
    private Transform playerRootForScene2;
    private bool isRepeatSceneAdditivelyLoaded;
    private bool scene2TransitionPreloadComplete;
    private bool scene2TransitionPreloadFailed;
    private bool openingAmbienceAlreadyFaded;
    

    [SerializeField] private VideoClip liminalEndingVideo;
    [SerializeField] private GameObject[] liminalEnvironmentModels;
    [SerializeField] private Texture2D liminalSkyPanorama;
    [SerializeField] private Texture2D liminalGroundTexture;

    private Transform repeatCameraOriginalParent;
    private Vector3 repeatCameraOriginalLocalPosition;
    private Quaternion repeatCameraOriginalLocalRotation;
    private bool repeatCameraPoseCaptured;
    private Transform liminalEnvironmentRoot;
    private bool liminalEndingActive;
    private bool liminalDoorDialoguePlayed;
    private Material liminalGroundMaterial;
    private Material liminalDoorMaterial;
    private readonly System.Collections.Generic.List<Transform> liminalGroundModelRoots =
        new System.Collections.Generic.List<Transform>();

    // model_1 and model_2 share this mesh-space centre. The OBJ vertices keep absolute
    // coordinates, so any scale change has to pivot around it to stay on the terrain.
    private static readonly Vector3 LiminalDioramaCenter = new Vector3(-26.6348f, 12.3713f, 0f);
    private const float LiminalShellDiameter = 41.837f;
    private const float LiminalShellRadius = 17f;
    // Horizontal centre (u) of the cloud puff inside skyPS.png.
    private const float LiminalCloudCenterU = 0.501f;

    private Transform liminalShellModel;
    private Renderer[] liminalShellRenderers;
    private Transform liminalGateModel;
    private Bounds liminalShellBounds;
    private float liminalShellRadius;
    private Vector3 liminalGateLookPoint;
    private Vector3 liminalGateKeyPoint;
    private Vector3 liminalSpawnPoint;

    // The rim walls sit outside the shell so the gate, which stands on the rim, stays reachable.
    private const float LiminalBoundaryMargin = 6f;
    private const float LiminalDoorPromptRange = 6.5f;
    private const float ComaLookSensitivity = 0.12f;
    private const string ComaDoctorModelPath = "Assets/doctor/doctor_-_sketchfab_weekly_-_13_mar23.glb";

    private bool comaEndingStarted;
    private CanvasGroup liminalDoorPromptGroup;
    private RectTransform liminalDoorPromptTransform;
    private TextMeshProUGUI liminalDoorPromptText;
    private UnityEngine.Animations.AnimationClipPlayable comaDoctorIdle;
    private UnityEngine.Playables.PlayableGraph comaDoctorGraph;
    private bool comaDoctorIdleActive;
    private float comaDoctorIdleLength;
    private float comaDoctorIdleTime;
    private bool liminalRenderSettingsCaptured;
    private Material savedLiminalSkybox;
    private UnityEngine.Rendering.AmbientMode savedAmbientMode;
    private Color savedAmbientSkyColor;
    private Color savedAmbientEquatorColor;
    private Color savedAmbientGroundColor;
    private float savedAmbientIntensity;
    private bool savedFog;
    private Color savedFogColor;
    private FogMode savedFogMode;
    private float savedFogDensity;
    private float comaLookYaw;
    private float comaLookPitch;
private UnityEngine.AsyncOperation scene2TransitionLoadOperation;

    // Repeat-mode only (set in Awake via scene name): the replay skips the YESTERDAY title card
    // and runs the extended Fred confrontation instead.
    private bool isRepeatScene;

    public OpeningState State { get; private set; }

    private void InitializeOpeningCameraForOriginalScene()
    {
        if (playerCamera == null)
        {
            var cameraObject = GameObject.Find("PlayerCamera");
            if (cameraObject != null) playerCamera = cameraObject.GetComponent<Camera>();
        }
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera != null)
        {
            var fred = GameObject.Find("Fred");
            if (fred != null)
            {
                var lookTarget = fred.transform.position + Vector3.up * 1.25f;
                var lookRotation = Quaternion.LookRotation(lookTarget - playerCamera.transform.position, Vector3.up);
                playerCamera.transform.rotation = lookRotation * Quaternion.Euler(0f, -10f, 0f);
            }
        }
    }

    private void Awake()
    {
        isRepeatScene = gameObject.scene.path.Contains("Repeat");
        if (isRepeatScene && gameObject.scene.name != "Scene1_Opening_Repeat")
        {
            // A second (original-scene) copy of this manager is loaded alongside the repeat
            // scene. Only the manager actually living inside the repeat scene may drive it.
            enabled = false;
            return;
        }
        if (!isRepeatScene)
        {
            ResolveReferences();
            InitializeOpeningCameraForOriginalScene();
            repeatFred = null;
        }
        else
        {
            // Important: initialize when the additive repeat scene's Start() runs, after
            // Scene2 has loaded it. Don't guess a camera from the old Scene2 world in Awake.
            repeatFred = FindObjectInScene(gameObject.scene, "Fred");
        }
        if (isRepeatScene && (playerCamera == null || repeatFred == null))
        {
            Debug.LogError("Repeat scene is missing its own PlayerCamera or Fred object.", this);
        }
        if (isRepeatScene && playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        if (isRepeatScene && playerCamera != null)
        {
            if (!repeatCameraCaptured)
            {
                repeatCameraPosition = playerCamera.transform.position;
                repeatCameraRotation = playerCamera.transform.rotation;
                repeatCameraCaptured = true;
            }
        }
        if (playerCamera != null)
        {
            cameraStartPosition = playerCamera.transform.position;
        }

        if (isRepeatScene && playerRootForScene2 != null)
        {
            var pauseMenus = playerRootForScene2.GetComponentsInChildren<PauseSettingsMenu>(true);
            for (int i = 0; i < pauseMenus.Length; i++) pauseMenus[i].enabled = false;
        }

        if (!isRepeatScene && playerCamera != null)
        {
            var fred = GameObject.Find("Fred");
            if (fred != null)
            {
                var lookTarget = fred.transform.position + Vector3.up * 1.25f;
                var directLook = Quaternion.LookRotation(lookTarget - playerCamera.transform.position, Vector3.up);
                playerCamera.transform.rotation = directLook * Quaternion.Euler(0f, -10f, 0f);
            }
        }

        if (!isRepeatScene)
        {
            wakeUpBaseCameraRotation = playerCamera != null
                ? playerCamera.transform.rotation
                : Quaternion.identity;
            CreateWakeUpEyesOverlay();
        }

        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 1f;
        }
        else if (isRepeatScene)
        {
            fadeOverlay = CreateRepeatFadeOverlay();
        }

        if (subtitles != null)
        {
            subtitles.Clear();
        }

        if (titleCard != null)
        {
            titleCard.SetAlpha(0f);
        }

        if (recordingFill != null)
        {
            recordingFill.enabled = false;
        }

        if (recordingCamera != null)
        {
            recordingCamera.enabled = false;
        }

        // Repeat scene: night-time factory. The original Scene 1 evening look is replaced by
        // readable night lighting so Fred and the factory ceiling remain visible.
        if (isRepeatScene)
        {
            if (playerCamera == null)
            {
                Debug.LogError("Repeat scene initialization failed: no active gameplay camera.", this);
                return;
            }
            if (repeatFred != null && !repeatFred.gameObject.activeSelf)
            {
                repeatFred.gameObject.SetActive(true);
            }
            ApplyNightFactoryLighting();
            MakeFredVisible();
            SetRepeatSceneOverlayVisibility();
            SetRepeatSceneControls();
            SetPlayerControlsEnabled(false);
        }
        else
        {
            ApplyNightFactoryLighting();
            MakeOriginalFredHardToRecognize();
        }
    }

    private void CreateWakeUpEyesOverlay()
    {
        wakeUpEyesCanvas = new GameObject("Wake Up Eyes Overlay",
            typeof(RectTransform), typeof(UnityEngine.Canvas), typeof(UnityEngine.UI.CanvasScaler));
        wakeUpEyesCanvas.transform.SetParent(transform, false);
        var canvas = wakeUpEyesCanvas.GetComponent<UnityEngine.Canvas>();
        canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 600;

        var scaler = wakeUpEyesCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var top = CreateWakeUpLid("Upper Eyelid");
        wakeUpTopLid = top.GetComponent<RectTransform>();
        wakeUpTopLid.anchorMin = new Vector2(0f, 0.5f);
        wakeUpTopLid.anchorMax = Vector2.one;
        wakeUpTopLid.offsetMin = Vector2.zero;
        wakeUpTopLid.offsetMax = Vector2.zero;

        var bottom = CreateWakeUpLid("Lower Eyelid");
        wakeUpBottomLid = bottom.GetComponent<RectTransform>();
        wakeUpBottomLid.anchorMin = Vector2.zero;
        wakeUpBottomLid.anchorMax = new Vector2(1f, 0.5f);
        wakeUpBottomLid.offsetMin = Vector2.zero;
        wakeUpBottomLid.offsetMax = Vector2.zero;
    }

    private GameObject CreateWakeUpLid(string objectName)
    {
        var lid = new GameObject(objectName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        lid.transform.SetParent(wakeUpEyesCanvas.transform, false);
        lid.GetComponent<UnityEngine.UI.Image>().color = Color.black;
        lid.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        return lid;
    }

    private void SetWakeUpEyesOpen(float amount)
    {
        float open = Mathf.Clamp01(amount);
        if (wakeUpTopLid != null)
            wakeUpTopLid.anchorMin = new Vector2(0f, Mathf.Lerp(0.5f, 1f, open));
        if (wakeUpBottomLid != null)
            wakeUpBottomLid.anchorMax = new Vector2(1f, Mathf.Lerp(0.5f, 0f, open));
    }

    private void ApplyWakeUpDizziness(float strength)
    {
        if (playerCamera == null)
            return;

        float time = Time.unscaledTime;
        float pitch = Mathf.Sin(time * 1.55f) * 0.9f * strength;
        float yaw = Mathf.Sin(time * 1.05f + 0.7f) * 0.65f * strength;
        float roll = Mathf.Sin(time * 1.8f + 1.3f) * 2.0f * strength;
        playerCamera.transform.rotation = wakeUpBaseCameraRotation * Quaternion.Euler(pitch, yaw, roll);
    }

    private IEnumerator OpenWakeUpEyes(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetWakeUpEyesOpen(progress);
            ApplyWakeUpDizziness(1f - progress * 0.35f);
            yield return null;
        }

        SetWakeUpEyesOpen(1f);
    }

    private IEnumerator SettleWakeUpDizziness(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            ApplyWakeUpDizziness(0.65f * (1f - progress));
            yield return null;
        }

        if (playerCamera != null)
            playerCamera.transform.rotation = wakeUpBaseCameraRotation;
        if (wakeUpEyesCanvas != null)
            wakeUpEyesCanvas.SetActive(false);
    }

    // Keep the original opening's distant Fred as an indistinct night silhouette so the
    // player can notice a figure without getting a clear look at the character model.
    // This intentionally changes only renderer appearance; Fred and camera transforms stay authored.
    private void MakeOriginalFredHardToRecognize()
    {
        var fred = GameObject.Find("Fred");
        if (fred == null)
            return;

        var renderers = fred.GetComponentsInChildren<Renderer>(true);
        var darkTint = new Color(0.085f, 0.10f, 0.14f, 1f);
        for (int i = 0; i < renderers.Length; i++)
        {
            var renderer = renderers[i];
            if (renderer == null)
                continue;

            var materials = renderer.sharedMaterials;
            bool hasBaseColor = false;
            bool hasColor = false;
            bool hasEmission = false;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null)
                    continue;
                hasBaseColor |= material.HasProperty("_BaseColor");
                hasColor |= material.HasProperty("_Color");
                hasEmission |= material.HasProperty("_EmissionColor");
            }

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (hasBaseColor)
                block.SetColor("_BaseColor", darkTint);
            if (hasColor)
                block.SetColor("_Color", darkTint);
            if (hasEmission)
                block.SetColor("_EmissionColor", Color.black);
            renderer.SetPropertyBlock(block);
        }
    }

    // Night factory look: cool dim ambient, deep blue-black fog, a low industrial sun. The
    // Fred model keeps a separate fill light so his face stays readable against the dark.
    private void ApplyNightFactoryLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.020f, 0.028f, 0.055f);
        RenderSettings.ambientEquatorColor = new Color(0.028f, 0.038f, 0.070f);
        RenderSettings.ambientGroundColor = new Color(0.010f, 0.012f, 0.022f);
        RenderSettings.ambientIntensity = 0.42f;

        // Use the authored dark-blue night sky for the repeat copy (repeat is night-time).
        var nightSky = nightSkyboxMaterial;
#if UNITY_EDITOR
        if (nightSky == null)
            nightSky = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Night_DarkBlueSky.mat");
#endif
        if (nightSky != null)
        {
            RenderSettings.skybox = nightSky;
        }

        // Night haze for the repeat: cool deep blue, keeps the liminal night vibe without
        // turning the whole frame into an opaque dark band.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.05f, 0.075f, 0.14f);
        RenderSettings.fogDensity = 0.022f;

        var sceneLights = FindObjectsByType<Light>();
        for (int i = 0; i < sceneLights.Length; i++)
        {
            var light = sceneLights[i];
            if (light == null || light.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            if (light.type == LightType.Directional)
            {
                light.color = new Color(0.30f, 0.38f, 0.68f);
                light.intensity = 0.14f;
                RenderSettings.sun = light;
            }
            else if (light.type == LightType.Point || light.type == LightType.Spot)
            {
                light.color = new Color(0.72f, 0.80f, 1f);
                light.intensity = light.intensity <= 0f ? 0.75f : Mathf.Min(light.intensity, 1.1f);
                light.range = Mathf.Max(light.range, 12f);
            }
        }

        if (overheadLight != null)
        {
            overheadLight.color = new Color(0.68f, 0.76f, 1f);
            overheadLight.intensity = overheadLight.intensity <= 0f ? 0.95f : Mathf.Min(overheadLight.intensity, 1.15f);
            overheadLight.range = Mathf.Max(overheadLight.range, 14f);
        }

        DynamicGI.UpdateEnvironment();
    }

    private CanvasGroup CreateRepeatFadeOverlay()
    {
        var canvasObject = new GameObject("RepeatFadeOverlay");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 400;
        canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var panelObject = new GameObject("BlackPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        var rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = panelObject.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        var group = panelObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        return group;
    }

    private void SetRepeatSceneOverlayVisibility()
    {
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.alpha = 1f;
        }
    }

    private void PlaceRepeatSceneCamera()
    {
        if (playerCamera != null)
        {
            // Preserve the active player's actual world position and view; only retain its current
            // pose as the repeat camera start instead of teleporting it to a copied scene origin.
            repeatCameraPosition = playerCamera.transform.position;
            repeatCameraRotation = playerCamera.transform.rotation;
            repeatCameraCaptured = true;
            cameraStartPosition = repeatCameraPosition;
        }
    }

    private void SetRepeatSceneControls()
    {
        var controls = new System.Collections.Generic.List<Behaviour>();
        var worldPlayer = ResolveWorldPlayerRoot();
        var activePlayerRoot = worldPlayer != null ? worldPlayer : repeatPlayerRoot;
        if (activePlayerRoot != null)
        {
            var behaviours = activePlayerRoot.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i] != this &&
                    (behaviours[i].GetType().Name.Contains("FirstPersonController") ||
                     behaviours[i].GetType().Name.Contains("PauseSettingsMenu")))
                {
                    controls.Add(behaviours[i]);
                }
            }
        }
        var globalWorldControllers = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < globalWorldControllers.Length; i++)
        {
            var controller = globalWorldControllers[i];
            if (controller == null || !controller.GetType().Name.Contains("FirstPersonController") ||
                controls.Contains(controller))
            {
                continue;
            }

            var controllerPosition = controller.transform.position;
            if (controller.gameObject.scene != gameObject.scene ||
                Vector3.Distance(controllerPosition, repeatCameraPosition) > 100f)
            {
                controls.Add(controller);
            }
        }
        playerControls = controls.ToArray();
    }

    private void DisableOtherSceneCamerasAndListeners()
    {
        // The repeat scene is additive, so keep only the selected gameplay camera/listener.
    }

    // Use a gentle face fill light, leaving Fred's authored materials and appearance intact.
    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null) return;
        target.layer = layer;
        for (int childIndex = 0; childIndex < target.transform.childCount; childIndex++)
        {
            SetLayerRecursively(target.transform.GetChild(childIndex).gameObject, layer);
        }
    }

    private void MakeFredVisible()
    {
        var fred = isRepeatScene && repeatFred != null ? repeatFred.gameObject : GameObject.Find("Fred");
        if (fred == null || fred.transform.Find("RepeatFaceFill") != null)
        {
            return;
        }

        var fillObject = new GameObject("RepeatFaceFill");
        fillObject.transform.SetParent(fred.transform, false);
        fillObject.transform.localPosition = new Vector3(0f, 1.45f, -0.45f);
        var fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.color = new Color(0.86f, 0.88f, 1f);
        fill.intensity = 4f;
        fill.range = 5f;
        fill.cullingMask = 1 << fred.layer;
        var fredRenderers = fred.GetComponentsInChildren<Renderer>(true);
        var fallbackShader = Shader.Find("Universal Render Pipeline/Lit");
        if (fallbackShader == null) fallbackShader = Shader.Find("Standard");
        for (int i = 0; i < fredRenderers.Length; i++)
        {
            fredRenderers[i].enabled = true;
            if ((fredRenderers[i].sharedMaterial == null || fredRenderers[i].sharedMaterial.shader == null) && fallbackShader != null)
            {
                var fallbackMaterial = new Material(fallbackShader);
                fallbackMaterial.color = new Color(0.7f, 0.68f, 0.65f);
                fredRenderers[i].sharedMaterial = fallbackMaterial;
            }
        }
    }

    private void Start()
    {
        if (isRepeatScene)
        {
            InitializeRepeatSceneRuntime();
        }
        PlayLoop(roomAmbience);
        PlayLoop(electricalHum);
        PlayLoop(airMovement);
        PlayLoop(lightBuzz);
        PlayLoop(characterPresence);
        SetupSuspiciousAmbience();
        PlayLoop(suspiciousAmbienceSource);
        StartCoroutine(DistantNoiseLoop());

        if (playOnStart)
        {
            StartCoroutine(BeginOpeningAfterRepeatSetup());
        }

        if (overheadLight != null)
        {
            StartCoroutine(FlickerLoop());
        }
    }

    private IEnumerator DistantNoiseLoop()
    {
        while (State != OpeningState.SceneComplete)
        {
            yield return new WaitForSecondsRealtime(Random.Range(18f, 34f));
            if (State != OpeningState.SceneComplete && distantBuildingNoise != null && distantBuildingNoise.clip != null)
            {
                distantBuildingNoise.Play();
            }
        }
    }

    private void Update()
    {
        if (liminalEndingActive)
            UpdateLiminalEnding();
        if (sequenceRunning && isRepeatScene && playerCamera != null && repeatCameraCaptured && !dreamWakeSequenceRunning)
        {
            var breathing = Mathf.Sin(Time.unscaledTime * 0.72f) * 0.004f;
            var breathingPosition = repeatCameraPosition + Vector3.up * breathing;
            if (Vector3.Distance(playerCamera.transform.position, breathingPosition) > 0.08f)
            {
                repeatCameraPosition = playerCamera.transform.position;
            }
            else
            {
                playerCamera.transform.position = breathingPosition;
            }
        }
        else if (sequenceRunning && !isRepeatScene && playerCamera != null)
        {
            var breathing = Mathf.Sin(Time.unscaledTime * 0.72f) * 0.004f;
            playerCamera.transform.position = cameraStartPosition + Vector3.up * breathing;
        }
    }

    private IEnumerator BeginOpeningAfterRepeatSetup()
    {
        if (isRepeatScene && playerCamera == null)
        {
            InitializeRepeatSceneRuntime();
        }

        // The sequence is invoked by this scene's manager and the regular controller camera stays
        // active. Do not wait for a second camera/room-scene load while holding a black overlay.
        if (isRepeatScene && fadeOverlay != null)
        {
            fadeOverlay.alpha = 1f;
        }
        BeginOpening();
        yield break;
    }

    public void BeginOpening()
    {
        if (!sequenceRunning)
        {
            sequenceRunning = true;
            StartCoroutine(OpeningSequence());
        }
    }

    private void OnEnable()
    {
        if (!isActiveAndEnabled || !gameObject.scene.path.Contains("Repeat")) return;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleRepeatSceneLoaded;
        if (isRepeatScene)
        {
            InitializeRepeatSceneRuntime();
        }
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleRepeatSceneLoaded;
    }

    private void HandleRepeatSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (!isActiveAndEnabled || !gameObject.scene.path.Contains("Repeat"))
        {
            return;
        }

        if (scene.path.Contains("Repeat") && scene == gameObject.scene)
        {
            isRepeatScene = true;
            isRepeatSceneAdditivelyLoaded = true;
            playerCamera = null;
            repeatCameraTransform = null;
            repeatCameraCaptured = false;
            repeatFred = FindObjectInScene(scene, "Fred");
            InitializeRepeatSceneRuntime();
        }
    }

    private void InitializeRepeatSceneRuntime()
    {
        if (!isRepeatScene || playerCamera != null && repeatCameraCaptured)
        {
            return;
        }

        // Never hijack the repeat scene from an outside (original Scene1) manager copy,
        // and never keep a camera reference from a foreign scene.
        if (!isActiveAndEnabled || !gameObject.scene.path.Contains("Repeat"))
        {
            return;
        }

        // Keep the authored factory camera. The World_Master camera starts outside the factory.
        var repeatCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < repeatCameras.Length; i++)
        {
            if (repeatCameras[i] != null && repeatCameras[i].gameObject.scene == gameObject.scene &&
                repeatCameras[i].gameObject.name == "PlayerCamera")
            {
                playerCamera = repeatCameras[i];
                break;
            }
        }
        if (playerCamera != null && playerCamera.transform.root == playerCamera.transform &&
            playerCamera.CompareTag("MainCamera") && playerCamera.gameObject.scene.name == "World_Master")
        {
            // The master camera is intentional for this additive cinematic.
        } else if (playerCamera != null && playerCamera.gameObject.scene != gameObject.scene)
        {
            playerCamera = null;
            repeatCameraTransform = null;
            repeatCameraCaptured = false;
        }
        if (playerCamera == null)
        {
            ResolveReferences();
        }
        if (playerCamera == null)
        {
            var candidates = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != recordingCamera && candidates[i].enabled && candidates[i].tag == "MainCamera")
                {
                    playerCamera = candidates[i];
                    break;
                }
            }
        }
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        if (playerCamera == null)
        {
            Debug.LogError("Repeat scene could not resolve the active gameplay camera.", this);
            return;
        }

        repeatCameraTransform = playerCamera.transform;
        if (!repeatCameraPoseCaptured)
        {
            repeatCameraOriginalParent = playerCamera.transform.parent;
            repeatCameraOriginalLocalPosition = playerCamera.transform.localPosition;
            repeatCameraOriginalLocalRotation = playerCamera.transform.localRotation;
            repeatCameraPoseCaptured = true;
        }
        playerCamera.tag = "MainCamera";
        var factoryCamera = playerCamera;
        repeatPlayerRoot = ResolveWorldPlayerRoot();
        playerCamera = factoryCamera;
        if (repeatPlayerRoot == null) repeatPlayerRoot = playerCamera.transform.root;
        repeatCameraPosition = playerCamera.transform.position;
        repeatCameraRotation = playerCamera.transform.rotation;
        repeatCameraCaptured = true;

        if (repeatFred != null)
        {
            SetLayerRecursively(repeatFred.gameObject, playerCamera.gameObject.layer);
        }
        if (fadeOverlay == null)
        {
            fadeOverlay = CreateRepeatFadeOverlay();
        }
        if (subtitles == null)
        {
            var allSubtitles = FindObjectsByType<Scene1SubtitleController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allSubtitles.Length; i++)
            {
                subtitles = allSubtitles[i];
                if (allSubtitles[i].gameObject.scene == gameObject.scene) break;
            }
        }
        if (repeatFred != null)
        {
            repeatFred.gameObject.SetActive(true);
            MakeFredVisible();
        }

        // Render through the resolved gameplay camera only; duplicate cameras from additive
        // scenes otherwise layer the factory and dream-room geometry over one another.
        var allCams2 = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allCams2.Length; i++)
        {
            var c = allCams2[i];
            if (c == null) continue;
            if (c == playerCamera)
            {
                c.enabled = true;
                continue;
            }
            c.enabled = false;
        }
        var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var chosenListener = playerCamera.GetComponent<AudioListener>();
        if (chosenListener == null && listeners.Length > 0) chosenListener = listeners[0];
        if (chosenListener == null) chosenListener = playerCamera.gameObject.AddComponent<AudioListener>();
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] == null) continue;
            listeners[i].enabled = listeners[i] == chosenListener;
        }
        chosenListener.enabled = true;
        SetRepeatSceneControls();
        SetPlayerControlsEnabled(false);
        cameraStartPosition = playerCamera.transform.position;
    }

    public void SetPlayerControlsEnabled(bool enabled)
    {
        if (playerControls == null)
        {
            return;
        }

        foreach (var control in playerControls)
        {
            if (control != null && control != this)
            {
                control.enabled = enabled;
            }
        }
    }

    public bool DebugStartLiminalEndingTest()
    {
        if (!isRepeatScene || gameObject.scene.name != "Scene1_Opening_Repeat")
            return false;
        if (playerCamera == null || !repeatCameraCaptured)
            InitializeRepeatSceneRuntime();
        if (playerCamera == null || liminalEndingVideo == null || liminalEnvironmentModels == null || liminalEnvironmentModels.Length == 0)
            return false;

        StopAllCoroutines();
        var scene2Manager = FindFirstObjectByType<Scene2EveningManager>(FindObjectsInactive.Include);
        if (scene2Manager != null)
            scene2Manager.DebugPrepareForLiminalEndingTest();
        sequenceRunning = false;
        dreamWakeSequenceRunning = false;
        liminalEndingActive = false;
        State = OpeningState.SceneComplete;
        ClearSubtitle();
        SetPlayerControlsEnabled(false);
        StartCoroutine(PlayEndingVideoThenLiminalSequence());
        Debug.Log("[TEST] Skipped directly to the ending video and liminal scene.", this);
        return true;
    }
    private IEnumerator OpeningSequence()
    {
        State = OpeningState.OpeningBlack;
        yield return new WaitForSecondsRealtime(1.6f);

        State = OpeningState.FadeIn;
        if (isRepeatScene)
        {
            yield return Fade(fadeOverlay, 0f, 0.8f);
        }
        else
        {
            StartCoroutine(Fade(fadeOverlay, 0f, 1.1f));
            yield return StartCoroutine(OpenWakeUpEyes(3.8f));
        }
        if (isRepeatScene && playerCamera != null && repeatFred != null)
        {
            var initialLook = Quaternion.LookRotation(
                repeatFred.position + Vector3.up * 1.25f - playerCamera.transform.position,
                Vector3.up);
            yield return RotateCamera(playerCamera.transform.rotation, initialLook, 1.0f);
        }
        if (isRepeatScene)
            yield return new WaitForSecondsRealtime(1.7f);
        else
            yield return StartCoroutine(SettleWakeUpDizziness(1.7f));

        State = OpeningState.FirstDialogue;
        ShowSubtitle("Where am I?");
        yield return new WaitForSecondsRealtime(2.4f);
        yield return Fade(subtitles != null ? subtitles.Group : null, 0f, 0.35f);
        ClearSubtitle();
        yield return new WaitForSecondsRealtime(0.85f);

        State = OpeningState.SecondDialogue;
        ShowSubtitle("How did I end up like this?");
        yield return new WaitForSecondsRealtime(2.9f);
        yield return Fade(subtitles != null ? subtitles.Group : null, 0f, 0.35f);
        ClearSubtitle();

        // Look at Fred. In the repeat scene, continue into the requested confrontation instead
        // of fading to the YESTERDAY title card.
        State = OpeningState.LookAtFred;
        var fred = isRepeatScene && repeatFred != null ? repeatFred.gameObject : GameObject.Find("Fred");
        if (fred == null)
        {
            Debug.LogError("Scene1 opening cannot look at Fred because no Fred GameObject exists.", this);
        }
        else if (playerCamera != null)
        {
            var current = playerCamera.transform.rotation;
            var lookTarget = fred.transform.position + Vector3.up * 1.25f;
            var lookAtFred = Quaternion.LookRotation(lookTarget - playerCamera.transform.position, Vector3.up);
            yield return RotateCamera(current, lookAtFred, 1.8f);
        }

        if (isRepeatScene)
        {
            SetPlayerControlsEnabled(false);
            yield return StartCoroutine(RunRepeatConfrontation(fred));
            State = OpeningState.SceneComplete;
            sequenceRunning = false;
            yield break;
        }
        else
        {
            yield return new WaitForSecondsRealtime(1.2f);

            State = OpeningState.TitleCard;
            // The YESTERDAY card is meant to appear over black, as in the original cutscene.
            yield return Fade(fadeOverlay, 1f, 2f);
            yield return new WaitForSecondsRealtime(0.6f);
            // Start loading only after the fade is fully opaque. This prevents the
            // additive daytime world camera from flashing over the last Scene 1 shot.
            StartCoroutine(PrepareScene2Transition());

            if (titleCard != null)
            {
                titleCard.gameObject.SetActive(true);
                TriggerCue(titleCardCue, 0.22f);
                yield return Fade(titleCard.Group, 1f, 1.6f);
                yield return new WaitForSecondsRealtime(5.2f);
                while (!scene2TransitionPreloadComplete && !scene2TransitionPreloadFailed)
                    yield return null;
                yield return Fade(titleCard.Group, 0f, 0.35f);
            }
            else
            {
                while (!scene2TransitionPreloadComplete && !scene2TransitionPreloadFailed)
                    yield return null;
            }

            // The original opening ends on the YESTERDAY title card. Hold black for one
            // second while the opening ambience fades, then hand off to the prepared scenes.
            yield return Fade(fadeOverlay, 1f, 0.2f);
            yield return StartCoroutine(FadeOpeningAmbienceForSceneTransition(1f));
            openingAmbienceAlreadyFaded = true;
            if (scene2TransitionLoadOperation != null)
            {
                scene2TransitionLoadOperation.allowSceneActivation = true;
                while (!scene2TransitionLoadOperation.isDone)
                    yield return null;
            }
            yield return StartCoroutine(TransitionToScene2());
            yield break;
        }

        State = OpeningState.SceneComplete;
        SetPlayerControlsEnabled(true);
        sequenceRunning = false;
    }

    private IEnumerator PrepareScene2Transition()
    {
        const string scene2Path = "Assets/Scenes/Scene2_Evening.unity";
        const string worldMasterPath = "Assets/Scenes/World_Master.unity";

        var worldMaster = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(worldMasterPath);
        if (!worldMaster.IsValid() || !worldMaster.isLoaded)
        {
            var worldLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                worldMasterPath,
                UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (worldLoad == null)
            {
                scene2TransitionPreloadFailed = true;
                yield break;
            }

            while (!worldLoad.isDone)
                yield return null;
        }

        var scene2 = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene2Path);
        if (!scene2.IsValid() || !scene2.isLoaded)
        {
            scene2TransitionLoadOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                scene2Path,
                UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (scene2TransitionLoadOperation == null)
            {
                scene2TransitionPreloadFailed = true;
                yield break;
            }

            // Finish loading assets behind the title card, but hold activation until the
            // one-second blackout so Scene 2 gameplay and dialogue don't run early.
            scene2TransitionLoadOperation.allowSceneActivation = false;
            while (scene2TransitionLoadOperation.progress < 0.9f)
                yield return null;
        }

        scene2TransitionPreloadComplete = true;
    }

    private IEnumerator TransitionToScene2()
    {
        const string scene2Path = "Assets/Scenes/Scene2_Evening.unity";
        const string worldMasterPath = "Assets/Scenes/World_Master.unity";
        SetPlayerControlsEnabled(false);

        // Load the shared world before Scene 2 is instantiated. Scene2's Awake also
        // has a standalone fallback loader, but letting both bootstraps request the
        // same additive scene in the same frame can leave Scene 2 without a world.
        var worldMaster = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(worldMasterPath);
        if (!worldMaster.IsValid() || !worldMaster.isLoaded)
        {
            var worldLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                worldMasterPath,
                UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (worldLoad == null)
            {
                Debug.LogError("Scene 1 could not load required World_Master scene: " + worldMasterPath, this);
                yield return RecoverFromFailedScene2Transition();
                yield break;
            }

            while (!worldLoad.isDone)
                yield return null;

            worldMaster = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(worldMasterPath);
        }

        if (!worldMaster.IsValid() || !worldMaster.isLoaded)
        {
            Debug.LogError("World_Master load completed, but the scene is still unavailable: " + worldMasterPath, this);
            yield return RecoverFromFailedScene2Transition();
            yield break;
        }

        var scene2 = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene2Path);
        if (!scene2.IsValid() || !scene2.isLoaded)
        {
            var loadOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                scene2Path,
                UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (loadOperation == null)
            {
                Debug.LogError("Scene 1 could not load Scene2_Evening. Check the Build Settings scene list.", this);
                yield return RecoverFromFailedScene2Transition();
                yield break;
            }

            while (!loadOperation.isDone)
                yield return null;

            scene2 = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene2Path);
        }

        if (!scene2.IsValid() || !scene2.isLoaded)
        {
            Debug.LogError("Scene 1 finished loading, but Scene2_Evening is unavailable.", this);
            yield return RecoverFromFailedScene2Transition();
            yield break;
        }

        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene2);

        // Scene 2 can now initialize against the already loaded shared world.
        float deadline = Time.realtimeSinceStartup + 30f;
        worldMaster = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(worldMasterPath);
        while ((!worldMaster.IsValid() || !worldMaster.isLoaded) && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            worldMaster = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(worldMasterPath);
        }

        if (!worldMaster.IsValid() || !worldMaster.isLoaded)
        {
            Debug.LogError("Scene 2 did not finish loading its required World_Master scene.", this);
            yield return RecoverFromFailedScene2Transition();
            yield break;
        }

        // The opening ambience was faded during the one-second post-card blackout. Keep
        // the fallback fade for transitions that reach this method through another route.
        if (!openingAmbienceAlreadyFaded)
            yield return StartCoroutine(FadeOpeningAmbienceForSceneTransition(2.6f));

        // Let Scene2's Awake/Start setup finish using the loaded world player, then unload
        // the opening scene while the screen is still covered by its fade overlay.
        yield return null;
        yield return null;
        State = OpeningState.SceneComplete;
        sequenceRunning = false;
        var openingScene = gameObject.scene;
        if (openingScene.IsValid() && openingScene.isLoaded)
            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(openingScene);
    }

    private IEnumerator FadeOpeningAmbienceForSceneTransition(float duration)
    {
        AudioSource[] ambienceSources =
        {
            roomAmbience,
            electricalHum,
            monitorNoise,
            lightBuzz,
            distantBuildingNoise,
            airMovement,
            characterPresence,
            suspiciousAmbienceSource
        };

        float[] startingVolumes = new float[ambienceSources.Length];
        for (int i = 0; i < ambienceSources.Length; i++)
        {
            if (ambienceSources[i] != null)
                startingVolumes[i] = ambienceSources[i].volume;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration)));
            for (int i = 0; i < ambienceSources.Length; i++)
            {
                if (ambienceSources[i] != null)
                    ambienceSources[i].volume = Mathf.Lerp(startingVolumes[i], 0f, t);
            }
            yield return null;
        }

        for (int i = 0; i < ambienceSources.Length; i++)
        {
            if (ambienceSources[i] != null)
            {
                ambienceSources[i].volume = 0f;
                ambienceSources[i].Stop();
            }
        }
    }

    private IEnumerator RecoverFromFailedScene2Transition()
    {
        ShowSubtitle("Unable to load the next scene. Please check the game scene setup.");
        yield return Fade(fadeOverlay, 0f, 0.7f);
        SetPlayerControlsEnabled(true);
        State = OpeningState.SceneComplete;
        sequenceRunning = false;
    }

    private IEnumerator RunRepeatConfrontation(GameObject fred)
    {
        yield return new WaitForSecondsRealtime(0.7f);
        ShowSubtitle("Wait, Fred? Is that you?");
        yield return new WaitForSecondsRealtime(2.5f);
        ShowSubtitle("So you are the one behind all of this.");
        yield return new WaitForSecondsRealtime(2.7f);
        ShowSubtitle("You betrayed me.");
        yield return new WaitForSecondsRealtime(2.2f);

        ShowSubtitle("FRED: No, I am not the one who is behind all of this.");
        yield return new WaitForSecondsRealtime(3.2f);
        ShowSubtitle("?");
        yield return new WaitForSecondsRealtime(1.2f);

        var mother = SpawnMotherEnteringFromSide(fred);
        if (mother != null)
        {
            yield return MoveMotherIntoView(mother, fred);
            yield return AimCameraAt(mother.transform.position + Vector3.up * 1.45f, 0.7f);
            yield return new WaitForSecondsRealtime(0.35f);
        }

        if (mother != null && playerCamera != null)
        {
            var facePlayer = playerCamera.transform.position - mother.transform.position;
            facePlayer.y = 0f;
            if (facePlayer.sqrMagnitude > 0.001f)
            {
                mother.transform.rotation = Quaternion.LookRotation(facePlayer.normalized, Vector3.up);
            }
        }
        ShowSubtitle("MOM?");
        yield return new WaitForSecondsRealtime(1.5f);
        if (mother != null && playerCamera != null)
        {
            var towardPlayer = playerCamera.transform.position - mother.transform.position;
            towardPlayer.y = 0f;
            if (towardPlayer.sqrMagnitude > 0.001f)
            {
                mother.transform.rotation = Quaternion.LookRotation(towardPlayer.normalized, Vector3.up);
            }
        }
        ShowSubtitle("MOM: Yeah, it's me. Actually, you are not even my real child. You're adopted. You actually feel like a burden after your father's death.");
        yield return new WaitForSecondsRealtime(3.5f);
        yield return StartCoroutine(FadeOutSuspiciousAmbience(0.9f));
        yield return Fade(fadeOverlay, 1f, 1.2f);
        ClearSubtitle();
        SetPlayerControlsEnabled(false);
        yield return StartCoroutine(PlayBedroomDreamSequence());
        yield return StartCoroutine(PlayEndingVideoThenLiminalSequence());
        State = OpeningState.SceneComplete;
        sequenceRunning = false;
    }

    private IEnumerator PlayBedroomDreamSequence()
    {
        dreamWakeSequenceRunning = true;
        SetPlayerControlsEnabled(false);
        if (playerCamera == null)
        {
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].gameObject.scene == gameObject.scene &&
                    cameras[i].gameObject.name == "PlayerCamera")
                {
                    playerCamera = cameras[i];
                    break;
                }
            }
        }
        if (playerCamera == null)
        {
            Debug.LogError("Bedroom dream: active player camera not found.", this);
            yield return Fade(fadeOverlay, 0f, 0.8f);
            yield break;
        }

        // The factory camera is cinematic-only. Detach it, then place both it and the player
        // at the real MC_HOUSE bedroom while Scene 2 remains loaded additively.
        if (playerCamera.transform.parent != null)
        {
            playerCamera.transform.SetParent(null, true);
        }
        var scene2Manager = FindFirstObjectByType<Scene2EveningManager>(FindObjectsInactive.Include);
        var bed = scene2Manager != null ? scene2Manager.RepeatCutsceneBedroomBed : null;
        var bedroomDoor = scene2Manager != null ? scene2Manager.RepeatCutsceneBedroomDoor : null;
        var doorwayCenter = scene2Manager != null ? scene2Manager.RepeatCutsceneBedroomDoorwayCenter : Vector3.zero;
        var doorwayNormal = scene2Manager != null ? scene2Manager.RepeatCutsceneBedroomDoorwayNormal : Vector3.zero;
        if (bed == null || bedroomDoor == null)
        {
            Debug.LogError("Bedroom dream: the existing MC_HOUSE bed or bedroom door was not found.", this);
            yield return Fade(fadeOverlay, 0f, 0.8f);
            yield break;
        }

        if (playerCamera != null && bed != null)
        {
            var bedPosition = bed.position;
            var bedForward = bed.forward;
            bedForward.y = 0f;
            if (bedForward.sqrMagnitude < 0.001f) bedForward = Vector3.forward;
            bedForward.Normalize();

            // Use the existing bed and stay just above its mattress during the wake shot.
            var bedCameraPosition = bedPosition + Vector3.up * 0.95f - bedForward * 0.35f;
            var standingCameraPosition = bedPosition + Vector3.up * 1.55f - bedForward * 0.85f;
            var standingRotation = bedroomDoor != null
                ? Quaternion.LookRotation(bedroomDoor.position - standingCameraPosition, Vector3.up)
                : Quaternion.LookRotation(bedForward, Vector3.up);
            var startRotation = Quaternion.LookRotation(Vector3.up * -0.25f + bedForward * 0.97f, Vector3.up);

            if (repeatPlayerRoot != null)
            {
                var controllers = repeatPlayerRoot.GetComponentsInChildren<CharacterController>(true);
                for (int i = 0; i < controllers.Length; i++) controllers[i].enabled = false;
                repeatPlayerRoot.position = bedPosition + Vector3.up * 0.1f;
                for (int i = 0; i < controllers.Length; i++) controllers[i].enabled = true;
            }

            playerCamera.transform.SetPositionAndRotation(bedCameraPosition, startRotation);
            // The mom-dialogue blackout has covered the scene. Fade it OUT onto the bedroom shot
            // before the wake animation; previously it remained alpha=1 for the whole dream.
            yield return Fade(fadeOverlay, 0f, 0.8f);
            yield return new WaitForSecondsRealtime(0.5f);
            var elapsed = 0f;
            const float sitUpDuration = 1.8f;
            while (elapsed < sitUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / sitUpDuration));
                playerCamera.transform.position = Vector3.Lerp(bedCameraPosition, standingCameraPosition, t);
                playerCamera.transform.rotation = Quaternion.Slerp(startRotation, standingRotation, t);
                yield return null;
            }
            playerCamera.transform.SetPositionAndRotation(standingCameraPosition, standingRotation);
        }
        else
        {
            // Never fall through to an opaque-overlay-only dream if the bed camera setup failed.
            Debug.LogError("Bedroom dream: bed/root transition cannot be positioned.", this);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Fade(fadeOverlay, 0f, 0.8f);
            yield break;
        }

        yield return new WaitForSecondsRealtime(1f);
        ShowSubtitle("Is that a dream?");
        yield return new WaitForSecondsRealtime(2f);
        ClearSubtitle();
        yield return new WaitForSecondsRealtime(3f);

        scene2Manager.OpenBedroomForRepeatCutscene();
        yield return new WaitForSecondsRealtime(1.2f);
        var mother = SpawnBedroomMotherAtDoor(bedroomDoor, bed.position, doorwayCenter, doorwayNormal);
        if (mother != null)
        {
            yield return StartCoroutine(MotherEntersBedroom(mother.transform, bedroomDoor, bed.position, doorwayCenter, doorwayNormal));
            if (playerCamera != null)
                yield return AimCameraAt(mother.transform.position + Vector3.up * 1.45f, 0.7f);
        }

        ShowSubtitle("MOM: I am going to Aunt Ela's house.");
        yield return new WaitForSecondsRealtime(2.3f);
        ShowSubtitle("MOM: I am telling you because my phone is broken.");
        yield return new WaitForSecondsRealtime(2.8f);
        ShowSubtitle("MOM: So take care of your little brother.");
        yield return new WaitForSecondsRealtime(2.5f);
        ClearSubtitle();

        ShowSubtitle("A DREAM.");
        if (playerCamera != null)
        {
            // Let the player break eye contact with Mom and slowly look down as the line lands.
            var dreamLookPoint = mother != null
                ? mother.transform.position + Vector3.up * 0.12f
                : playerCamera.transform.position + playerCamera.transform.forward * 1.5f;
            yield return AimCameraAt(dreamLookPoint, 1.25f);
        }
        yield return new WaitForSecondsRealtime(1.5f);
        ClearSubtitle();
        yield return Fade(fadeOverlay, 1f, 0.8f);
        if (mother != null) Destroy(mother);

        var dreamCardObject = new GameObject("DreamTitleCardCanvas");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dreamCardObject, gameObject.scene);
        dreamCardObject.transform.SetParent(transform, false);
        var dreamCanvas = dreamCardObject.AddComponent<Canvas>();
        dreamCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dreamCanvas.overrideSorting = true;
        dreamCanvas.sortingOrder = 405;
        dreamCardObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        dreamCardObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var dreamTextObject = new GameObject("DreamTitleText");
        dreamTextObject.transform.SetParent(dreamCardObject.transform, false);
        var dreamRect = dreamTextObject.AddComponent<RectTransform>();
        dreamRect.anchorMin = Vector2.zero;
        dreamRect.anchorMax = Vector2.one;
        dreamRect.offsetMin = Vector2.zero;
        dreamRect.offsetMax = Vector2.zero;
        var dreamText = dreamTextObject.AddComponent<TextMeshProUGUI>();
        dreamText.text = "A DREAM";
        dreamText.fontSize = 48f;
        dreamText.fontStyle = FontStyles.Bold;
        dreamText.alignment = TextAlignmentOptions.Center;
        dreamText.color = Color.white;
        dreamText.raycastTarget = false;
        var dreamGroup = dreamTextObject.AddComponent<CanvasGroup>();
        dreamGroup.alpha = 0f;
        yield return Fade(dreamGroup, 1f, 0.5f);
        yield return new WaitForSecondsRealtime(2f);
        yield return Fade(dreamGroup, 0f, 0.5f);
        Destroy(dreamCardObject);
        dreamWakeSequenceRunning = false;
    }

    private Transform ResolveWorldPlayerRoot()
    {
        if (playerRootForScene2 != null && playerCamera != null) return playerRootForScene2;

        if (isRepeatSceneAdditivelyLoaded)
        {
            // Prefer the World_Master camera/player explicitly in additive cinematic mode.
            var loadedWorldPlayer = GameObject.Find("TestingPlayer");
            if (loadedWorldPlayer != null && loadedWorldPlayer.scene.name == "World_Master")
            {
                playerRootForScene2 = loadedWorldPlayer.transform;
                var cameras = playerRootForScene2.GetComponentsInChildren<Camera>(true);
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i].tag == "MainCamera" || cameras[i].gameObject.name == "PlayerCamera")
                    {
                        playerCamera = cameras[i];
                        return playerRootForScene2;
                    }
                }
            }
        }

        // Normal Scene 1 play: use the local scripted Player first. Additive repeat: bind the
        // existing World_Master first-person player/controller and its child camera.
        if (!isRepeatSceneAdditivelyLoaded)
        {
            var localPlayer = GameObject.Find("Player");
            if (localPlayer != null)
            {
                playerRootForScene2 = localPlayer.transform;
                if (playerCamera == null) playerCamera = FindCameraInHierarchy(playerRootForScene2);
                return playerRootForScene2;
            }
        }

        var allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            var candidate = allBehaviours[i];
            if (candidate == null || !candidate.GetType().Name.Contains("FirstPersonController")) continue;
            if (isRepeatSceneAdditivelyLoaded && candidate.gameObject.scene.name != "World_Master") continue;

            playerRootForScene2 = candidate.transform;
            if (playerCamera == null)
            {
                var cameras = candidate.GetComponentsInChildren<Camera>(true);
                for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
                {
                    if (cameras[cameraIndex].tag == "MainCamera" || cameras[cameraIndex].gameObject.name == "PlayerCamera")
                    {
                        playerCamera = cameras[cameraIndex];
                        break;
                    }
                }
            }
            return playerRootForScene2;
        }

        return playerCamera != null ? playerCamera.transform.root : repeatPlayerRoot;
    }

    private GameObject SpawnBedroomMotherAtDoor(Transform door, Vector3 bedPosition, Vector3 doorwayCenter, Vector3 doorwayNormal)
    {
        if (door == null) return null;

#if UNITY_EDITOR
        var motherAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/MOM/mom.glb");
        if (motherAsset == null) return null;
        var mother = Instantiate(motherAsset);
        mother.name = "BedroomMother";
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(mother, door.gameObject.scene);
        mother.transform.SetParent(null, true);
        mother.transform.localScale = Vector3.one;
        var momRenderers = mother.GetComponentsInChildren<Renderer>(true);
        if (momRenderers.Length > 0)
        {
            var momBounds = momRenderers[0].bounds;
            for (int i = 1; i < momRenderers.Length; i++) momBounds.Encapsulate(momRenderers[i].bounds);
            if (momBounds.size.y > 0.01f) mother.transform.localScale *= 2.25f / momBounds.size.y;
        }
        var towardBedroom = bedPosition - door.position;
        towardBedroom.y = 0f;
        if (towardBedroom.sqrMagnitude < 0.001f) towardBedroom = -door.forward;
        towardBedroom.Normalize();
        var threshold = doorwayCenter.sqrMagnitude > 0.001f ? doorwayCenter : door.position;
        threshold.y = door.position.y;
        var doorPlaneNormal = doorwayNormal.sqrMagnitude > 0.001f ? doorwayNormal : door.forward;
        doorPlaneNormal.y = 0f;
        if (Vector3.Dot(doorPlaneNormal, towardBedroom) < 0f) doorPlaneNormal = -doorPlaneNormal;
        var initialPosition = threshold - doorPlaneNormal * 1.25f;
        initialPosition.y = ResolveBedroomFloorHeight(initialPosition, bedPosition.y - 0.5f);
        mother.transform.position = initialPosition;
        var groundBounds = GetCombinedRendererBounds(mother.transform);
        mother.transform.position += Vector3.up * (initialPosition.y - groundBounds.min.y);
        var facePlayer = playerCamera != null ? playerCamera.transform.position - mother.transform.position : towardBedroom;
        facePlayer.y = 0f;
        if (facePlayer.sqrMagnitude < 0.001f) facePlayer = towardBedroom;
        mother.transform.rotation = Quaternion.LookRotation(facePlayer.normalized, Vector3.up);
        return mother;
#else
        return null;
#endif
    }

    private IEnumerator MotherEntersBedroom(Transform mother, Transform door, Vector3 bedPosition, Vector3 doorwayCenter, Vector3 doorwayNormal)
    {
        if (mother == null || door == null) yield break;
        var direction = bedPosition - door.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = -door.forward;
        direction.Normalize();
        var threshold = doorwayCenter.sqrMagnitude > 0.001f ? doorwayCenter : door.position;
        threshold.y = door.position.y;
        var inwardNormal = doorwayNormal.sqrMagnitude > 0.001f ? doorwayNormal : door.forward;
        inwardNormal.y = 0f;
        if (Vector3.Dot(inwardNormal, direction) < 0f) inwardNormal = -inwardNormal;

        // Cross the door plane through its center, then turn toward the bed after clearing the wall.
        var waypoints = new[]
        {
            threshold - inwardNormal * 1.25f,
            threshold + inwardNormal * 1.35f,
            threshold + inwardNormal * 1.35f + direction * 0.8f
        };
        float rootHeightAboveFloor = mother.position.y - ResolveBedroomFloorHeight(waypoints[0], bedPosition.y - 0.5f);
        var lookPoint = playerCamera != null ? playerCamera.transform.position : bedPosition;
        float totalLength = Vector3.Distance(waypoints[0], waypoints[1]) + Vector3.Distance(waypoints[1], waypoints[2]);
        const float totalDuration = 2.4f;
        var previous = waypoints[0];
        for (int waypointIndex = 1; waypointIndex < waypoints.Length && mother != null; waypointIndex++)
        {
            var destination = waypoints[waypointIndex];
            float segmentLength = Vector3.Distance(previous, destination);
            float segmentDuration = totalDuration * segmentLength / totalLength;
            float segmentElapsed = 0f;
            float startFloor = ResolveBedroomFloorHeight(previous, bedPosition.y - 0.5f);
            float endFloor = ResolveBedroomFloorHeight(destination, bedPosition.y - 0.5f);
            while (segmentElapsed < segmentDuration && mother != null)
            {
                segmentElapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(segmentElapsed / segmentDuration));
                var position = Vector3.Lerp(previous, destination, t);
                position.y = Mathf.Lerp(startFloor, endFloor, t) + rootHeightAboveFloor;
                var facing = lookPoint - position;
                facing.y = 0f;
                mother.SetPositionAndRotation(position, Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing : direction, Vector3.up));
                yield return null;
            }
            previous = destination;
        }
    }

    private static float ResolveBedroomFloorHeight(Vector3 position, float fallbackHeight)
    {
        // Start below the MC_HOUSE roof so the ray finds the bedroom floor, not the roof mesh.
        var origin = new Vector3(position.x, Mathf.Max(position.y + 2.5f, fallbackHeight + 2.5f), position.z);
        if (Physics.Raycast(origin, Vector3.down, out var hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            hit.normal.y > 0.45f)
        {
            return hit.point.y;
        }
        return fallbackHeight;
    }

    private IEnumerator ShowEndCredits()
    {
        if (fadeOverlay == null)
        {
            yield break;
        }

        fadeOverlay.alpha = 1f;
        var creditsCanvas = new GameObject("EndCreditsCanvas");
        creditsCanvas.transform.SetParent(transform, false);
        var canvas = creditsCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 410;
        creditsCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        creditsCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var creditsObject = new GameObject("EndCreditsText");
        creditsObject.transform.SetParent(creditsCanvas.transform, false);
        var rect = creditsObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var credits = creditsObject.AddComponent<TextMeshProUGUI>();
        credits.text = "<size=34>Developed By:</size>\n<size=48><b>NYXO (Varun)</b></size>";
        credits.alignment = TextAlignmentOptions.Center;
        credits.color = Color.white;
        credits.fontSize = 40f;
        credits.raycastTarget = false;

        var creditsGroup = creditsObject.AddComponent<CanvasGroup>();
        creditsGroup.alpha = 0f;
        yield return Fade(creditsGroup, 1f, 0.8f);
        yield return new WaitForSecondsRealtime(5f);
        yield return Fade(creditsGroup, 0f, 1.2f);
        Destroy(creditsCanvas);
    }

    
    private IEnumerator PlayEndingVideoThenLiminalSequence()
    {
        SetPlayerControlsEnabled(false);
        ClearSubtitle();
        if (fadeOverlay != null)
            yield return Fade(fadeOverlay, 1f, 0.45f);

        GameObject videoCanvasObject = null;
        VideoPlayer endingPlayer = null;
        RenderTexture videoTarget = null;
        if (liminalEndingVideo == null)
        {
            Debug.LogError("The ending video is not assigned on Scene1OpeningManager.", this);
        }
        else
        {
            videoTarget = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
            {
                name = "LiminalEndingVideoTarget",
                useMipMap = false,
                autoGenerateMips = false
            };
            videoTarget.Create();

            videoCanvasObject = new GameObject("LiminalEndingVideoCanvas");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(videoCanvasObject, gameObject.scene);
            var canvas = videoCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 390;
            var scaler = videoCanvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var videoImageObject = new GameObject("EndingVideoImage", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            videoImageObject.transform.SetParent(videoCanvasObject.transform, false);
            var videoRect = videoImageObject.GetComponent<RectTransform>();
            videoRect.anchorMin = Vector2.zero;
            videoRect.anchorMax = Vector2.one;
            videoRect.offsetMin = Vector2.zero;
            videoRect.offsetMax = Vector2.zero;
            var rawImage = videoImageObject.GetComponent<UnityEngine.UI.RawImage>();
            rawImage.texture = videoTarget;
            rawImage.raycastTarget = false;

            var videoPlayerObject = new GameObject("LiminalEndingVideoPlayer");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(videoPlayerObject, gameObject.scene);
            endingPlayer = videoPlayerObject.AddComponent<VideoPlayer>();
            endingPlayer.playOnAwake = false;
            endingPlayer.isLooping = false;
            endingPlayer.clip = liminalEndingVideo;
            endingPlayer.renderMode = VideoRenderMode.RenderTexture;
            endingPlayer.targetTexture = videoTarget;
            endingPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            endingPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            endingPlayer.EnableAudioTrack(0, true);
            endingPlayer.SetDirectAudioVolume(0, 1f);
            endingPlayer.Prepare();

            float prepareElapsed = 0f;
            while (!endingPlayer.isPrepared && prepareElapsed < 15f)
            {
                prepareElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (endingPlayer.isPrepared)
            {
                // Start the video while the screen is still fully black. Revealing the overlay
                // before the first decoded frame reaches the RenderTexture exposes the world
                // for a fraction of a second between the "A DREAM" card and the video.
                endingPlayer.Play();
                float firstFrameElapsed = 0f;
                while (endingPlayer.frame < 1 && firstFrameElapsed < 1f)
                {
                    firstFrameElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                // One more frame so the RawImage has been redrawn with that frame before the fade.
                yield return null;
                yield return Fade(fadeOverlay, 0f, 0.55f);
                float playbackElapsed = 0f;
                float playbackLimit = Mathf.Max(12f, (float)liminalEndingVideo.length + 5f);
                while (endingPlayer.isPlaying && playbackElapsed < playbackLimit)
                {
                    playbackElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                endingPlayer.Stop();
            }
            else
            {
                Debug.LogError("The ending video did not prepare; continuing to the liminal scene instead of holding on black.", this);
            }

            yield return Fade(fadeOverlay, 1f, 0.65f);
        }

        if (endingPlayer != null)
            Destroy(endingPlayer.gameObject);
        if (videoCanvasObject != null)
            Destroy(videoCanvasObject);
        if (videoTarget != null)
        {
            videoTarget.Release();
            Destroy(videoTarget);
        }

        BuildLiminalEndingEnvironment();
        if (liminalEnvironmentRoot == null)
        {
            Debug.LogError("Liminal ending environment could not be built; keeping the player in the existing scene.", this);
            yield return Fade(fadeOverlay, 0f, 0.6f);
            SetPlayerControlsEnabled(true);
            sequenceRunning = false;
            State = OpeningState.SceneComplete;
            yield break;
        }

        TeleportPlayerToLiminalRoadEnd();
        yield return null;
        yield return Fade(fadeOverlay, 0f, 1f);
        yield return new WaitForSecondsRealtime(0.5f);

        liminalEndingActive = true;
        liminalDoorDialoguePlayed = false;
        ShowSubtitle("What's that?");
        yield return new WaitForSecondsRealtime(2.2f);
        ClearSubtitle();

        var scene2Manager = FindFirstObjectByType<Scene2EveningManager>(FindObjectsInactive.Include);
        if (scene2Manager != null)
            scene2Manager.SetLiminalEndingObjective("Go to the door");

        sequenceRunning = false;
        State = OpeningState.SceneComplete;
        SetPlayerControlsEnabled(true);
    }

    private void BuildLiminalEndingEnvironment()
    {
        if (liminalEnvironmentRoot != null)
            return;
        if (liminalEnvironmentModels == null || liminalEnvironmentModels.Length == 0)
        {
            Debug.LogError("No liminal environment models are assigned.", this);
            return;
        }

        // Keep the authored liminal scene on its own isolated patch of space. The player is
        // teleported there only after the video, so no road or terrain joins it to the game map.
        var environment = new GameObject("LiminalEndingEnvironment");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(environment, gameObject.scene);
        environment.transform.position = new Vector3(4096f, 0f, 4096f);
        environment.SetActive(false);
        liminalEnvironmentRoot = environment.transform;
        liminalGroundModelRoots.Clear();
        liminalShellModel = null;
        liminalShellRenderers = null;
        liminalGateModel = null;
        liminalShellRadius = 0f;
        CaptureLiminalRenderSettings();
        CreateLiminalGroundMaterial();
        CreateLiminalDoorMaterial();
        CreateLiminalGroundPlane(environment.transform);

        int instantiatedModelCount = 0;
        for (int i = 0; i < liminalEnvironmentModels.Length; i++)
        {
            var modelAsset = liminalEnvironmentModels[i];
            if (modelAsset == null)
                continue;

            var model = Instantiate(modelAsset, liminalEnvironmentRoot, false);
            instantiatedModelCount++;
            model.name = modelAsset.name;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            // model_1 is the shell around the diorama. Its faces point outward, so it reads as
            // the sphere seen from outside; shrink it to the terrain footprint so it hugs the
            // hard edge of the ground instead of looming past it. Scaling around the root
            // would also slide the shell off the terrain, so undo that offset.
            if (model.name == "model_1")
            {
                const float shellScale = LiminalShellRadius * 2f / LiminalShellDiameter;
                model.transform.localScale = Vector3.one * shellScale;
                model.transform.localPosition = new Vector3(
                    LiminalDioramaCenter.x * (1f - shellScale),
                    0f,
                    LiminalDioramaCenter.z * (1f - shellScale));
                liminalShellModel = model.transform;
                liminalShellRenderers = model.GetComponentsInChildren<Renderer>(true);
                continue;
            }

            // model_2 through model_4 are the mapped terrain/path meshes. Their source
            // texture contains both the mossy hill and winding dirt path; without it Unity
            // renders the imported OBJ surfaces as a featureless brown plane.
            bool isGroundModel = model.name == "model_2" || model.name == "model_3" || model.name == "model_4";
            bool isDoorModel = model.name == "model_0";
            if (isGroundModel)
            {
                liminalGroundModelRoots.Add(model.transform);
                ApplyLiminalGroundMaterial(model);
            }
            else if (isDoorModel)
            {
                ApplyLiminalDoorMaterial(model);
                liminalGateModel = model.transform;
            }

            var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
            for (int meshIndex = 0; meshIndex < meshFilters.Length; meshIndex++)
            {
                var meshFilter = meshFilters[meshIndex];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;
                if ((isGroundModel || isDoorModel) && meshFilter.GetComponent<MeshCollider>() == null)
                {
                    var meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
                }
            }
        }

        if (instantiatedModelCount == 0)
        {
            Destroy(environment);
            liminalEnvironmentRoot = null;
            Debug.LogError("No liminal environment model references resolved.", this);
            return;
        }

        // Renderer bounds are only trustworthy once the set is active, and the spawn, the rim
        // walls and the sky rotation are all derived from them.
        liminalEnvironmentRoot.gameObject.SetActive(true);
        Physics.SyncTransforms();
        ComputeLiminalPlacement(environment.transform);
        CreateLiminalBoundary(environment.transform);
        Physics.SyncTransforms();

        if (liminalSkyPanorama != null)
        {
            var skyboxShader = Shader.Find("Liminal/SkyBackdrop");
            if (skyboxShader == null)
                skyboxShader = Shader.Find("Skybox/Panoramic");
            if (skyboxShader != null)
            {
                var liminalSky = new Material(skyboxShader)
                {
                    name = "LiminalEndingSky"
                };
                liminalSky.SetTexture("_MainTex", liminalSkyPanorama);
                // The cloud sits low in the source image. Anchor the image bottom to the
                // horizon and turn it so the cloud rises directly behind the door as seen
                // from the spawn point (the sky is world-locked, not camera-relative).
                Vector3 spawnToGate = liminalGateLookPoint - liminalSpawnPoint;
                float gateAzimuthU = Mathf.Atan2(spawnToGate.z, spawnToGate.x) / (2f * Mathf.PI) + 0.5f;
                liminalSky.SetFloat("_Rotation", (LiminalCloudCenterU - gateAzimuthU) * 360f);
                if (liminalSky.HasProperty("_VerticalPower"))
                    liminalSky.SetFloat("_VerticalPower", 1f);
                if (liminalSky.HasProperty("_Tint"))
                    liminalSky.SetColor("_Tint", Color.white);
                if (liminalSky.HasProperty("_Exposure"))
                    liminalSky.SetFloat("_Exposure", 1f);
                if (liminalSky.HasProperty("_Mapping"))
                    liminalSky.SetFloat("_Mapping", 6f);
                RenderSettings.skybox = liminalSky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.18f, 0.22f, 0.30f);
                RenderSettings.ambientEquatorColor = new Color(0.12f, 0.14f, 0.17f);
                RenderSettings.ambientGroundColor = new Color(0.035f, 0.04f, 0.045f);
                RenderSettings.ambientIntensity = 0.8f;
                DynamicGI.UpdateEnvironment();
            }
        }

        ApplyLiminalAtmosphere();
    }

    // The repeat scene keeps the factory's warm orange fog, which belongs to the night intro.
    // The liminal set reads as a clear blue-sky diorama, so the haze is switched off entirely.
    private void ApplyLiminalAtmosphere()
    {
        RenderSettings.fog = false;
    }

    // The ending cuts back to the authored house, so the night sky/ambient that the liminal
    // set overwrites has to be put back before the bedroom shot.
    private void CaptureLiminalRenderSettings()
    {
        if (liminalRenderSettingsCaptured)
            return;

        savedLiminalSkybox = RenderSettings.skybox;
        savedAmbientMode = RenderSettings.ambientMode;
        savedAmbientSkyColor = RenderSettings.ambientSkyColor;
        savedAmbientEquatorColor = RenderSettings.ambientEquatorColor;
        savedAmbientGroundColor = RenderSettings.ambientGroundColor;
        savedAmbientIntensity = RenderSettings.ambientIntensity;
        savedFog = RenderSettings.fog;
        savedFogColor = RenderSettings.fogColor;
        savedFogMode = RenderSettings.fogMode;
        savedFogDensity = RenderSettings.fogDensity;
        liminalRenderSettingsCaptured = true;
    }

    private void RestoreLiminalRenderSettings()
    {
        if (!liminalRenderSettingsCaptured)
            return;

        RenderSettings.skybox = savedLiminalSkybox;
        RenderSettings.ambientMode = savedAmbientMode;
        RenderSettings.ambientSkyColor = savedAmbientSkyColor;
        RenderSettings.ambientEquatorColor = savedAmbientEquatorColor;
        RenderSettings.ambientGroundColor = savedAmbientGroundColor;
        RenderSettings.ambientIntensity = savedAmbientIntensity;
        RenderSettings.fogColor = savedFogColor;
        RenderSettings.fogMode = savedFogMode;
        RenderSettings.fogDensity = savedFogDensity;
        RenderSettings.fog = savedFog;
        DynamicGI.UpdateEnvironment();
    }

    // A flat skirt under the imported terrain so the diorama does not end in mid air; it fades
    // into the haze before the horizon.
    private void CreateLiminalGroundPlane(Transform parent)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "LiminalGroundPlane";
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(plane, gameObject.scene);
        plane.transform.SetParent(parent, false);
        plane.transform.localPosition = new Vector3(0f, 2.0f, 0f);
        plane.transform.localScale = new Vector3(200f, 1f, 200f);

        var material = new Material(shader) { name = "Liminal Ground Skirt (Runtime)" };
        Color groundColor = new Color(0.075f, 0.105f, 0.08f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", groundColor);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", groundColor);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.08f);
        plane.GetComponent<Renderer>().sharedMaterial = material;
    }

    // Positions come from the imported meshes instead of hard-coded offsets: the shell bounds
    // give the bubble centre and radius, the door bounds give the look target. The spawn sits
    // on the dirt path at the rim opposite the door, always inside the shell.
    private void ComputeLiminalPlacement(Transform environment)
    {
        Vector3 environmentOrigin = environment.position;

        if (liminalShellModel != null)
        {
            liminalShellBounds = GetCombinedRendererBounds(liminalShellModel);
            liminalShellRadius = Mathf.Min(liminalShellBounds.extents.x, liminalShellBounds.extents.z);
        }
        if (liminalShellRadius < 1f)
        {
            liminalShellBounds = new Bounds(
                environmentOrigin + LiminalDioramaCenter,
                new Vector3(LiminalShellRadius * 2f, 1f, LiminalShellRadius * 2f));
            liminalShellRadius = LiminalShellRadius;
        }

        if (liminalGateModel != null)
        {
            var gateBounds = GetCombinedRendererBounds(liminalGateModel);
            liminalGateLookPoint = gateBounds.center;
            // The gateway opening is the clear span between the low base and the arch crown,
            // so the key hint floats just under the crown instead of above the whole structure.
            liminalGateKeyPoint = new Vector3(
                gateBounds.center.x,
                gateBounds.min.y + gateBounds.size.y * 0.45f,
                gateBounds.center.z);
        }
        else
        {
            liminalGateLookPoint = environmentOrigin + new Vector3(-30.6f, 7.08f, -17.55f);
            liminalGateKeyPoint = liminalGateLookPoint + Vector3.down * 0.35f;
        }

        // The dirt path runs along the door axis; step back from the shell centre so the
        // player starts at the far rim with the gate ahead.
        Vector3 center = liminalShellBounds.center;
        liminalSpawnPoint = new Vector3(
            center.x - 0.6f,
            environmentOrigin.y + 2.6f,
            center.z + liminalShellRadius * 0.73f);
    }

    // The shell is the visual edge of the diorama, but its rim curves down into the terrain,
    // so its own faces cannot stop a walker. Ring the area with tall invisible walls, set back
    // from the rim so the gate standing on the edge can still be reached.
    private void CreateLiminalBoundary(Transform parent)
    {
        const int segments = 24;
        const float wallHeight = 14f;
        const float wallThickness = 0.6f;
        float radius = Mathf.Max(liminalShellRadius, 1f) + LiminalBoundaryMargin;
        // Each panel is wider than its arc so neighbouring corners always overlap.
        float panelWidth = Mathf.PI * 2f * radius / segments * 1.2f;

        var ring = new GameObject("LiminalBoundary");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ring, gameObject.scene);
        ring.transform.SetParent(parent, false);
        ring.transform.position = new Vector3(
            liminalShellBounds.center.x,
            parent.position.y,
            liminalShellBounds.center.z);

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            var panel = new GameObject("BoundaryWall");
            panel.transform.SetParent(ring.transform, false);
            panel.transform.localPosition = outward * radius;
            panel.transform.localRotation = Quaternion.LookRotation(outward, Vector3.up);

            var wall = panel.AddComponent<BoxCollider>();
            wall.size = new Vector3(panelWidth, wallHeight, wallThickness);
            wall.center = new Vector3(0f, wallHeight * 0.5f, 0f);
        }
    }

    private void CreateLiminalDoorMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("No supported shader was found for the liminal door.", this);
            return;
        }

        liminalDoorMaterial = new Material(shader) { name = "Liminal Door Black (Runtime)" };
        Color doorColor = new Color(0.014f, 0.015f, 0.017f);
        if (liminalDoorMaterial.HasProperty("_BaseColor"))
        {
            liminalDoorMaterial.SetColor("_BaseColor", doorColor);
            if (liminalDoorMaterial.HasProperty("_Smoothness"))
                liminalDoorMaterial.SetFloat("_Smoothness", 0.22f);
            if (liminalDoorMaterial.HasProperty("_Metallic"))
                liminalDoorMaterial.SetFloat("_Metallic", 0f);
        }
        else if (liminalDoorMaterial.HasProperty("_Color"))
        {
            liminalDoorMaterial.SetColor("_Color", doorColor);
        }
    }

    private void ApplyLiminalDoorMaterial(GameObject model)
    {
        if (liminalDoorMaterial == null || model == null) return;
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].sharedMaterial = liminalDoorMaterial;
        }
    }

    private void CreateLiminalGroundMaterial()
    {
        if (liminalGroundTexture == null)
        {
            Debug.LogError("The liminal ground/path texture is not assigned.", this);
            return;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("No supported shader was found for the liminal ground.", this);
            return;
        }

        liminalGroundMaterial = new Material(shader)
        {
            name = "Liminal Moss and Path (Runtime)"
        };
        if (liminalGroundMaterial.HasProperty("_BaseMap"))
        {
            liminalGroundMaterial.SetTexture("_BaseMap", liminalGroundTexture);
            if (liminalGroundMaterial.HasProperty("_BaseColor"))
                liminalGroundMaterial.SetColor("_BaseColor", Color.white);
            if (liminalGroundMaterial.HasProperty("_Smoothness"))
                liminalGroundMaterial.SetFloat("_Smoothness", 0.12f);
        }
        else if (liminalGroundMaterial.HasProperty("_MainTex"))
        {
            liminalGroundMaterial.SetTexture("_MainTex", liminalGroundTexture);
            if (liminalGroundMaterial.HasProperty("_Color"))
                liminalGroundMaterial.SetColor("_Color", Color.white);
        }
    }

    private void ApplyLiminalGroundMaterial(GameObject model)
    {
        if (liminalGroundMaterial == null || model == null) return;
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            var materials = renderers[i].sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderers[i].sharedMaterial = liminalGroundMaterial;
                continue;
            }
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                materials[materialIndex] = liminalGroundMaterial;
            renderers[i].sharedMaterials = materials;
        }
    }

    private void TeleportPlayerToLiminalRoadEnd()
    {
        // The repeat scene owns a factory camera for its cinematic. When it is loaded
        // additively with World_Master, the player is the World_Master rig, so switch the
        // render camera to that rig before teleporting. Otherwise the player moves to the
        // liminal set while the view stays behind in the factory.
        if (repeatPlayerRoot != null && repeatPlayerRoot.gameObject.scene.name == "World_Master")
        {
            var cameras = repeatPlayerRoot.GetComponentsInChildren<Camera>(true);
            Camera worldPlayerCamera = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && (cameras[i].CompareTag("MainCamera") || cameras[i].gameObject.name == "PlayerCamera"))
                {
                    worldPlayerCamera = cameras[i];
                    break;
                }
            }

            if (worldPlayerCamera != null && worldPlayerCamera != playerCamera)
            {
                if (playerCamera != null)
                {
                    playerCamera.enabled = false;
                    var oldListener = playerCamera.GetComponent<AudioListener>();
                    if (oldListener != null) oldListener.enabled = false;
                    if (playerCamera.CompareTag("MainCamera")) playerCamera.tag = "Untagged";
                }

                playerCamera = worldPlayerCamera;
                playerCamera.enabled = true;
                playerCamera.tag = "MainCamera";
                repeatCameraOriginalParent = playerCamera.transform.parent;
                repeatCameraOriginalLocalPosition = playerCamera.transform.localPosition;
                repeatCameraOriginalLocalRotation = playerCamera.transform.localRotation;
                repeatCameraPoseCaptured = repeatCameraOriginalParent != null;

                var activeListener = playerCamera.GetComponent<AudioListener>();
                if (activeListener == null) activeListener = playerCamera.gameObject.AddComponent<AudioListener>();
                var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < listeners.Length; i++)
                {
                    if (listeners[i] != null) listeners[i].enabled = listeners[i] == activeListener;
                }
                activeListener.enabled = true;
            }
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        // Placement was resolved from the shell/door mesh bounds while the set was built.
        var spawn = liminalSpawnPoint;
        var doorLookPoint = liminalGateLookPoint;

        // Find the actual imported road/ground surface under the spawn point. The authored
        // environment's mesh colliders are static and all other gameplay maps are far away.
        var rayOrigin = new Vector3(spawn.x, spawn.y + 30f, spawn.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out var groundHit, 100f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            IsLiminalGroundTransform(groundHit.transform))
        {
            spawn.y = groundHit.point.y;
        }

        var horizontalLook = doorLookPoint - spawn;
        horizontalLook.y = 0f;
        if (horizontalLook.sqrMagnitude < 0.001f)
            horizontalLook = Vector3.left;

        if (repeatPlayerRoot != null)
        {
            var controllers = repeatPlayerRoot.GetComponentsInChildren<CharacterController>(true);
            for (int i = 0; i < controllers.Length; i++)
                controllers[i].enabled = false;
            repeatPlayerRoot.SetPositionAndRotation(
                spawn,
                Quaternion.LookRotation(horizontalLook.normalized, Vector3.up));
            for (int i = 0; i < controllers.Length; i++)
                controllers[i].enabled = true;
        }

        if (playerCamera != null)
        {
            if (repeatCameraOriginalParent != null)
                playerCamera.transform.SetParent(repeatCameraOriginalParent, false);
            if (repeatCameraPoseCaptured && repeatCameraOriginalParent != null)
            {
                playerCamera.transform.localPosition = repeatCameraOriginalLocalPosition;
                playerCamera.transform.localRotation = repeatCameraOriginalLocalRotation;
            }
            else
            {
                playerCamera.transform.position = spawn + Vector3.up * 1.6f;
            }

            var lookRotation = Quaternion.LookRotation(
                doorLookPoint - playerCamera.transform.position,
                Vector3.up);
            playerCamera.transform.rotation = lookRotation;

            // Keep the player's yaw and pitch state in sync with the placed camera.
            // Without this, the first controller Update restores its old pitch/yaw.
            if (repeatPlayerRoot != null && repeatPlayerRoot.gameObject.scene.name == "World_Master")
            {
                var cameraEuler = playerCamera.transform.eulerAngles;
                repeatPlayerRoot.rotation = Quaternion.Euler(0f, cameraEuler.y, 0f);
                playerCamera.transform.localRotation = Quaternion.Euler(cameraEuler.x, 0f, 0f);
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, cameraEuler.x), -85f, 85f);
                SyncPlayerLookPitch(repeatPlayerRoot, pitch);
            }
        }

        Debug.Log(
            $"[Liminal] spawn={spawn.ToString("F1")} player={(repeatPlayerRoot != null ? repeatPlayerRoot.position.ToString("F1") : "none")} " +
            $"camera={(playerCamera != null ? playerCamera.transform.position.ToString("F1") : "none")} " +
            $"shell={liminalShellBounds.center.ToString("F1")} r={liminalShellRadius:F1}",
            this);
    }

    // World_Master is driven by SimpleFirstPersonController ("cameraPitch"); the newer
    // WorldFirstPersonController keeps the same value in "pitch". Whichever one is attached,
    // its private field has to match the camera or the next mouse move snaps the view back.
    private static void SyncPlayerLookPitch(Transform playerRoot, float pitch)
    {
        var simpleController = playerRoot.GetComponent<SimpleFirstPersonController>();
        if (simpleController != null)
        {
            SetPrivateFloat(simpleController, "cameraPitch", pitch);
            return;
        }

        var worldController = playerRoot.GetComponent<WorldFirstPersonController>();
        if (worldController != null)
            SetPrivateFloat(worldController, "pitch", pitch);
    }

    private static void SetPrivateFloat(object target, string fieldName, float value)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = target.GetType().GetField(fieldName, flags);
        if (field != null)
            field.SetValue(target, value);
    }

    private bool IsLiminalGroundTransform(Transform candidate)
    {
        if (candidate == null) return false;
        for (int i = 0; i < liminalGroundModelRoots.Count; i++)
        {
            var groundRoot = liminalGroundModelRoots[i];
            if (groundRoot != null && (candidate == groundRoot || candidate.IsChildOf(groundRoot)))
                return true;
        }
        return false;
    }

    private void UpdateLiminalEnding()
    {
        if (!liminalEndingActive || playerCamera == null)
            return;

        KeepPlayerInsideLiminalShell();
        UpdateLiminalShellVisibility();

        // Plan distance to the door: the gate arch sits above the ground, so a full 3D
        // distance can never get small while the player is held inside the shell.
        var toDoor = playerCamera.transform.position - liminalGateLookPoint;
        toDoor.y = 0f;
        var distance = toDoor.magnitude;
        var scene2Manager = FindFirstObjectByType<Scene2EveningManager>(FindObjectsInactive.Include);

        if (!liminalDoorDialoguePlayed && distance <= 10f)
        {
            liminalDoorDialoguePlayed = true;
            StartCoroutine(ShowLiminalDoorDialogue());
        }

        if (comaEndingStarted)
        {
            SetLiminalDoorPrompt(false);
            return;
        }

        bool atDoor = distance <= LiminalDoorPromptRange;
        SetLiminalDoorPrompt(atDoor);
        if (atDoor && LiminalInteractPressed())
        {
            SetLiminalDoorPrompt(false);
            ClearSubtitle();
            if (scene2Manager != null)
                scene2Manager.SetLiminalEndingObjective(string.Empty);
            StartCoroutine(PlayComaEndingSequence());
        }
    }

    private static bool LiminalInteractPressed()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null)
            return false;
        return keyboard[WorldMasterKeyBindings.GetKey("Interact")].wasPressedThisFrame;
    }

    private void SetLiminalDoorPrompt(bool visible)
    {
        if (visible)
            EnsureLiminalDoorPrompt();
        if (liminalDoorPromptGroup == null)
            return;

        if (visible && liminalDoorPromptTransform != null && playerCamera != null)
        {
            liminalDoorPromptTransform.position = liminalGateKeyPoint;
            var toCamera = playerCamera.transform.position - liminalGateKeyPoint;
            if (toCamera.sqrMagnitude > 0.001f)
                liminalDoorPromptTransform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        liminalDoorPromptGroup.alpha = visible ? 1f : 0f;
    }

    // A small key badge floating in the gateway opening, matching the way the other doors
    // advertise their interact key instead of a full-width banner.
    private void EnsureLiminalDoorPrompt()
    {
        if (liminalDoorPromptGroup != null)
            return;

        var canvasObject = new GameObject("LiminalGateKeyHint", typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
        // Kept at the scene root: a world-space canvas must not inherit a scaled parent.
        canvasObject.transform.SetParent(null, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = playerCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 415;

        liminalDoorPromptTransform = (RectTransform)canvasObject.transform;
        liminalDoorPromptTransform.sizeDelta = new Vector2(96f, 96f);
        liminalDoorPromptTransform.localScale = Vector3.one * 0.0062f;

        var keyObject = new GameObject("KeyCap", typeof(RectTransform));
        keyObject.transform.SetParent(canvasObject.transform, false);
        var keyRect = (RectTransform)keyObject.transform;
        keyRect.anchorMin = Vector2.zero;
        keyRect.anchorMax = Vector2.one;
        keyRect.offsetMin = Vector2.zero;
        keyRect.offsetMax = Vector2.zero;
        var keyImage = keyObject.AddComponent<UnityEngine.UI.Image>();
        keyImage.color = new Color(0.04f, 0.04f, 0.05f, 0.72f);
        keyImage.raycastTarget = false;

        var textObject = new GameObject("KeyLabel", typeof(RectTransform));
        textObject.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)textObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        liminalDoorPromptText = textObject.AddComponent<TextMeshProUGUI>();
        liminalDoorPromptText.text = "E";
        liminalDoorPromptText.fontSize = 64f;
        liminalDoorPromptText.fontStyle = FontStyles.Bold;
        liminalDoorPromptText.alignment = TextAlignmentOptions.Center;
        liminalDoorPromptText.color = Color.white;
        liminalDoorPromptText.outlineWidth = 0.12f;
        liminalDoorPromptText.outlineColor = Color.black;
        liminalDoorPromptText.raycastTarget = false;

        liminalDoorPromptGroup = canvasObject.AddComponent<CanvasGroup>();
        liminalDoorPromptGroup.alpha = 0f;
        liminalDoorPromptGroup.blocksRaycasts = false;
        liminalDoorPromptGroup.interactable = false;
    }

    // The liminal set is abandoned here: white flash at the door, blackout, then the player
    // wakes up in the MC_HOUSE bedroom.
    private IEnumerator PlayComaEndingSequence()
    {
        if (comaEndingStarted)
            yield break;

        comaEndingStarted = true;
        liminalEndingActive = false;
        SetPlayerControlsEnabled(false);
        ClearSubtitle();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var scene2Manager = FindFirstObjectByType<Scene2EveningManager>(FindObjectsInactive.Include);
        var black = CreateComaOverlay("ComaBlackout", Color.black, 431);
        var white = CreateComaOverlay("ComaFlash", Color.white, 432);

        // 1) Hard white flash out of the door, then the screen cuts to black.
        white.alpha = 1f;
        yield return null;
        yield return new WaitForSecondsRealtime(0.16f);
        black.alpha = 1f;
        Destroy(white.gameObject);
        yield return new WaitForSecondsRealtime(0.3f);

        // 2) Move to the bedroom while the screen is black.
        RestoreLiminalRenderSettings();
        if (playerCamera == null)
            playerCamera = Camera.main;
        if (playerCamera != null && playerCamera.transform.parent != null)
            playerCamera.transform.SetParent(null, true);

        var bed = ResolveComaBed(scene2Manager);
        if (playerCamera == null || bed == null)
        {
            Debug.LogError("Coma ending: the bedroom bed or player camera was not found; skipping to the title cards.", this);
            yield return StartCoroutine(PlayComaTitleCards(black));
            yield break;
        }

        var bedPosition = bed.position;
        var bedForward = bed.forward;
        bedForward.y = 0f;
        if (bedForward.sqrMagnitude < 0.001f) bedForward = Vector3.forward;
        bedForward.Normalize();

        // A head-height shot from the mattress: lying on the back, looking at the ceiling.
        var lyingCameraPosition = bedPosition + Vector3.up * 0.95f - bedForward * 0.35f;
        comaLookYaw = Quaternion.LookRotation(bedForward, Vector3.up).eulerAngles.y;
        comaLookPitch = -78f;

        if (repeatPlayerRoot != null)
        {
            var controllers = repeatPlayerRoot.GetComponentsInChildren<CharacterController>(true);
            for (int i = 0; i < controllers.Length; i++)
                controllers[i].enabled = false;
            repeatPlayerRoot.position = bedPosition + Vector3.up * 0.1f;
        }

        playerCamera.transform.SetPositionAndRotation(
            lyingCameraPosition,
            Quaternion.Euler(comaLookPitch, comaLookYaw, 0f));

        if (scene2Manager != null)
            scene2Manager.OpenBedroomForRepeatCutscene();

        var doctor = SpawnComaDoctor(bed);

        yield return Fade(black, 0f, 1.2f);

        // 3) Let the player turn their head on the pillow, then the doctor comes into view.
        yield return WaitWithComaLook(2.2f);
        if (doctor != null)
            yield return TurnToComaDoctor(doctor.transform, 1.4f);

        ShowSubtitle("DOCTOR: Oh... you're finally awake.");
        yield return WaitWithComaLook(3f);
        ShowSubtitle("Where am I now?");
        yield return WaitWithComaLook(2.6f);
        ShowSubtitle("DOCTOR: You've been in a coma for the last 4 months.");
        yield return WaitWithComaLook(3.4f);
        ShowSubtitle("How?");
        yield return WaitWithComaLook(1.8f);
        ShowSubtitle("DOCTOR: You and your father had an accident 4 months ago.");
        yield return WaitWithComaLook(3.6f);
        ShowSubtitle("So where is my father now?");
        yield return WaitWithComaLook(2.8f);
        ShowSubtitle("DOCTOR: ...");
        yield return WaitWithComaLook(1.2f);
        ShowSubtitle("DOCTOR: He passed away in that accident.");
        yield return WaitWithComaLook(3.6f);
        ClearSubtitle();

        // 4) The head rolls back and the shot sinks into the mattress.
        yield return SinkComaCamera(lyingCameraPosition, 1.6f);
        yield return new WaitForSecondsRealtime(1f);

        yield return StartCoroutine(PlayComaTitleCards(black));
    }

    private IEnumerator WaitWithComaLook(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyComaLook();
            yield return null;
        }
    }

    // Head-turn only: the player stays flat on the bed. The view starts on the ceiling and
    // can swing down to bed level, so a turn toward the doctor reads as lying on the back.
    private void ApplyComaLook()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            var delta = mouse.delta.ReadValue();
            comaLookYaw += delta.x * ComaLookSensitivity;
            comaLookPitch -= delta.y * ComaLookSensitivity;
            comaLookPitch = Mathf.Clamp(comaLookPitch, -84f, 25f);
        }

        if (playerCamera != null)
            playerCamera.transform.rotation = Quaternion.Euler(comaLookPitch, comaLookYaw, 0f);
    }

    private IEnumerator TurnToComaDoctor(Transform doctor, float duration)
    {
        if (doctor == null || playerCamera == null)
            yield break;

        // Frame the torso, not the face: from mattress height the head alone would sit at the
        // top edge of the screen, while the chest keeps half the body in shot.
        var doctorBounds = GetCombinedRendererBounds(doctor);
        var torsoPoint = doctorBounds.center;
        if (doctorBounds.size.y > 0.4f)
            torsoPoint.y = doctorBounds.min.y + doctorBounds.size.y * 0.65f;

        var toDoctor = torsoPoint - playerCamera.transform.position;
        if (toDoctor.sqrMagnitude < 0.001f)
            yield break;

        var direction = toDoctor.normalized;
        float targetPitch = Mathf.Clamp(Mathf.Asin(-direction.y) * Mathf.Rad2Deg, -84f, 25f);
        float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float startYaw = comaLookYaw;
        float startPitch = comaLookPitch;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            comaLookYaw = Mathf.LerpAngle(startYaw, targetYaw, t);
            comaLookPitch = Mathf.Lerp(startPitch, targetPitch, t);
            if (playerCamera != null)
                playerCamera.transform.rotation = Quaternion.Euler(comaLookPitch, comaLookYaw, 0f);
            yield return null;
        }

        comaLookYaw = targetYaw;
        comaLookPitch = targetPitch;
    }

    private IEnumerator SinkComaCamera(Vector3 from, float duration)
    {
        if (playerCamera == null)
            yield break;

        var startRotation = playerCamera.transform.rotation;
        var endRotation = Quaternion.Euler(-88f, comaLookYaw, 0f);
        var endPosition = from - Vector3.up * 0.55f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            playerCamera.transform.position = Vector3.Lerp(from, endPosition, t);
            playerCamera.transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }
    }

    private IEnumerator PlayComaTitleCards(CanvasGroup black)
    {
        yield return Fade(black, 1f, 1.4f);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return StartCoroutine(ShowComaTitleCard("NOT A DREAM", 44f, 2f, 432));
        yield return StartCoroutine(ShowComaTitleCard("Developed by : NYXO (Varun)", 34f, 2.6f, 433));

        StopComaDoctorIdle();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            "Assets/Scenes/MainMenu.unity",
            UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private IEnumerator ShowComaTitleCard(string text, float fontSize, float holdSeconds, int sortingOrder)
    {
        var canvasObject = new GameObject("ComaTitleCard");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();

        var textObject = new GameObject("TitleText", typeof(RectTransform));
        textObject.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)textObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        var group = textObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        yield return Fade(group, 1f, 0.7f);
        yield return new WaitForSecondsRealtime(holdSeconds);
        yield return Fade(group, 0f, 0.5f);
        Destroy(canvasObject);
    }

    private CanvasGroup CreateComaOverlay(string name, Color color, int sortingOrder)
    {
        var canvasObject = new GameObject(name);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();

        var fillObject = new GameObject("Fill", typeof(RectTransform));
        fillObject.transform.SetParent(canvasObject.transform, false);
        var fillRect = (RectTransform)fillObject.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var image = fillObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;

        var group = canvasObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        return group;
    }

    private Transform ResolveComaBed(Scene2EveningManager scene2Manager)
    {
        var bed = scene2Manager != null ? scene2Manager.RepeatCutsceneBedroomBed : null;
        if (bed != null)
            return bed;

        // Debug runs can reach the ending without Scene 2's manager; the bed is named in the house.
        var fallback = GameObject.Find("PFB_Bed");
        return fallback != null ? fallback.transform : null;
    }

    private GameObject SpawnComaDoctor(Transform bed)
    {
        if (bed == null)
            return null;

#if UNITY_EDITOR
        var doctorAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ComaDoctorModelPath);
        if (doctorAsset == null)
        {
            Debug.LogError("Coma ending: the doctor model was not found under Assets/doctor.", this);
            return null;
        }

        var doctor = Instantiate(doctorAsset);
        doctor.name = "ComaDoctor";
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(doctor, bed.gameObject.scene);
        doctor.transform.SetParent(null, true);
        doctor.transform.localScale = Vector3.one;

        // The glTF importer leaves a bare Animator behind, so the model would stay in its bind
        // pose. Start the shipped idle now, before measuring, so the pose is a real stance.
        PlayComaDoctorIdle(doctor);

        var renderers = doctor.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            var modelBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                modelBounds.Encapsulate(renderers[i].bounds);
            if (modelBounds.size.y > 0.01f)
                doctor.transform.localScale *= 1.8f / modelBounds.size.y;
        }

        var bedForward = bed.forward;
        bedForward.y = 0f;
        if (bedForward.sqrMagnitude < 0.001f) bedForward = Vector3.forward;
        bedForward.Normalize();
        var bedRight = Vector3.Cross(Vector3.up, bedForward).normalized;

        // Stand at the side of the bed, level with the patient's shoulders rather than the middle.
        var bedPosition = bed.position;
        var bedside = bedPosition - bedForward * 0.25f;
        var rightSide = bedside + bedRight * 1.45f;
        var leftSide = bedside - bedRight * 1.45f;
        var target = rightSide;
        if (!HasRoomForComaDoctor(rightSide, bedRight) && HasRoomForComaDoctor(leftSide, -bedRight))
            target = leftSide;

        target.y = ResolveBedroomFloorHeight(target, bedPosition.y);
        doctor.transform.position = target;
        var doctorBounds = GetCombinedRendererBounds(doctor.transform);
        doctor.transform.position += Vector3.up * (target.y - doctorBounds.min.y);

        var lookAtCamera = playerCamera != null ? playerCamera.transform.position : bedPosition;
        lookAtCamera.y = doctor.transform.position.y;
        var toPlayer = lookAtCamera - doctor.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.001f)
            doctor.transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);

        // The imported skinned meshes update their skinning even when offscreen; the doctor is
        // only watched from the bed, so the offscreen updates are pure cost.
        var skinnedMeshes = doctor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedMeshes.Length; i++)
            skinnedMeshes[i].updateWhenOffscreen = false;

        return doctor;
#else
        return null;
#endif
    }

    private void PlayComaDoctorIdle(GameObject doctor)
    {
        var animator = doctor.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = doctor.AddComponent<Animator>();

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.enabled = true;

        AnimationClip idle = null;
#if UNITY_EDITOR
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(ComaDoctorModelPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && clip.length > 0.05f &&
                !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
            {
                idle = clip;
                break;
            }
        }
#endif
        if (idle == null)
        {
            Debug.LogWarning("Coma ending: the doctor idle clip was not found; the model stays in its bind pose.", this);
            return;
        }

        // Only one animation output may drive this Animator, and the imported one has no usable
        // controller, so the clip is played through a playable graph.
        animator.runtimeAnimatorController = null;
        comaDoctorIdle = UnityEngine.Playables.AnimationPlayableUtilities.PlayClip(animator, idle, out comaDoctorGraph);
        comaDoctorIdleLength = idle.length;
        comaDoctorIdleTime = 0f;
        comaDoctorIdleActive = true;
        animator.Update(0f);
        StartCoroutine(LoopComaDoctorIdle());
    }

    private void StopComaDoctorIdle()
    {
        comaDoctorIdleActive = false;
        if (comaDoctorGraph.IsValid())
            comaDoctorGraph.Destroy();
    }

    // PlayClip holds the last frame, and the coma dialogue outlasts the 3 s take, so the clip
    // time is wrapped by hand on unscaled time.
    private IEnumerator LoopComaDoctorIdle()
    {
        while (comaDoctorIdleActive)
        {
            comaDoctorIdleTime += Time.unscaledDeltaTime;
            if (comaDoctorIdleLength > 0.01f)
                UnityEngine.Playables.PlayableExtensions.SetTime(
                    comaDoctorIdle, comaDoctorIdleTime % comaDoctorIdleLength);
            yield return null;
        }
    }

    private static bool HasRoomForComaDoctor(Vector3 position, Vector3 outward)
    {
        var origin = position + Vector3.up * 1.2f;
        return !Physics.Raycast(
            origin, outward, 0.85f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    // The rim walls are invisible box colliders; if the character controller ever slips past
    // them, put the player back just inside the ring so the ending area cannot be left.
    private void KeepPlayerInsideLiminalShell()
    {
        if (repeatPlayerRoot == null || liminalShellRadius < 1f)
            return;

        Vector3 offset = repeatPlayerRoot.position - liminalShellBounds.center;
        offset.y = 0f;
        float limit = liminalShellRadius + LiminalBoundaryMargin - 0.6f;
        if (offset.sqrMagnitude <= limit * limit)
            return;

        var outward = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector3.back;
        var target = new Vector3(
            liminalShellBounds.center.x + outward.x * limit,
            repeatPlayerRoot.position.y,
            liminalShellBounds.center.z + outward.z * limit);

        var controllers = repeatPlayerRoot.GetComponentsInChildren<CharacterController>(true);
        for (int i = 0; i < controllers.Length; i++)
            controllers[i].enabled = false;
        repeatPlayerRoot.position = target;
        for (int i = 0; i < controllers.Length; i++)
            controllers[i].enabled = true;
    }

    // model_1 is an outward-facing dome: invisible from inside, but the moment the camera
    // steps past the rim it reads as a bare sphere hanging behind the gate. The playable area
    // ends well inside this radius, so the shell simply stays off while the player is around.
    private void UpdateLiminalShellVisibility()
    {
        if (liminalShellRenderers == null || liminalShellRenderers.Length == 0)
            return;

        var cameraPosition = playerCamera.transform.position;
        float dx = cameraPosition.x - liminalShellBounds.center.x;
        float dz = cameraPosition.z - liminalShellBounds.center.z;
        float hideRadius = liminalShellRadius * 1.6f;
        bool hideShell = dx * dx + dz * dz < hideRadius * hideRadius;

        for (int i = 0; i < liminalShellRenderers.Length; i++)
        {
            var shellRenderer = liminalShellRenderers[i];
            if (shellRenderer != null && shellRenderer.enabled == hideShell)
                shellRenderer.enabled = !hideShell;
        }
    }

    private IEnumerator ShowLiminalDoorDialogue()
    {
        ShowSubtitle("Is that a way out?");
        yield return new WaitForSecondsRealtime(2.4f);
        ClearSubtitle();
    }

private GameObject SpawnMotherEnteringFromSide(GameObject fred)
    {
        if (fred == null)
        {
            return null;
        }

#if UNITY_EDITOR
        var motherAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/MOM/mom.glb");
        if (motherAsset == null)
        {
            Debug.LogError("Repeat Scene could not load mom.glb.", this);
            return null;
        }

        var mother = Instantiate(motherAsset);
        mother.name = "RepeatMother";
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(mother, gameObject.scene);
        var side = playerCamera != null ? playerCamera.transform.right : fred.transform.right;
        side.y = 0f;
        if (side.sqrMagnitude < 0.001f)
        {
            side = Vector3.right;
        }
        side.Normalize();
        var fredBounds = GetCombinedRendererBounds(fred.transform);
        mother.transform.position = fred.transform.position + side * 3.2f;
        mother.transform.position = new Vector3(mother.transform.position.x, fredBounds.min.y, mother.transform.position.z);
        mother.transform.rotation = Quaternion.LookRotation(-side, Vector3.up);
        mother.transform.localScale = Vector3.one;
        var localRenderers = mother.GetComponentsInChildren<Renderer>(true);
        if (localRenderers.Length > 0)
        {
            var localBounds = localRenderers[0].localBounds;
            for (int i = 1; i < localRenderers.Length; i++)
            {
                localBounds.Encapsulate(localRenderers[i].localBounds);
            }
            if (localBounds.size.y > 0.01f)
            {
                mother.transform.localScale *= 2.25f / (localBounds.size.y * mother.transform.localScale.y);
            }
            mother.transform.position = new Vector3(
                mother.transform.position.x,
                fredBounds.min.y - localBounds.min.y * mother.transform.localScale.y,
                mother.transform.position.z);
        }
        return mother;
#else
        return null;
#endif
    }

    private IEnumerator MoveMotherIntoView(GameObject mother, GameObject fred)
    {
        if (mother == null || fred == null)
        {
            yield break;
        }

        var side = playerCamera != null ? playerCamera.transform.right : fred.transform.right;
        side.y = 0f;
        side = side.sqrMagnitude > 0.001f ? side.normalized : Vector3.right;
        var start = mother.transform.position;
        var startY = start.y;
        var end = fred.transform.position + side * 0.55f;
        end.y = startY;
        mother.transform.position = start;

        var elapsed = 0f;
        const float duration = 1.8f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            mother.transform.position = Vector3.Lerp(start, end, t);
            if (playerCamera != null)
            {
                var target = mother.transform.position + Vector3.up * 1.45f;
                playerCamera.transform.rotation = Quaternion.RotateTowards(
                    playerCamera.transform.rotation,
                    Quaternion.LookRotation(target - playerCamera.transform.position, Vector3.up),
                    65f * Time.unscaledDeltaTime);
            }
            yield return null;
        }
        mother.transform.position = end;
        var lookAtPlayer = playerCamera != null ? playerCamera.transform.position - mother.transform.position : Vector3.zero;
        lookAtPlayer.y = 0f;
        if (lookAtPlayer.sqrMagnitude > 0.001f)
        {
            mother.transform.rotation = Quaternion.LookRotation(lookAtPlayer.normalized, Vector3.up);
        }
    }

    private static Bounds GetCombinedRendererBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.position, Vector3.one);
        }
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    private void ResolveRepeatSceneReferences(UnityEngine.SceneManagement.Scene scene)
    {
        repeatFred = FindObjectInScene(scene, "Fred");
        repeatCameraTransform = FindObjectInScene(scene, "PlayerCamera");
        if (repeatCameraTransform == null && playerCamera != null)
        {
            repeatCameraTransform = playerCamera.transform;
        }
        if (repeatCameraTransform == null && playerCamera != null)
        {
            repeatCameraTransform = playerCamera.transform;
        }
        if (repeatCameraTransform == null)
        {
            var camera = FindCameraInHierarchy(transform.root);
            if (camera != null) repeatCameraTransform = camera.transform;
        }
        repeatPlayerRoot = repeatCameraTransform != null ? repeatCameraTransform.root : null;
    }

    private static Transform FindRootObject(UnityEngine.SceneManagement.Scene scene, string objectName)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
            {
                return roots[i].transform;
            }
        }
        return null;
    }

    private static Transform FindObjectInScene(UnityEngine.SceneManagement.Scene scene, string objectName)
    {
        var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            if (allTransforms[i].gameObject.scene == scene && allTransforms[i].name == objectName)
            {
                return allTransforms[i];
            }
        }

        foreach (var rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == objectName)
            {
                return rootObject.transform;
            }
            var found = FindChildByName(rootObject.transform, objectName);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static Transform FindChildByName(Transform parent, string objectName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == objectName)
            {
                return child;
            }
            var found = FindChildByName(child, objectName);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static Camera FindCameraInHierarchy(Transform root)
    {
        var cameras = root.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].gameObject.name == "PlayerCamera" || cameras[i].gameObject.name == "FirstPersonCamera")
            {
                return cameras[i];
            }
        }
        return cameras.Length > 0 ? cameras[0] : null;
    }

    private IEnumerator AimCameraAt(Vector3 point, float duration)
    {
        if (playerCamera == null)
        {
            yield break;
        }
        var start = playerCamera.transform.rotation;
        var target = Quaternion.LookRotation(point - playerCamera.transform.position, Vector3.up);
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            playerCamera.transform.rotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        playerCamera.transform.rotation = target;
    }

    private IEnumerator RotateCamera(Quaternion from, Quaternion to, float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (playerCamera != null)
            {
                playerCamera.transform.rotation = Quaternion.Slerp(from, to, amount);
            }

            yield return null;
        }

        if (playerCamera != null)
        {
            playerCamera.transform.rotation = to;
        }
    }

    private IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        if (group == null)
        {
            yield break;
        }

        var start = group.alpha;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = target;
    }

    private IEnumerator FlickerLoop()
    {
        if (overheadLight == null)
        {
            yield break;
        }

        var stableIntensity = overheadLight.intensity;
        while (State != OpeningState.SceneComplete)
        {
            yield return new WaitForSecondsRealtime(Random.Range(2.8f, 6.5f));
            var dips = Random.Range(1, 3);
            for (var i = 0; i < dips; i++)
            {
                overheadLight.intensity = stableIntensity * Random.Range(0.12f, 0.32f);
                yield return new WaitForSecondsRealtime(Random.Range(0.045f, 0.09f));
                overheadLight.intensity = stableIntensity * Random.Range(0.88f, 1.04f);
                yield return new WaitForSecondsRealtime(Random.Range(0.08f, 0.16f));
            }

            overheadLight.intensity = stableIntensity;
        }
    }

    private void ShowSubtitle(string line)
    {
        TriggerCue(subtitleCue, 0.11f);
        if (subtitles != null)
        {
            subtitles.Show(line);
        }
    }

    private void ClearSubtitle()
    {
        if (subtitles != null)
        {
            subtitles.Clear();
        }
    }

    private static void PlayLoop(AudioSource source)
    {
        if (source == null || source.clip == null || source.isPlaying)
        {
            return;
        }

        source.loop = true;
        source.Play();
    }

    private void SetupSuspiciousAmbience()
    {
        if (suspiciousAmbienceClip == null)
        {
#if UNITY_EDITOR
            suspiciousAmbienceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sounds/suspicious ambiance.mp3");
#endif
        }

        if (suspiciousAmbienceClip == null)
        {
            Debug.LogWarning("Scene1 could not load Assets/Sounds/suspicious ambiance.mp3.", this);
            return;
        }

        var sourceObject = new GameObject("Suspicious Ambience");
        sourceObject.transform.SetParent(transform, false);
        suspiciousAmbienceSource = sourceObject.AddComponent<AudioSource>();
        suspiciousAmbienceSource.clip = suspiciousAmbienceClip;
        suspiciousAmbienceSource.loop = true;
        suspiciousAmbienceSource.playOnAwake = false;
        suspiciousAmbienceSource.spatialBlend = 0f;
        suspiciousAmbienceSource.volume = 0.32f;
    }

    private IEnumerator FadeOutSuspiciousAmbience(float duration)
    {
        if (suspiciousAmbienceSource == null || !suspiciousAmbienceSource.isPlaying)
        {
            yield break;
        }

        var startVolume = suspiciousAmbienceSource.volume;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            suspiciousAmbienceSource.volume = Mathf.Lerp(startVolume, 0f, progress);
            yield return null;
        }

        suspiciousAmbienceSource.Stop();
        suspiciousAmbienceSource.volume = startVolume;
    }

    private static void TriggerCue(AudioSource source, float volume)
    {
        if (source == null || source.clip == null)
        {
            return;
        }

        source.volume = Mathf.Clamp01(volume);
        source.PlayOneShot(source.clip, volume);
    }

    private void ResolveReferences()
    {
        if (playerCamera == null && isRepeatScene)
        {
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != recordingCamera && cameras[i].enabled && cameras[i].tag == "MainCamera")
                {
                    playerCamera = cameras[i];
                    break;
                }
            }
            if (playerCamera == null) playerCamera = Camera.main;
        }
        if (playerCamera == null && !isRepeatScene)
        {
            var player = GameObject.Find("PlayerCamera");
            if (player != null) playerCamera = player.GetComponent<Camera>();
        }

        if (recordingCamera == null && !isRepeatScene)
        {
            var recorder = GameObject.Find("RecordingCamera");
            if (recorder != null) recordingCamera = recorder.GetComponent<Camera>();
        }

        if (fadeOverlay == null && !isRepeatScene)
        {
            var fade = GameObject.Find("FadeOverlay");
            if (fade != null) fadeOverlay = fade.GetComponent<CanvasGroup>();
        }

        if (subtitles == null)
        {
            var subtitleControllers = FindObjectsByType<Scene1SubtitleController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < subtitleControllers.Length; i++)
            {
                if (subtitleControllers[i].gameObject.scene == gameObject.scene)
                {
                    subtitles = subtitleControllers[i];
                    break;
                }
            }
        }
        if (titleCard == null)
        {
            var titleCards = FindObjectsByType<Scene1TitleCardController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < titleCards.Length; i++)
            {
                if (titleCards[i].gameObject.scene == gameObject.scene)
                {
                    titleCard = titleCards[i];
                    break;
                }
            }
        }
        if (!isRepeatScene)
        {
            if (overheadLight == null)
            {
                var lightObject = GameObject.Find("Unstable Overhead Light");
                if (lightObject != null) overheadLight = lightObject.GetComponent<Light>();
            }

            if (recordingFill == null)
            {
                var fillObject = GameObject.Find("Recording Camera Fill");
                if (fillObject != null) recordingFill = fillObject.GetComponent<Light>();
            }

            roomAmbience = FindAudio("Room Ambience", roomAmbience);
            electricalHum = FindAudio("Electrical Hum", electricalHum);
            monitorNoise = FindAudio("Monitor Noise", monitorNoise);
            lightBuzz = FindAudio("Light Buzz", lightBuzz);
            distantBuildingNoise = FindAudio("Distant Building Noise", distantBuildingNoise);
            airMovement = FindAudio("Air Movement", airMovement);

            subtitleCue = EnsureAudioSource("Subtitle Cue", subtitleCue, true, 0.12f);
            titleCardCue = EnsureAudioSource("Title Card Cue", titleCardCue, false, 0.18f);
            characterPresence = EnsureAudioSource("BlackClad Man Presence", characterPresence, true, 0.05f);
        }
    }

    private static AudioSource FindAudio(string objectName, AudioSource current)
    {
        if (current != null) return current;
        var sourceObject = GameObject.Find(objectName);
        return sourceObject != null ? sourceObject.GetComponent<AudioSource>() : null;
    }

    private static AudioSource EnsureAudioSource(string name, AudioSource current, bool loop, float volume)
    {
        if (current != null)
        {
            if (current.clip == null)
            {
                current.clip = CreateCueClip(name, loop ? 0.9f : 0.28f, loop ? 0.11f : 0.24f);
            }

            current.volume = volume;
            current.loop = loop;
            return current;
        }

        var sourceObject = new GameObject(name);
        sourceObject.transform.SetParent(GameObject.Find("Scene1OpeningManager")?.transform ?? null, false);
        var source = sourceObject.AddComponent<AudioSource>();
        source.clip = CreateCueClip(name, loop ? 0.9f : 0.28f, loop ? 0.11f : 0.24f);
        source.loop = loop;
        source.volume = volume;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private static AudioClip CreateCueClip(string clipName, float duration, float lowFrequency)
    {
        var sampleRate = 22050;
        var sampleCount = Mathf.CeilToInt(sampleRate * duration);
        var samples = new float[sampleCount];
        var random = new System.Random(clipName.GetHashCode());

        for (var i = 0; i < sampleCount; i++)
        {
            var time = (float)i / sampleRate;
            var envelope = Mathf.Clamp01(1f - (time / duration));
            var baseTone = Mathf.Sin(time * (lowFrequency * 220f + 50f) * Mathf.PI * 2f) * 0.18f;
            var noise = (float)(random.NextDouble() * 2.0 - 1.0) * 0.07f;
            var rumble = Mathf.Sin(time * 29f + random.Next(0, 8)) * 0.05f;
            samples[i] = Mathf.Clamp((baseTone + noise + rumble) * envelope, -0.4f, 0.4f);
        }

        var clip = AudioClip.Create("Scene1 " + clipName + " Placeholder", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
