using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public sealed class Scene2EveningManager : MonoBehaviour
{
    private struct DialogueLine
    {
        public readonly string Speaker;
        public readonly string Text;

        public DialogueLine(string speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }
    }

    // A screen-space objective marker: a yellow pin plus a live distance readout, tracking a world
    // position. Being UI, it always draws on top of the world (houses, trees, terrain) and keeps a
    // constant pixel size at any distance.
    private sealed class ScreenMarker
    {
        public GameObject Root;
        public RectTransform Icon;
        public RectTransform DistanceLabel;
        public TextMeshProUGUI DistanceText;
        public Vector3 WorldPosition;
    }

    private enum ScenePhase
    {
        Intro,
        GoHome,
        EnterHouse,
        EnterHouseDialogue,
        GoToMom,
        MomDialogue,
        CheckLaptop,
        BrotherDialogue,
        BedroomTransition,
        BedPrompt,
        LyingDownTransition,
        PhonePrompt,
        PhoneOpen,
        PostPhoneDialogue,
        LaptopSequence,
        TimeTransition,
        NightLaptopDialogue,
        GoToMomAfterTimeSkip,
        MissingMomDialogue,
        LateDiningLaptopDialogue,
        LateCheckLaptop,
        RedRoomIntroDialogue,
        RedRoomImages,
        ClosingDiningLaptop,
        LittleBrotherRedRoomDialogue,
        FredArgumentLeadIn,
        FredArgumentPhone,
        TwoHoursLaterTransition,
        WakeUpSequence,
        BrotherMissingSearch,
        BathroomShadow,
        OutsideSearch,
        FredWarningPhone,
        FredLinkLeadIn,
        FredLinkLoading,
        FredImageDialogue,
        FredImagePhoneClose,
        MomNoteApproach,
        MomNoteText,
        MomAfterNoteDialogue,
        ReadMomNote,
        MysteriousMan,
        EscapeDoorSetup,
        WindowDistraction,
        EscapeDanger,
        RoomSecured,
        FinalEscape,
        GameOver,
        RetryCheckpoint,
        JungleChase,
        JungleChaseSpotted,
        JungleChaseVanish,
        AuntElaApproach,
        AuntElaSecondApproach,
        AuntElaApproachFredScare,
        AuntElaFredScareBlackout,
        AuntElaSpot,
        AuntElaRush,
        HallwayWake,
        HallwayExplore,
        HallwayEndless,
        HallwayPcSpotted,
        HallwayGoToPc,
        HallwayPcDialogue,
        BedroomReturn,
        Finished
    }

    private readonly List<DialogueLine> introDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "What the hell is dark web?"),
        new DialogueLine("MC", "Is that even legal?"),
        new DialogueLine("Fred", "Trust me it’s 100% legal and it’s so fun to use it, trust me."),
        new DialogueLine("MC", "Ok whatever it is I am not going to use this."),
        new DialogueLine("Fred", "Trust me, I’ll send you a link tonight. Open it in an onion browser."),
        new DialogueLine("Fred", "Hey wait listen."),
        new DialogueLine("MC", "Oh he thinks I am a fool. I am not going to use it. Let’s get home or mom will get angry.")
    };

    private readonly List<DialogueLine> momDialogue = new List<DialogueLine>
    {
        new DialogueLine("Mom", "Did you finish your homework?"),
        new DialogueLine("MC", "No homework today, today I just chill and watch NYXOPLAYZ videos.")
    };

    private readonly List<DialogueLine> brotherDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "What are you doing?"),
        new DialogueLine("Little Brother", "Oh nothing.")
    };

    private readonly List<DialogueLine> afterPhoneDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "Nyxo’s videos are just so fun.")
    };

    // The jungle chase: after the player leaves the house and heads for Aunt Ela's, something
    // picks up their trail on the road and hunts them until it abruptly disappears.
    private readonly List<DialogueLine> jungleChaseStartDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "What is that? Something is moving out there."),
        new DialogueLine("MC", "I have to run!")
    };

    private readonly List<DialogueLine> jungleChaseVanishDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "Where did it go? It just vanished.")
    };

    // The second encounter, right outside Aunt Ela's house: it appears out of nowhere again.
    private readonly List<DialogueLine> auntElaSpotDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "WTF? Not again!")
    };

    // ~200 m before Aunt Ela's house a strange sound plays behind the player; when they turn
    // around Fred is standing there and the screen instantly cuts to black.
    private readonly List<DialogueLine> fredScareWhatsThatDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "What's that?")
    };

    // The player jolts awake standing in the endless yellow hallway after the second encounter.
    private readonly List<DialogueLine> hallwayWakeDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "Where am I now?"),
        new DialogueLine("MC", "What's this place? I have to find an exit to get out of here.")
    };

    // Fired after the player walks ~70 m in either direction inside the hallway.
    private readonly List<DialogueLine> hallwayEndlessDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "Is this place endless? How do I get out of here.")
    };

    // The office PC setup that appears ~10 m ahead after the "endless" line.
    private readonly List<DialogueLine> hallwayPcSpottedDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "What's that?")
    };

    // Conversation with the unseen developer once the player reaches the office setup.
    private readonly List<DialogueLine> hallwayPcDialogue = new List<DialogueLine>
    {
        new DialogueLine("Unknown Man", "Hey, how did you get in there?"),
        new DialogueLine("MC", "Where are you? I can't see you."),
        new DialogueLine("Unknown Man", "Yeah, you can't see me. I am your developer."),
        new DialogueLine("MC", "What?"),
        new DialogueLine("Unknown Man", "Nothing."),
        new DialogueLine("MC", "What's this place?"),
        new DialogueLine("Unknown Man", "Oh, you don't have to know this. It's a setup of a future project."),
        new DialogueLine("MC", "What?"),
        new DialogueLine("Unknown Man", "Ok, bye."),
        new DialogueLine("MC", "Wait!")
    };

    // Back in the bedroom after the black screen ends the hallway sequence.
    private readonly List<DialogueLine> bedroomReturnDialogue = new List<DialogueLine>
    {
        new DialogueLine("MC", "Was that a dream?"),
        new DialogueLine("MC", "I have to call the police now."),
        new DialogueLine("MC", "Where is my phone? I can't find it."),
        new DialogueLine("MC", "Ok, I am going straight to Aunt Ela's house.")
    };

    private GameObject player;
    private CharacterController characterController;
    private SimpleFirstPersonController firstPersonController;
    private WorldFirstPersonController worldFirstPersonController;
    private bool firstPersonControllerWasEnabled;
    private bool worldFirstPersonControllerWasEnabled;
    private bool pauseSettingsMenuWasEnabled;
    private bool playerControlsLocked;
    private PauseSettingsMenu pauseSettingsMenu;
    private Camera playerCamera;
    private AudioSource audioSource;
    private AudioSource uiSfxAudioSource;
    private AudioSource impactSfxAudioSource;
    private AudioSource neighborhoodAmbienceSource;
    private AudioSource liminalAmbienceSource;
    private AudioSource horrorAmbienceSource;
    private AudioSource heartbeatAudioSource;
    private AudioSource unknownManVoiceSource;
    private AudioClip unknownManGreetingVoiceClip;
    private AudioClip unknownManDeveloperVoiceClip;
    private AudioClip unknownManNothingVoiceClip;
    private AudioClip unknownManSetupVoiceClip;
    private AudioClip unknownManByeVoiceClip;
    private AudioClip pendingVoiceClip;
    private bool advanceQueuedForVoice;
    private AudioClip typingClip;
    private AudioClip notificationClip;
    private AudioClip walkingSfxClip;
    private AudioClip jumpSfxClip;
    private AudioClip clickSfxClip;
    private AudioClip fredHitSfxClip;
    private AudioClip horrorHitSfxClip;
    private AudioClip neighborhoodAmbienceClip;
    private AudioClip liminalAmbienceClip;
    private AudioClip horrorAmbienceClip;
    private AudioClip heartbeatClip;
    private AudioClip killSfxClip;
    [SerializeField] private AudioClip chainsawClip;
    [SerializeField] private Texture2D fredLinkImage5;

    private GameObject fredActor;
    private GameObject momActor;
    private GameObject brotherActor;
    private GameObject phoneObject;
    private WorldPhoneMessages phoneMessages;
    private GameObject houseTrigger;
    private GameObject bedroomTrigger;
    private GameObject houseObjectiveMarker;
    private GameObject auntElaMarkerObject;
    private ScreenMarker auntElaMarker;
    private ScreenMarker homeMarker;

    [Tooltip("On-screen size of the location pin, in UI pixels (constant at any distance).")]
    [SerializeField] private float auntElaMarkerScreenSize = 90f;

    [Tooltip("Optional override: world position of Aunt Ela's house. Leave at zero to locate the " +
             "house in the scene automatically.")]
    [SerializeField] private Vector3 auntElaHousePosition = Vector3.zero;

    private Transform homeHouse;
    private Transform mainRoad;
    private Transform auntElaHouseResolved;
    private float nextAuntElaHouseSearchTime;
    private Transform bathroomWindowCurtain;
    private Transform bathroomWindowSink;
    private bool bathroomAnchorsResolved;
    private bool bathroomFocusResolved;
    private Vector3 bathroomFocusPoint;
    private Transform bedroomBed;
    private Transform bedroomDoor;
    private Vector3 bedroomDoorwayCenter;
    private Vector3 bedroomDoorwayNormal;
    private Transform bathroomDoor;
    private GameObject bathroomTrigger;
    private Animator bedroomDoorAnimator;
    private MoveObjectController bedroomDoorInteraction;
        private MoveObjectController houseEntranceDoorInteraction;
    private string bedroomDoorOpenParameter;
    private Transform houseEntranceDoor;
    private Animator houseEntranceDoorAnimator;
    private string houseEntranceDoorOpenParameter;
    private Transform bedroomLaptop;
    private Renderer bedroomLaptopScreenRenderer;
    private Transform placeholderLaptop;
    private Renderer placeholderLaptopScreenRenderer;
    private GameObject laptopImageSurface;
    private TextMeshPro laptopImageLabel;
    private Transform diningLaptop;
    private Transform diningLaptopScreenLid;
    private Quaternion diningLaptopScreenOpenRotation;
    private Transform momPlaceholderBody;
    private Transform brotherPlaceholderBody;
    private Transform momModelRoot;
    private Transform brotherModelRoot;
    private Material laptopImageMaterial;

    private GameObject dialogueCanvasObject;
    private GameObject dialoguePanelObject;
    private GameObject objectiveBlock;
    private CanvasGroup objectiveCanvasGroup;
    private TextMeshProUGUI objectiveHeaderText;
    private TextMeshProUGUI speakerText;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI promptText;
    private TextMeshProUGUI objectiveText;
    private string lastObjectiveLayoutText;
    private TextMeshProUGUI phonePromptText;
    private GameObject timeTransitionPanel;
    private CanvasGroup timeTransitionCanvasGroup;
    private CanvasGroup timeTransitionTextCanvasGroup;
    private TextMeshProUGUI timeTransitionText;
    private GameObject bathroomShadowObject;
    private GameObject noteObject;
    private GameObject noteMarkerObject;
    private GameObject suspiciousSilhouetteObject;
    private GameObject distractionProp;
    private GameObject retryButtonObject;
    private Button retryButton;

    private ScenePhase phase;
    private List<DialogueLine> activeDialogue;
    private int dialogueIndex;
    private bool dialogueActive;
    private bool isTyping;
    private bool waitForSpace;
    private string pendingSentence;
    private Coroutine typingCoroutine;
    private Transform focusTarget;
    private Vector3 cameraAimPoint;
    private bool hasCameraAimPoint;
    private Vector3 cameraAimOverride;
    private bool hasCameraAimOverride;
    private bool homeObjectiveActive;
    private bool houseReached;
    private bool cameraFocusActive;
    private Vector3 cameraFocusPoint;
    private bool diningLaptopViewLocked;
    private bool redRoomLaptopViewSaved;
    private bool bedroomDoorOpenedAfterTimeSkip;
    private bool houseDoorClosedForStory;
    private bool noteRead;
    private bool noteApproached;
    private bool bathroomShadowTriggered;
    private bool brotherHiddenDuringSearch;
    private bool outsideSearchTriggered;
    private bool warningPhoneOpened;
    private bool distractionThrown;
    private bool distractionClockPickedUp;
    private bool horrorAmbienceJourneyActive;
    private bool heartbeatActive;
    private bool escapeTimerStarted;
    private bool escapeTimerExpired;
    private bool gameOverVisible;
    private bool creatureRushActive;
    private float escapeTimerRemaining;
    private GameObject pursuingCreatureObject;
    private Vector3 pursuingCreatureTargetPosition;
    private bool pursuingCreatureAtBedroomWindow;

    // Jungle chase state (the monster that stalks the player on the road to Aunt Ela's).
    private GameObject jungleChaseMonster;
    private bool jungleChaseActive;
    private bool jungleChaseStarted;
    private bool jungleChaseVanishShown;
    private Vector3 jungleChaseLastTrackedPosition;
    private float jungleChaseTravelledBeforeTrigger;
    private bool jungleChaseTravelTrackingInitialized;
    private float jungleChaseSpeed;
    private Vector3 jungleChaseVanishPoint;
    private float jungleChaseCatchDistance = 1.4f;
    [SerializeField] private float jungleChaseCatchReset = 0.4f;
    private float jungleChaseCatchTimer;
    private Vector3 jungleChaseCheckpointPosition;
    private bool jungleChaseCheckpointValid;
    private bool gameOverFromJungleChase;

    // The second encounter, right outside Aunt Ela's house. Once the jungle chase has vanished the
    // ambush is armed and fires when the player gets within ~10 m of Aunt Ela's door: the monster
    // appears, the player says "WTF", and it rushes them before the screen cuts to black.
    private bool auntElaSpotArmed;
    private bool auntElaSpotTriggered;
    private bool auntElaRushActive;
    private float auntElaRushTimer;
    private GameObject auntElaMonster;
    [SerializeField] private float auntElaSpotDistance = 10f;
    private bool finalEscapeActive;
    private float finalEscapeRemaining;
    private Vector3 diningLaptopCameraPosition;
    private Quaternion diningLaptopCameraRotation;
    private Vector3 cameraPositionBeforeLaptopCloseup;
    private Quaternion cameraRotationBeforeLaptopCloseup;
    private bool hasCameraPoseBeforeLaptopCloseup;
    private string currentObjective;
    private string currentPrompt;
    private bool bedroomReached;
    private int laptopImageIndex;
    private Vector3 standingCameraLocalPosition;
    private bool chainsawWarningLogged;
    private bool sceneInitialized;
    private bool sceneInitializationFailed;
    [SerializeField] private Material nightSkyboxMaterial;
    [SerializeField] private Material sunsetSkyboxMaterial;
    private Material sceneSkyboxMaterial;
    private Material originalSkyboxMaterial;
    private Light sceneDirectionalLight;
    private Color originalDirectionalColor;
    private float originalDirectionalIntensity;
    private Quaternion originalDirectionalRotation;
    private UnityEngine.Rendering.AmbientMode originalAmbientMode;
    private Color originalAmbientSkyColor;
    private Color originalAmbientEquatorColor;
    private Color originalAmbientGroundColor;
    private Color originalAmbientLight;
    private float originalAmbientIntensity;
    private bool originalFogEnabled;
    private FogMode originalFogMode;
    private Color originalFogColor;
    private float originalFogDensity;
    private GameObject liminalVolumeObject;
    private float liminalVolumePulseTime;

    private void Awake()
    {
        var worldMaster = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/World_Master.unity");
        if (!worldMaster.IsValid() || !worldMaster.isLoaded)
        {
            StartCoroutine(LoadWorldMasterThenInitialize());
            return;
        }

        InitializeScene();
    }

    private void InitializeScene()
    {
        phase = ScenePhase.Intro;
        CaptureSceneLighting();
        ApplyEveningLighting();
        SetupPlayer();
        SetupAudio();
        SetupUi();
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        sceneInitialized = true;
    }

    private IEnumerator Start()
    {
        while (!sceneInitialized && !sceneInitializationFailed)
            yield return null;

        if (sceneInitializationFailed)
            yield break;

        SpawnSceneActors();
        UpdateObjectiveUi();
        StartConversation(introDialogue, ScenePhase.Intro);
    }

    private IEnumerator LoadWorldMasterThenInitialize()
    {
        const string worldMasterPath = "Assets/Scenes/World_Master.unity";
        var operation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
            worldMasterPath,
            UnityEngine.SceneManagement.LoadSceneMode.Additive);
        if (operation == null)
        {
            Debug.LogError("Scene2 could not load required scene: " + worldMasterPath, this);
            sceneInitializationFailed = true;
            yield break;
        }

        while (!operation.isDone)
            yield return null;

        var loadedWorld = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(worldMasterPath);
        if (!loadedWorld.IsValid() || !loadedWorld.isLoaded)
        {
            Debug.LogError("Scene2 finished loading but World_Master is not available.", this);
            sceneInitializationFailed = true;
            yield break;
        }

        var scene2 = gameObject.scene;
        if (scene2.IsValid() && scene2.isLoaded)
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene2);

        InitializeScene();
    }

    private void SetupPlayer()
    {
        var testingPlayer = GameObject.Find("TestingPlayer");
        if (testingPlayer == null)
        {
            var fp = FindAnyObjectByType<SimpleFirstPersonController>();
            if (fp != null)
            {
                testingPlayer = fp.gameObject;
            }
        }

        if (testingPlayer == null)
        {
            var worldFp = FindAnyObjectByType<WorldFirstPersonController>();
            if (worldFp != null)
            {
                testingPlayer = worldFp.gameObject;
            }
        }

        player = testingPlayer;
        if (player == null)
        {
            Debug.LogWarning("Scene2EveningManager could not find a player object.");
            return;
        }

        firstPersonController = player.GetComponent<SimpleFirstPersonController>();
        worldFirstPersonController = player.GetComponent<WorldFirstPersonController>();
        pauseSettingsMenu = player.GetComponent<PauseSettingsMenu>();
        if (firstPersonController != null && worldFirstPersonController != null &&
            firstPersonController.enabled && worldFirstPersonController.enabled)
        {
            worldFirstPersonController.enabled = false;
        }

        firstPersonControllerWasEnabled = firstPersonController != null && firstPersonController.enabled;
        worldFirstPersonControllerWasEnabled = worldFirstPersonController != null && worldFirstPersonController.enabled;
        pauseSettingsMenuWasEnabled = pauseSettingsMenu != null && pauseSettingsMenu.enabled;

        characterController = player.GetComponent<CharacterController>();
        var cameraTransform = player.transform.Find("PlayerCamera");
        if (cameraTransform == null)
        {
            cameraTransform = player.transform.Find("FirstPersonCamera");
        }

        playerCamera = cameraTransform != null
            ? cameraTransform.GetComponent<Camera>()
            : Camera.main;
        if (playerCamera != null)
        {
            standingCameraLocalPosition = playerCamera.transform.localPosition;
        }
    }

    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        typingClip = LoadAudioClip("Assets/SFX/TEXT ANIMATION.mp3");
        if (typingClip == null)
        {
            typingClip = LoadAudioClip("Assets/SFX/TEXTANIMATION.mp3");
        }

        notificationClip = LoadAudioClip("Assets/SFX/NOTIFICATION.mp3");
        if (chainsawClip == null)
        {
            chainsawClip = LoadAudioClip("Assets/SFX/CHAINSAW.mp3");
        }

        walkingSfxClip = LoadAudioClip("Assets/Sounds/Walking.mp3");
        jumpSfxClip = LoadAudioClip("Assets/Sounds/jump.mp3");
        clickSfxClip = LoadAudioClip("Assets/Sounds/click.mp3");
        fredHitSfxClip = LoadAudioClip("Assets/Sounds/fred hit.mp3");
        horrorHitSfxClip = LoadAudioClip("Assets/Sounds/horror hit.mp3");
        neighborhoodAmbienceClip = LoadAudioClip("Assets/Sounds/neighbourhood ambiance.mp3");
        liminalAmbienceClip = LoadAudioClip("Assets/Sounds/liminal space ambiance.mp3");
        horrorAmbienceClip = LoadAudioClip("Assets/Sounds/horror ambiance.mp3");
        heartbeatClip = LoadAudioClip("Assets/Sounds/heartbeat.mp3");
        killSfxClip = LoadAudioClip("Assets/Sounds/kill.mp3");

        unknownManGreetingVoiceClip = LoadAudioClip("Assets/UNKNOWN MAN VOICE/HEY-HOW-DID-YOU-GET-IN-THERE.mp3");
        unknownManDeveloperVoiceClip = LoadAudioClip("Assets/UNKNOWN MAN VOICE/YEAH-YOU-CAN'T-SEE-ME-I-AM-YOUR-DEVELOPER.mp3");
        unknownManNothingVoiceClip = LoadAudioClip("Assets/UNKNOWN MAN VOICE/NOTHING.mp3");
        unknownManSetupVoiceClip = LoadAudioClip("Assets/UNKNOWN MAN VOICE/OH,-YOU-DON'T-HAVE-TO-KNOW-THIS.-ITS-A-SETUP-OF.mp3");
        unknownManByeVoiceClip = LoadAudioClip("Assets/UNKNOWN MAN VOICE/OK,-BYE.mp3");

        uiSfxAudioSource = CreateSceneAudioSource("UI SFX", false);
        impactSfxAudioSource = CreateSceneAudioSource("Impact SFX", false);
        uiSfxAudioSource.volume = 1f;
        impactSfxAudioSource.volume = 1f;
        neighborhoodAmbienceSource = CreateSceneAudioSource("Neighbourhood Ambience", true);
        liminalAmbienceSource = CreateSceneAudioSource("Liminal Space Ambience", true);
        horrorAmbienceSource = CreateSceneAudioSource("Horror Ambience", true);
        heartbeatAudioSource = CreateSceneAudioSource("Monster Heartbeat", true);
        unknownManVoiceSource = CreateSceneAudioSource("Unknown Man Voice", false);
        unknownManVoiceSource.volume = 1f;

        ConfigureLoopingAudio(neighborhoodAmbienceSource, neighborhoodAmbienceClip);
        ConfigureLoopingAudio(liminalAmbienceSource, liminalAmbienceClip);
        ConfigureLoopingAudio(horrorAmbienceSource, horrorAmbienceClip);
        ConfigureLoopingAudio(heartbeatAudioSource, heartbeatClip);

        if (firstPersonController != null)
        {
            firstPersonController.ConfigureMovementAudio(walkingSfxClip, jumpSfxClip);
        }
    }

    private AudioSource CreateSceneAudioSource(string sourceName, bool loop)
    {
        var sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        var source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = 0f;
        return source;
    }

    private static void ConfigureLoopingAudio(AudioSource source, AudioClip clip)
    {
        if (source == null)
        {
            return;
        }

        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
    }

    public void PlayUiClick()
    {
        if (uiSfxAudioSource != null && clickSfxClip != null)
        {
            uiSfxAudioSource.pitch = 1.12f;
            uiSfxAudioSource.PlayOneShot(clickSfxClip, 0.42f);
            uiSfxAudioSource.pitch = 1f;
        }
    }

    private void PlayImpact(AudioClip clip, float volume, float pitch = 1f)
    {
        if (impactSfxAudioSource == null || clip == null)
        {
            return;
        }

        impactSfxAudioSource.pitch = pitch;
        impactSfxAudioSource.PlayOneShot(clip, volume);
        impactSfxAudioSource.pitch = 1f;
    }

    private static void FadeLoopingAudio(AudioSource source, float targetVolume, float fadeSeconds)
    {
        if (source == null || source.clip == null)
        {
            return;
        }

        if (targetVolume > 0f && !source.isPlaying)
        {
            source.volume = 0f;
            source.Play();
        }

        float fadeRate = 1f / Mathf.Max(0.05f, fadeSeconds);
        source.volume = Mathf.MoveTowards(source.volume, targetVolume, fadeRate * Time.unscaledDeltaTime);
        if (targetVolume <= 0f && source.volume <= 0.001f && source.isPlaying)
        {
            source.Stop();
            source.volume = 0f;
        }
    }

    private void UpdateSceneAudio()
    {
        if (phase == ScenePhase.OutsideSearch)
        {
            horrorAmbienceJourneyActive = true;
        }

        bool paused = Time.timeScale <= 0f;
        bool inHallway = phase == ScenePhase.HallwayWake ||
                         phase == ScenePhase.HallwayExplore ||
                         phase == ScenePhase.HallwayEndless ||
                         phase == ScenePhase.HallwayPcSpotted ||
                         phase == ScenePhase.HallwayGoToPc ||
                         phase == ScenePhase.HallwayPcDialogue ||
                         phase == ScenePhase.BedroomReturn;
        bool inNeighborhood = phase == ScenePhase.GoHome;

        FadeLoopingAudio(neighborhoodAmbienceSource, !paused && inNeighborhood ? 0.13f : 0f, 1.4f);
        FadeLoopingAudio(liminalAmbienceSource, !paused && inHallway ? 0.12f : 0f, 1.8f);
        FadeLoopingAudio(horrorAmbienceSource, !paused && horrorAmbienceJourneyActive ? 0.19f : 0f, 2.2f);
        FadeLoopingAudio(heartbeatAudioSource, !paused && heartbeatActive ? 0.34f : 0f, 0.8f);
    }

    private void SpawnSceneActors()
    {
        var root = transform;
        var houseObject = GameObject.Find("MC_HOUSE");
        homeHouse = houseObject != null ? houseObject.transform : null;

        fredActor = SpawnFred(root);
        if (homeHouse != null)
        {
            CreateHomeObjectiveMarker(root);
            CreateAuntElaMarker(root);
            CreateHomeScreenMarker(root);
            SpawnHomeCharacters(root);
            FindExistingLaptops();
            FindBedroomDoor();
            BuildBackroomsHallway(root);
            CreateHouseDoorTrigger(root);
            CreateBedroomTrigger(root);
            CreateBathroomSearchTrigger(root);
        }
        else
        {
            Debug.LogError("Scene2EveningManager could not find MC_HOUSE in the loaded world.", this);
        }

        phoneObject = SpawnPhone();
        if (phoneObject != null)
        {
            phoneObject.SetActive(false);
        }

        phoneMessages = player != null ? player.GetComponent<WorldPhoneMessages>() : null;
        if (phoneMessages != null)
        {
            phoneMessages.ConfigureSceneConversation();
        }
        else
        {
            Debug.LogError("Scene2EveningManager could not find the existing WorldPhoneMessages phone system.", this);
        }

    }

    private void SpawnHomeCharacters(Transform parent)
    {
        var momGroundPosition = homeHouse.TransformPoint(new Vector3(-4.46f, 0.11f, 0f));
        var kitchenBench = homeHouse.Find("PFB_KitchenBench");
        var momPosition = kitchenBench != null
            ? new Vector3(kitchenBench.position.x, momGroundPosition.y, kitchenBench.position.z) + homeHouse.right * 1.2f
            : homeHouse.TransformPoint(new Vector3(-3.26f, 0.11f, 0f));
        var momFacing = player != null
            ? player.transform.position - momPosition
            : -(homeHouse.rotation * Vector3.forward);
        momFacing.y = 0f;
        var momRotation = momFacing.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(momFacing.normalized, Vector3.up)
            : homeHouse.rotation * Quaternion.Euler(0f, 180f, 0f);
        momActor = CreateCharacterActor(
            "Mom",
            "Assets/Characters/MOM/mom.glb",
            momPosition,
            momRotation,
            parent,
            2.25f,
            new Color(0.78f, 0.34f, 0.19f),
            out momModelRoot);
        momPlaceholderBody = momModelRoot == null ? momActor.transform.Find("PlaceholderBody") : null;

        var brotherChair = homeHouse.Find("PFB_DiningChair");
        var diningLaptopObject = GameObject.Find("SM_PKS_Laptop_low");
        var brotherScreen = diningLaptopObject != null
            ? FindLaptopScreenRenderer(diningLaptopObject.transform)
            : null;
        var brotherPosition = brotherScreen != null
            ? new Vector3(
                brotherScreen.bounds.center.x,
                brotherChair != null ? brotherChair.position.y : homeHouse.position.y,
                brotherScreen.bounds.center.z) + brotherScreen.transform.forward * 1.35f
            : brotherChair != null
                ? brotherChair.position + brotherChair.forward * 1.35f
                : homeHouse.TransformPoint(new Vector3(-3.11f, 0.13f, 0.08f));
        var tablePosition = homeHouse.TransformPoint(new Vector3(-1.99f, 0.93f, 0.40f));
        var brotherFacing = tablePosition - brotherPosition;
        brotherFacing.y = 0f;
        var brotherRotation = Quaternion.Euler(
            0f,
            brotherChair != null ? brotherChair.eulerAngles.y : homeHouse.eulerAngles.y,
            0f);
        brotherActor = CreateCharacterActor(
            "LittleBrother",
            "Assets/Characters/LIL_BROTHER/lil bro.obj",
            brotherPosition,
            brotherRotation,
            parent,
            1.28f,
            new Color(0.20f, 0.48f, 0.82f),
            out brotherModelRoot);
        brotherPlaceholderBody = brotherModelRoot == null ? brotherActor.transform.Find("PlaceholderBody") : null;
    }

    private GameObject CreateCharacterActor(
        string name,
        string assetPath,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        float targetHeight,
        Color fallbackColor,
        out Transform modelRoot)
    {
        modelRoot = null;
#if UNITY_EDITOR
        var modelAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (modelAsset != null)
        {
            var actor = new GameObject(name);
            actor.transform.SetParent(parent, false);
            actor.transform.SetPositionAndRotation(position, rotation);

            var model = Instantiate(modelAsset, actor.transform);
            model.name = name + "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            Color[] littleBrotherVertexColors = null;
            if (name == "LittleBrother")
            {
                var meshFilter = model.GetComponentInChildren<MeshFilter>();
                var characterRenderer = meshFilter != null ? meshFilter.GetComponent<Renderer>() : null;
                var vertexColorMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/Characters/LIL_BROTHER/LittleBrotherVertexColors.mat");
                littleBrotherVertexColors = meshFilter != null
                    ? LoadObjVertexColors(assetPath, meshFilter.sharedMesh)
                    : null;

                if (meshFilter != null && characterRenderer != null && littleBrotherVertexColors != null && vertexColorMaterial != null)
                {
                    var coloredMesh = Instantiate(meshFilter.sharedMesh);
                    coloredMesh.name = name + "VertexColorMesh";
                    meshFilter.sharedMesh = coloredMesh;
                    characterRenderer.sharedMaterial = vertexColorMaterial;
                }
            }

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                if (bounds.size.y > 0.001f)
                {
                    model.transform.localScale = Vector3.one * (targetHeight / bounds.size.y);
                    GroundActor(actor, position.y);
                    if (littleBrotherVertexColors != null)
                    {
                        var runtimeMeshFilter = model.GetComponentInChildren<MeshFilter>();
                        var runtimeMesh = runtimeMeshFilter.sharedMesh;
                        runtimeMesh.colors = littleBrotherVertexColors;
                        runtimeMeshFilter.sharedMesh = runtimeMesh;
                    }

                    modelRoot = model.transform;
                    return actor;
                }
            }

            Destroy(actor);
        }
#endif
        return CreateHumanPlaceholder(name + "Placeholder", position, rotation, parent, fallbackColor);
    }

    private static Color[] LoadObjVertexColors(string assetPath, Mesh mesh)
    {
#if UNITY_EDITOR
        if (mesh == null)
        {
            return null;
        }

        var objPath = Path.Combine(
            Application.dataPath,
            assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(objPath))
        {
            return null;
        }

        var colorsByPosition = new Dictionary<Vector3, Color>();
        var lines = File.ReadAllLines(objPath);
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("v ", System.StringComparison.Ordinal))
            {
                continue;
            }

            var fields = lines[i].Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 7)
            {
                continue;
            }

            var sourcePosition = new Vector3(
                float.Parse(fields[1], CultureInfo.InvariantCulture),
                float.Parse(fields[2], CultureInfo.InvariantCulture),
                float.Parse(fields[3], CultureInfo.InvariantCulture));
            var sourceColor = new Color(
                float.Parse(fields[4], CultureInfo.InvariantCulture),
                float.Parse(fields[5], CultureInfo.InvariantCulture),
                float.Parse(fields[6], CultureInfo.InvariantCulture),
                1f);
            colorsByPosition[QuantizeVertexColorPosition(new Vector3(-sourcePosition.x, sourcePosition.y, sourcePosition.z))] = sourceColor;
        }

        var meshVertices = mesh.vertices;
        var colors = new Color[meshVertices.Length];
        for (int i = 0; i < meshVertices.Length; i++)
        {
            if (!colorsByPosition.TryGetValue(QuantizeVertexColorPosition(meshVertices[i]), out colors[i]))
            {
                return null;
            }
        }

        return colors;
#else
        return null;
#endif
    }

    private static Vector3 QuantizeVertexColorPosition(Vector3 position)
    {
        const float precision = 100000f;
        return new Vector3(
            Mathf.Round(position.x * precision),
            Mathf.Round(position.y * precision),
            Mathf.Round(position.z * precision));
    }

    private void FindExistingLaptops()
    {
        bedroomBed = homeHouse.Find("PFB_Bed");
        bedroomLaptop = GameObject.Find("SM_PKS_Laptop_low (1)")?.transform;
        diningLaptop = GameObject.Find("SM_PKS_Laptop_low")?.transform;

        if (bedroomBed == null)
        {
            Debug.LogError("Scene2EveningManager could not find the existing MC_HOUSE bed.", this);
        }

        if (bedroomLaptop == null)
        {
            Debug.LogError("Scene2EveningManager could not find the existing bedroom laptop.", this);
        }
        else
        {
            bedroomLaptopScreenRenderer = FindLaptopScreenRenderer(bedroomLaptop);
            placeholderLaptop = bedroomLaptop;
            placeholderLaptopScreenRenderer = bedroomLaptopScreenRenderer;
            CreateLaptopImagePlaceholder();
        }

        if (diningLaptop == null)
        {
            Debug.LogWarning("Scene2EveningManager could not find the existing dining laptop.", this);
        }
        else
        {
            var screenRenderer = FindLaptopScreenRenderer(diningLaptop);
            diningLaptopScreenLid = screenRenderer != null ? screenRenderer.transform : null;
            if (diningLaptopScreenLid != null)
            {
                diningLaptopScreenOpenRotation = diningLaptopScreenLid.localRotation;
            }
        }
    }

    private void CreateHouseDoorTrigger(Transform parent)
    {
        var door = homeHouse.Find("PFB_Building_Base/PFB_DoorDouble");
        if (door == null)
        {
            Debug.LogError("Scene2EveningManager could not find the MC_HOUSE entrance door.", this);
            return;
        }

        houseEntranceDoor = door;
        houseEntranceDoorInteraction = door.GetComponent<MoveObjectController>();
        if (houseEntranceDoorInteraction == null)
        {
            houseEntranceDoorInteraction = door.GetComponentInChildren<MoveObjectController>(true);
        }
        var doorAnimators = door.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < doorAnimators.Length; i++)
        {
            var parameters = doorAnimators[i].parameters;
            for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
            {
                if (parameters[parameterIndex].type == AnimatorControllerParameterType.Bool &&
                    parameters[parameterIndex].name.StartsWith("isOpen_Obj_"))
                {
                    houseEntranceDoorAnimator = doorAnimators[i];
                    houseEntranceDoorOpenParameter = parameters[parameterIndex].name;
                    break;
                }
            }

            if (houseEntranceDoorAnimator != null)
            {
                break;
            }
        }

        houseTrigger = CreateTrigger(
            "HouseEntranceTrigger",
            door.position - Vector3.up * 1.25f + homeHouse.forward * 0.15f,
            new Vector3(2.4f, 2.5f, 0.9f),
            parent);
        houseTrigger.transform.rotation = door.rotation;
    }

    private void CreateBedroomTrigger(Transform parent)
    {
        if (bedroomBed == null)
        {
            return;
        }

        bedroomTrigger = CreateTrigger(
            "BedroomTrigger",
            bedroomBed.position,
            new Vector3(7f, 2.8f, 7f),
            parent);
        bedroomTrigger.transform.rotation = bedroomBed.rotation;
    }

    private void CreateBathroomSearchTrigger(Transform parent)
    {
        bathroomDoor = homeHouse.Find("PFB_Building_Base/PFB_DoorSingle_Open (1)");
        if (bathroomDoor == null)
        {
            Debug.LogWarning("Scene2EveningManager could not find the existing bathroom door for the search trigger.", this);
            return;
        }

        var bathroomSink = homeHouse.Find("PFB_BathroomSink");
        var interiorDirection = bathroomSink != null
            ? bathroomSink.position - bathroomDoor.position
            : -bathroomDoor.forward;
        interiorDirection.y = 0f;
        if (interiorDirection.sqrMagnitude < 0.001f)
        {
            interiorDirection = -bathroomDoor.forward;
        }

        // Cover the whole bathroom interior so the player only needs to be anywhere inside the
        // room to spot the shadow, without the box spilling out into the hallway. The box starts
        // just inside the doorway and extends to the far wall behind the sink.
        float bathroomDepth = 3.2f;
        var triggerCenter = bathroomDoor.position + interiorDirection.normalized * (bathroomDepth * 0.5f);
        triggerCenter.y = bathroomDoor.position.y + 0.6f;
        bathroomTrigger = CreateTrigger(
            "BathroomSearchTrigger",
            triggerCenter,
            new Vector3(2.4f, 2.4f, bathroomDepth),
            parent);
        // Face the trigger into the actual bathroom. The sink may sit along the house's local X
        // axis, so using the house rotation alone can leave the trigger box running through a wall.
        bathroomTrigger.transform.rotation = Quaternion.LookRotation(interiorDirection.normalized, Vector3.up);

        var bathroomBox = bathroomTrigger.GetComponent<BoxCollider>();
        if (bathroomBox != null)
        {
            bathroomBox.size = new Vector3(2.4f, 2.4f, bathroomDepth);
        }
    }

    private Transform FindBedroomWindowCurtain()
    {
        if (homeHouse == null || bedroomBed == null)
        {
            return null;
        }

        var transforms = homeHouse.GetComponentsInChildren<Transform>(true);
        Transform nearestCurtain = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < transforms.Length; i++)
        {
            if (!transforms[i].name.StartsWith("PFB_Curtain", System.StringComparison.Ordinal) ||
                transforms[i].name == "PFB_CurtainSmall")
            {
                continue;
            }

            float distance = HorizontalDistance(transforms[i].position, bedroomBed.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestCurtain = transforms[i];
            }
        }

        return nearestCurtain;
    }

    private Vector3 GetCurtainOutsideDirection(Transform curtain)
    {
        var outsideDirection = curtain != null && homeHouse != null
            ? curtain.position - homeHouse.position
            : Vector3.zero;
        outsideDirection.y = 0f;
        return outsideDirection.sqrMagnitude > 0.001f
            ? outsideDirection.normalized
            : Vector3.forward;
    }

    private Vector3 GetBathroomWindowFocusPoint()
    {
        if (homeHouse == null)
        {
            return Vector3.zero;
        }

        // Called twice per frame during the outside-search phase; the window does not move, so
        // resolve its name-based lookup once instead of hitting the house hierarchy every frame.
        if (bathroomFocusResolved)
        {
            return bathroomFocusPoint;
        }

        ResolveBathroomAnchors();
        var curtain = bathroomWindowCurtain;
        if (curtain == null)
        {
            return homeHouse.TransformPoint(new Vector3(-4.9f, 1.9f, -6.7f));
        }

        var renderer = curtain.GetComponentInChildren<Renderer>();
        var center = renderer != null ? renderer.bounds.center : curtain.position;
        var sink = bathroomWindowSink;
        var outsideDirection = sink != null ? curtain.position - sink.position : curtain.position - homeHouse.position;
        outsideDirection.y = 0f;
        if (outsideDirection.sqrMagnitude > 0.001f)
        {
            center += outsideDirection.normalized * 0.45f;
        }

        bathroomFocusPoint = center;
        bathroomFocusResolved = true;
        return center;
    }

    private void ResolveBathroomAnchors()
    {
        if (bathroomAnchorsResolved || homeHouse == null)
        {
            return;
        }

        bathroomAnchorsResolved = true;
        bathroomWindowCurtain = homeHouse.Find("PFB_Building_Base/PFB_CurtainSmall");
        bathroomWindowSink = homeHouse.Find("PFB_BathroomSink");
    }

    // Scene 1's additive dream cutscene uses the authored bedroom already present in MC_HOUSE.
    public Transform RepeatCutsceneBedroomBed => bedroomBed;
    public Transform RepeatCutsceneBedroomDoor => bedroomDoor;
    public Vector3 RepeatCutsceneBedroomDoorwayCenter => bedroomDoorwayCenter;
    public Vector3 RepeatCutsceneBedroomDoorwayNormal => bedroomDoorwayNormal;

    public void OpenBedroomForRepeatCutscene()
    {
        SetBedroomDoorOpen(true);
        if (bedroomDoorInteraction != null) bedroomDoorInteraction.enabled = false;
    }

    private void FindBedroomDoor()
    {
        if (homeHouse == null || bedroomBed == null)
        {
            Debug.LogError("Scene2EveningManager could not locate the bedroom door because the house or bed is missing.", this);
            return;
        }

        var animators = homeHouse.GetComponentsInChildren<Animator>(true);
        float bestBedroomDoorScore = float.NegativeInfinity;
        var kitchenDirection = GetKitchenFocusPoint() - bedroomBed.position;
        kitchenDirection.y = 0f;
        kitchenDirection = kitchenDirection.sqrMagnitude > 0.001f
            ? kitchenDirection.normalized
            : Vector3.forward;
        for (int i = 0; i < animators.Length; i++)
        {
            var candidate = animators[i];
            if (!candidate.gameObject.name.StartsWith("PFB_DoorSingle_Open"))
            {
                continue;
            }

            var candidateInteraction = candidate.GetComponent<MoveObjectController>();
            if (candidateInteraction == null)
            {
                candidateInteraction = candidate.GetComponentInChildren<MoveObjectController>(true);
            }

            if (candidateInteraction == null)
            {
                continue;
            }

            string openParameter = null;
            var parameters = candidate.parameters;
            for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
            {
                if (parameters[parameterIndex].type == AnimatorControllerParameterType.Bool &&
                    parameters[parameterIndex].name.StartsWith("isOpen_Obj_"))
                {
                    openParameter = parameters[parameterIndex].name;
                    break;
                }
            }

            if (string.IsNullOrEmpty(openParameter))
            {
                continue;
            }

            var doorDirection = candidate.transform.position - bedroomBed.position;
            doorDirection.y = 0f;
            if (doorDirection.sqrMagnitude < 0.001f)
            {
                continue;
            }

            float exitAlignment = Vector3.Dot(doorDirection.normalized, kitchenDirection);
            float distanceFromBed = HorizontalDistance(candidate.transform.position, bedroomBed.position);
            float bedroomDoorScore = exitAlignment - distanceFromBed * 0.15f;
            if (exitAlignment < 0.15f || bedroomDoorScore <= bestBedroomDoorScore)
            {
                continue;
            }

            bestBedroomDoorScore = bedroomDoorScore;
            bedroomDoor = candidate.transform;
            bedroomDoorAnimator = candidate;
            bedroomDoorInteraction = candidateInteraction;
            bedroomDoorOpenParameter = openParameter;
        }

        if (bedroomDoorAnimator == null)
        {
            Debug.LogError("Scene2EveningManager could not find an interactable single door for the bedroom.", this);
            return;
        }

        var doorwayRenderer = bedroomDoor.GetComponentInChildren<Renderer>(true);
        bedroomDoorwayCenter = doorwayRenderer != null ? doorwayRenderer.bounds.center : bedroomDoor.position;
        bedroomDoorwayCenter.y = bedroomDoor.position.y;
        var doorwayBounds = doorwayRenderer != null ? doorwayRenderer.bounds : new Bounds(bedroomDoor.position, Vector3.zero);
        bedroomDoorwayNormal = doorwayBounds.size.x > doorwayBounds.size.z
            ? bedroomDoor.TransformDirection(Vector3.forward)
            : bedroomDoor.TransformDirection(Vector3.right);
        bedroomDoorwayNormal.y = 0f;
        if (bedroomDoorwayNormal.sqrMagnitude > 0.001f) bedroomDoorwayNormal.Normalize();
    }

    private void SetBedroomDoorOpen(bool isOpen)
    {
        if (bedroomDoor == null || bedroomDoorAnimator == null ||
            string.IsNullOrEmpty(bedroomDoorOpenParameter))
        {
            Debug.LogError("Scene2EveningManager cannot set the bedroom door because its open control is missing.", this);
            return;
        }

        bedroomDoorAnimator.enabled = true;
        bedroomDoorAnimator.speed = 1f;
        bedroomDoorAnimator.SetBool(bedroomDoorOpenParameter, isOpen);
        // The asset controller's default Idle state has no transition into Open.
        // Start the authored clip directly so setting the bool actually moves the panel.
        bedroomDoorAnimator.Play(isOpen ? "DoorSingle_Open" : "DoorSingle_Close", 0, 0f);
        bedroomDoorAnimator.Update(0f);
    }

    private bool IsBedroomDoorInReach()
    {
        if (player == null || playerCamera == null || bedroomDoor == null)
        {
            return false;
        }

        // Use a forgiving proximity check: the door panel/trim colliders often
        // intercept the camera ray, making the old look-at interaction unreliable.
        return Vector3.Distance(player.transform.position, bedroomDoor.position) <= 4.5f;
    }

    private void CreateHomeObjectiveMarker(Transform parent)
    {
        houseObjectiveMarker = new GameObject("HomeObjectiveMarker");
        houseObjectiveMarker.transform.SetParent(parent, false);

        var renderers = homeHouse.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogError("MC_HOUSE has no renderers; cannot place the home objective marker.", this);
            houseObjectiveMarker.SetActive(false);
            return;
        }

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        houseObjectiveMarker.transform.position = new Vector3(bounds.center.x, bounds.max.y + 9f, bounds.center.z);
        var markerMaterial = CreateEmissiveMaterial(new Color(1f, 0.66f, 0.08f));
        CreateMarkerPart("ArrowShaft", PrimitiveType.Cylinder, houseObjectiveMarker.transform,
            new Vector3(0f, -1.15f, 0f), Vector3.one, new Vector3(0.16f, 1.15f, 0.16f), markerMaterial);
        CreateMarkerPart("ArrowHeadLeft", PrimitiveType.Cube, houseObjectiveMarker.transform,
            new Vector3(-0.32f, -2.30f, 0f), new Vector3(0f, 0f, -45f), new Vector3(0.14f, 0.72f, 0.14f), markerMaterial);
        CreateMarkerPart("ArrowHeadRight", PrimitiveType.Cube, houseObjectiveMarker.transform,
            new Vector3(0.32f, -2.30f, 0f), new Vector3(0f, 0f, 45f), new Vector3(0.14f, 0.72f, 0.14f), markerMaterial);
        houseObjectiveMarker.SetActive(false);
    }

    // Builds the yellow location pin shown for Aunt Ela's house.
    private void CreateAuntElaMarker(Transform parent)
    {
        auntElaMarker = CreateScreenMarker(parent, "AuntElaMarker");
        auntElaMarker.WorldPosition = ResolveAuntElaPosition();
    }

    // Builds the yellow location pin shown for the player's own home at the start of the scene.
    private void CreateHomeScreenMarker(Transform parent)
    {
        homeMarker = CreateScreenMarker(parent, "HomeMarker");
        homeMarker.WorldPosition = homeHouse != null ? homeHouse.position : Vector3.zero;
    }

    private ScreenMarker CreateScreenMarker(Transform parent, string objectName)
    {
        var marker = new ScreenMarker();

        marker.Root = new GameObject(objectName);
        marker.Root.transform.SetParent(parent, false);

        var canvas = marker.Root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        marker.Root.AddComponent<GraphicRaycaster>();

        var scaler = marker.Root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var iconObject = new GameObject("LocationPin");
        iconObject.transform.SetParent(marker.Root.transform, false);
        var iconRect = iconObject.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(auntElaMarkerScreenSize, auntElaMarkerScreenSize);

        var image = iconObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.sprite = CreateLocationPinSprite();
        image.color = Color.white;
        marker.Icon = iconRect;

        var labelObject = new GameObject("DistanceLabel");
        labelObject.transform.SetParent(marker.Root.transform, false);
        var labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.sizeDelta = new Vector2(160f, 34f);

        var text = labelObject.AddComponent<TextMeshProUGUI>();
        text.text = "0 m";
        text.fontSize = 24f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.84f, 0.1f);
        text.outlineWidth = 0.25f;
        text.outlineColor = Color.black;
        text.raycastTarget = false;
        marker.DistanceText = text;
        marker.DistanceLabel = labelRect;

        marker.Root.SetActive(false);
        return marker;
    }

    // Projects a marker's world position onto the screen, moves its pin and updates the distance.
    private void UpdateScreenMarker(ScreenMarker marker)
    {
        if (marker == null || marker.Root == null || !marker.Root.activeSelf || marker.Icon == null ||
            playerCamera == null)
        {
            return;
        }

        var screenPoint = playerCamera.WorldToScreenPoint(marker.WorldPosition + Vector3.up * 1.6f);

        // Behind the camera, or off screen: show the pin at the screen edge pointing that way.
        if (screenPoint.z < 0f)
        {
            screenPoint.x = Screen.width - screenPoint.x;
            screenPoint.y = Screen.height - screenPoint.y;
        }

        float margin = auntElaMarkerScreenSize * 0.5f + 8f;
        screenPoint.x = Mathf.Clamp(screenPoint.x, margin, Screen.width - margin);
        screenPoint.y = Mathf.Clamp(screenPoint.y, margin, Screen.height - margin);

        marker.Icon.position = new Vector3(screenPoint.x, screenPoint.y, 0f);
        marker.Icon.sizeDelta = new Vector2(auntElaMarkerScreenSize, auntElaMarkerScreenSize);

        // Distance counter sits just below the pin and always shows the straight-line metres to the
        // target, measured on the ground plane so height differences do not skew it.
        if (player != null && marker.DistanceText != null)
        {
            var toTarget = marker.WorldPosition - player.transform.position;
            toTarget.y = 0f;
            marker.DistanceText.text = Mathf.RoundToInt(toTarget.magnitude) + " m";
        }

        if (marker.DistanceLabel != null)
        {
            marker.DistanceLabel.position =
                new Vector3(screenPoint.x, screenPoint.y - auntElaMarkerScreenSize * 0.5f - 12f, 0f);
        }
    }

    private void SetScreenMarkerVisible(ScreenMarker marker, bool visible, Vector3 worldPosition)
    {
        if (marker == null || marker.Root == null)
        {
            return;
        }

        if (visible)
        {
            marker.WorldPosition = worldPosition;
        }

        marker.Root.SetActive(visible);
    }

    // Generates the yellow teardrop map-pin sprite used by the on-screen marker.
    private static Sprite CreateLocationPinSprite()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "AuntElaLocationPin",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color yellow = new Color(1f, 0.84f, 0.1f, 1f);
        Color darkYellow = new Color(0.78f, 0.58f, 0.02f, 1f);
        var pixels = new Color[size * size];

        Vector2 headCenter = new Vector2(size * 0.5f, size * 0.63f);
        float headRadius = size * 0.29f;
        Vector2 tip = new Vector2(size * 0.5f, size * 0.06f);
        float holeRadius = size * 0.115f;
        Vector2 holeCenter = new Vector2(headCenter.x, headCenter.y - headRadius * 0.05f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = Vector2.Distance(point, headCenter) <= headRadius;

                if (!inside)
                {
                    Vector2 left = headCenter + new Vector2(-headRadius * 0.78f, -headRadius * 0.62f);
                    Vector2 right = headCenter + new Vector2(headRadius * 0.78f, -headRadius * 0.62f);
                    float d1 = Sign(point, left, right);
                    float d2 = Sign(point, right, tip);
                    float d3 = Sign(point, tip, left);
                    bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
                    bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
                    inside = !(hasNeg && hasPos);
                }

                if (!inside || Vector2.Distance(point, holeCenter) <= holeRadius)
                {
                    pixels[y * size + x] = clear;
                    continue;
                }

                float shade = Mathf.InverseLerp(size * 0.1f, size * 0.7f, y);
                pixels[y * size + x] = Color.Lerp(darkYellow, yellow, shade);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static float Sign(Vector2 a, Vector2 b, Vector2 c)
    {
        return (a.x - c.x) * (b.y - c.y) - (b.x - c.x) * (a.y - c.y);
    }

    private Vector3 ResolveAuntElaPosition()
    {
        // An explicit override in the Inspector always wins.
        if (auntElaHousePosition.sqrMagnitude > 0.001f)
        {
            return auntElaHousePosition;
        }

        // Otherwise locate Aunt Ela's house in the scene and use its real world position. The
        // (0, 0.42, 0) seen in the Inspector for PFB_Building_Base is a local offset, which is why
        // using it directly placed the marker in the wrong spot.
        var auntElaHouse = FindAuntElaHouseTransform();
        if (auntElaHouse != null)
        {
            return auntElaHouse.position;
        }

        return Vector3.zero;
    }

    // Finds the scene object that represents Aunt Ela's house. The world has a dedicated object
    // literally named "Aunt_Ela_House" at the far end of the road (around z = 250), so we look for
    // that by name first. Only if it is missing do we fall back to a generic PFB_Building_Base,
    // and in that case we pick the FARTHEST one from the player's home so we never lock onto the
    // neighbour's house next door.
    private Transform FindAuntElaHouseTransform()
    {
        // This is reached from per-frame story checks (jungle chase, Aunt Ela approach). Scanning
        // every Transform in the loaded world each frame was the single biggest frame-time spike,
        // so the result is cached; a destroyed (unloaded) house is detected by the typed null check.
        if (auntElaHouseResolved != null)
        {
            return auntElaHouseResolved;
        }

        if (Time.unscaledTime < nextAuntElaHouseSearchTime)
        {
            return null;
        }

        var candidates = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // 1) Preferred: the dedicated Aunt Ela house object by name.
        for (int i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (candidate != null && candidate.name == "Aunt_Ela_House")
            {
                auntElaHouseResolved = candidate;
                return candidate;
            }
        }

        // 2) Fallback: the PFB_Building_Base farthest from the player's own house.
        Transform best = null;
        float bestDistance = -1f;
        var homeOrigin = homeHouse != null ? homeHouse.position : Vector3.zero;
        for (int i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (candidate == null || candidate.name != "PFB_Building_Base")
            {
                continue;
            }

            // Skip the player's own house, which also contains a PFB_Building_Base child.
            if (homeHouse != null && (candidate == homeHouse || candidate.IsChildOf(homeHouse)))
            {
                continue;
            }

            var offset = candidate.position - homeOrigin;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        auntElaHouseResolved = best;
        if (best == null)
        {
            // World_Master may not have finished loading yet; retry later instead of every frame.
            nextAuntElaHouseSearchTime = Time.unscaledTime + 2f;
        }

        return best;
    }

    // True when the player is actually standing on the MainRoad strip (a long, narrow band that
    // runs along the Z axis at x ~= 0). Used so the jungle ambush only fires once the player has
    // genuinely stepped onto the road, not while they are still in the yard.
    private bool IsPlayerOnMainRoad()
    {
        if (player == null)
        {
            return false;
        }

        var road = ResolveMainRoadTransform();
        if (road == null)
        {
            // No road object in the scene: fall back to the generic "outside the house" test.
            return IsPlayerOnRoad();
        }

        var roadPos = road.position;
        var roadScale = road.lossyScale;
        var playerPos = player.transform.position;

        // Road is centred on its transform and spans its local X/Z by the scale. Allow a small
        // margin so the trigger is forgiving right at the kerb.
        const float halfWidth = 4.5f;
        var halfLength = Mathf.Max(10f, roadScale.z * 0.5f);

        var dx = Mathf.Abs(playerPos.x - roadPos.x);
        var dz = Mathf.Abs(playerPos.z - roadPos.z);
        return dx <= Mathf.Max(halfWidth, roadScale.x * 0.5f + 1.5f) && dz <= halfLength;
    }

    private Transform ResolveMainRoadTransform()
    {
        if (mainRoad != null)
        {
            return mainRoad;
        }

        var roadObject = GameObject.Find("MainRoad");
        if (roadObject == null)
        {
            var roadObjects = GameObject.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < roadObjects.Length; i++)
            {
                if (roadObjects[i] != null && roadObjects[i].name == "MainRoad")
                {
                    roadObject = roadObjects[i].gameObject;
                    break;
                }
            }
        }

        mainRoad = roadObject != null ? roadObject.transform : null;
        return mainRoad;
    }

    private void SetAuntElaMarkerVisible(bool visible)
    {
        // Re-resolve the target when it becomes visible: the house may only exist in the scene by
        // now, and this also picks up the Inspector override if it was changed at runtime.
        SetScreenMarkerVisible(auntElaMarker, visible, ResolveAuntElaPosition());
    }

    private void SetHomeMarkerVisible(bool visible)
    {
        SetScreenMarkerVisible(homeMarker, visible, homeHouse != null ? homeHouse.position : Vector3.zero);
    }

    private static Material CreateEmissiveMaterial(Color color)
    {
        var material = new Material(GetPlaceholderShader());
        material.color = color;
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.8f);
        }

        return material;
    }

    private static void CreateMarkerPart(
        string name,
        PrimitiveType primitive,
        Transform parent,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale,
        Material material)
    {
        var part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.Euler(localEulerAngles);
        part.transform.localScale = localScale;
        var collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        var renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private GameObject SpawnFred(Transform parent)
    {
        var spawnForward = playerCamera != null
            ? playerCamera.transform.forward
            : player != null ? player.transform.forward : Vector3.forward;
        spawnForward.y = 0f;
        if (spawnForward.sqrMagnitude < 0.001f)
        {
            spawnForward = player != null ? player.transform.forward : Vector3.forward;
            spawnForward.y = 0f;
        }

        spawnForward.Normalize();
        var playerPosition = player != null ? player.transform.position : Vector3.zero;
        var spawnPosition = playerPosition + spawnForward * 2.5f;
        var groundY = ResolveGroundHeight(spawnPosition);
        spawnPosition.y = groundY;
        var facingPlayer = playerPosition - spawnPosition;
        facingPlayer.y = 0f;
        var spawnRotation = facingPlayer.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(facingPlayer.normalized, Vector3.up)
            : Quaternion.identity;

#if UNITY_EDITOR
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/FRED/fred.glb");
        if (asset == null)
        {
            return CreateHumanPlaceholder("Fred", spawnPosition, spawnRotation, parent, new Color(0.75f, 0.67f, 0.56f));
        }

        var instance = Instantiate(asset, spawnPosition, spawnRotation, parent);
        instance.name = "Fred";
        instance.transform.localScale = Vector3.one * 1.08f;
        GroundActor(instance, groundY);
        return instance;
#else
        return CreateHumanPlaceholder("Fred", spawnPosition, spawnRotation, parent, new Color(0.75f, 0.67f, 0.56f));
#endif
    }

    private float ResolveGroundHeight(Vector3 position)
    {
        return ResolveGroundHeight(position, float.NegativeInfinity);
    }

    // maxY lets callers reject hits that come from above the intended floor (e.g. a roof or
    // ceiling collider caught by the downward ray). Without it, ground placed indoors resolves
    // to the roof when the ray is cast from outside the building.
    private float ResolveGroundHeight(Vector3 position, float maxY)
    {
        var rayOrigin = position + Vector3.up * 5f;
        var hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            20f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            float hitY = hits[i].point.y;
            if (hitY <= maxY && hitY > best)
            {
                best = hitY;
            }
        }

        if (best > float.NegativeInfinity)
        {
            return best;
        }

        if (maxY > float.NegativeInfinity)
        {
            return maxY;
        }

        return characterController != null
            ? characterController.bounds.min.y
            : player != null ? player.transform.position.y : position.y;
    }

    private static void GroundActor(GameObject actor, float groundY)
    {
        var renderers = actor.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        actor.transform.position += Vector3.up * (groundY - bounds.min.y);
    }

    private GameObject SpawnPhone()
    {
        // The phone is rendered as a compact screen-space device by WorldPhoneMessages.
        // Do not spawn the exploratory GLB prop into the gameplay camera.
        return null;
    }

    private GameObject CreateHumanPlaceholder(string name, Vector3 position, Quaternion rotation, Transform parent, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = rotation;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "PlaceholderBody";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = Vector3.up * 0.9f;
        body.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);

        var bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            var material = new Material(GetPlaceholderShader());
            material.color = color;
            bodyRenderer.sharedMaterial = material;
        }

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.95f, 0f);
        head.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);

        var headRenderer = head.GetComponent<Renderer>();
        if (headRenderer != null)
        {
            var skin = new Material(GetPlaceholderShader());
            skin.color = new Color(0.95f, 0.86f, 0.78f);
            headRenderer.sharedMaterial = skin;
        }

        return go;
    }

    private static Shader GetPlaceholderShader()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        return shader != null ? shader : Shader.Find("Standard");
    }

    // Unity UI buttons (like RETRY) only receive clicks when a scene EventSystem exists with an
    // input module. The story scenes were shipped without one, so create it on demand. The project
    // uses the new Input System, so the UI module must be InputSystemUIInputModule.
    private void EnsureEventSystem()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("Scene2EventSystem");
            eventSystemObject.transform.SetParent(transform, false);
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        // The EventSystem is useless without an input module, so make sure one exists.
#if ENABLE_INPUT_SYSTEM
        var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null && eventSystem.GetComponent<StandaloneInputModule>() == null)
        {
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        // A freshly added module starts with no actions bound, so wire up Unity's defaults
        // otherwise the RETRY button would render but never receive a click.
        if (inputModule != null && inputModule.actionsAsset == null)
        {
            inputModule.AssignDefaultActions();
        }
#else
        if (eventSystem.GetComponent<StandaloneInputModule>() == null &&
            eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }
#endif
    }

    private GameObject CreateTrigger(string name, Vector3 position, Vector3 size, Transform parent)
    {
        var trigger = new GameObject(name);
        trigger.transform.SetParent(parent, false);
        trigger.transform.position = position;
        var collider = trigger.AddComponent<BoxCollider>();
        collider.size = size;
        collider.isTrigger = true;
        return trigger;
    }

    private void SetupUi()
    {
        EnsureEventSystem();

        dialogueCanvasObject = new GameObject("Scene2DialogueCanvas");
        dialogueCanvasObject.transform.SetParent(transform, false);

        var canvas = dialogueCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var canvasScaler = dialogueCanvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;
        dialogueCanvasObject.AddComponent<GraphicRaycaster>();

        objectiveBlock = new GameObject("ObjectiveBlock");
        objectiveBlock.transform.SetParent(dialogueCanvasObject.transform, false);
        var objectiveRect = objectiveBlock.AddComponent<RectTransform>();
        objectiveRect.anchorMin = new Vector2(1f, 1f);
        objectiveRect.anchorMax = new Vector2(1f, 1f);
        objectiveRect.pivot = new Vector2(1f, 1f);
        objectiveRect.anchoredPosition = new Vector2(-28f, -30f);
        objectiveRect.sizeDelta = new Vector2(380f, 64f);
        var objectiveBackground = objectiveBlock.AddComponent<Image>();
        objectiveBackground.color = new Color(0.025f, 0.035f, 0.05f, 0.78f);
        objectiveBackground.raycastTarget = false;
        objectiveCanvasGroup = objectiveBlock.AddComponent<CanvasGroup>();
        objectiveCanvasGroup.alpha = 1f;
        objectiveCanvasGroup.interactable = false;
        objectiveCanvasGroup.blocksRaycasts = false;
        var objectiveCanvas = objectiveBlock.AddComponent<Canvas>();
        objectiveCanvas.overrideSorting = true;
        objectiveCanvas.sortingOrder = 220;

        var objectiveHeaderObject = new GameObject("ObjectiveHeader");
        objectiveHeaderObject.transform.SetParent(objectiveBlock.transform, false);
        objectiveHeaderText = objectiveHeaderObject.AddComponent<TextMeshProUGUI>();
        objectiveHeaderText.text = "OBJECTIVE";
        objectiveHeaderText.fontSize = 13f;
        objectiveHeaderText.fontStyle = FontStyles.Bold;
        objectiveHeaderText.characterSpacing = 1.5f;
        objectiveHeaderText.alignment = TextAlignmentOptions.TopLeft;
        objectiveHeaderText.color = new Color(1f, 0.76f, 0.35f, 1f);
        objectiveHeaderText.rectTransform.anchorMin = new Vector2(0f, 1f);
        objectiveHeaderText.rectTransform.anchorMax = new Vector2(1f, 1f);
        objectiveHeaderText.rectTransform.pivot = new Vector2(0f, 1f);
        objectiveHeaderText.rectTransform.anchoredPosition = Vector2.zero;
        objectiveHeaderText.rectTransform.sizeDelta = new Vector2(0f, 18f);
        objectiveHeaderText.gameObject.SetActive(false);

        var objectiveTextObject = new GameObject("ObjectiveText");
        objectiveTextObject.transform.SetParent(objectiveBlock.transform, false);
        objectiveText = objectiveTextObject.AddComponent<TextMeshProUGUI>();
        objectiveText.fontSize = 23f;
        objectiveText.textWrappingMode = TextWrappingModes.Normal;
        objectiveText.enableAutoSizing = true;
        objectiveText.fontSizeMin = 18f;
        objectiveText.fontSizeMax = 23f;
        objectiveText.overflowMode = TextOverflowModes.Truncate;
        objectiveText.alignment = TextAlignmentOptions.MidlineLeft;
        objectiveText.color = Color.white;
        objectiveText.rectTransform.anchorMin = Vector2.zero;
        objectiveText.rectTransform.anchorMax = Vector2.one;
        objectiveText.rectTransform.pivot = new Vector2(0f, 1f);
        objectiveText.rectTransform.offsetMin = new Vector2(16f, 10f);
        objectiveText.rectTransform.offsetMax = new Vector2(-16f, -10f);
        objectiveBlock.SetActive(false);

        var phonePromptObject = new GameObject("PhonePrompt");
        phonePromptObject.transform.SetParent(dialogueCanvasObject.transform, false);
        phonePromptText = phonePromptObject.AddComponent<TextMeshProUGUI>();
        phonePromptText.fontSize = 18f;
        phonePromptText.fontStyle = FontStyles.Bold;
        phonePromptText.alignment = TextAlignmentOptions.Center;
        phonePromptText.color = Color.white;
        phonePromptText.raycastTarget = false;
        phonePromptText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        phonePromptText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        phonePromptText.rectTransform.pivot = new Vector2(0.5f, 0f);
        phonePromptText.rectTransform.anchoredPosition = new Vector2(0f, 88f);
        phonePromptText.rectTransform.sizeDelta = new Vector2(440f, 34f);
        phonePromptObject.SetActive(false);

        dialoguePanelObject = new GameObject("DialoguePanel");
        dialoguePanelObject.transform.SetParent(dialogueCanvasObject.transform, false);
        var panelRect = dialoguePanelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(-80f, 78f);
        panelRect.sizeDelta = new Vector2(480f, 0f);

        var layout = dialoguePanelObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 8, 8);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var panelSize = dialoguePanelObject.AddComponent<ContentSizeFitter>();
        panelSize.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        panelSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var speakerObj = new GameObject("Speaker");
        speakerObj.transform.SetParent(dialoguePanelObject.transform, false);
        speakerText = speakerObj.AddComponent<TextMeshProUGUI>();
        speakerText.fontSize = 30f;
        speakerText.fontStyle = FontStyles.Bold;
        speakerText.alignment = TextAlignmentOptions.Left;
        speakerText.color = Color.white;
        speakerText.outlineWidth = 0.25f;
        speakerText.outlineColor = Color.black;
        speakerText.richText = true;
        speakerText.textWrappingMode = TextWrappingModes.NoWrap;
        speakerText.raycastTarget = false;
        var speakerShadow = speakerObj.AddComponent<Shadow>();
        speakerShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        speakerShadow.effectDistance = new Vector2(2f, -2f);
        speakerShadow.useGraphicAlpha = true;
        var speakerLayout = speakerObj.AddComponent<LayoutElement>();
        speakerLayout.minHeight = 36f;
        speakerLayout.preferredHeight = 36f;
        speakerLayout.flexibleHeight = 0f;

        var dialogueObj = new GameObject("Dialogue");
        dialogueObj.transform.SetParent(dialoguePanelObject.transform, false);
        dialogueText = dialogueObj.AddComponent<TextMeshProUGUI>();
        dialogueText.fontSize = 34f;
        dialogueText.alignment = TextAlignmentOptions.Left;
        dialogueText.color = new Color(0.97f, 0.97f, 0.95f, 1f);
        dialogueText.outlineWidth = 0.22f;
        dialogueText.outlineColor = Color.black;
        dialogueText.richText = true;
        dialogueText.textWrappingMode = TextWrappingModes.Normal;
        dialogueText.overflowMode = TextOverflowModes.Overflow;
        dialogueText.margin = new Vector4(8f, 3f, 8f, 3f);
        dialogueText.raycastTarget = false;
        var dialogueShadow = dialogueObj.AddComponent<Shadow>();
        dialogueShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        dialogueShadow.effectDistance = new Vector2(2f, -2f);
        dialogueShadow.useGraphicAlpha = true;
        var dialogueSize = dialogueObj.AddComponent<ContentSizeFitter>();
        dialogueSize.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        dialogueSize.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var promptObj = new GameObject("Prompt");
        promptObj.transform.SetParent(dialoguePanelObject.transform, false);
        promptText = promptObj.AddComponent<TextMeshProUGUI>();
        promptText.fontSize = 16f;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = new Color(1f, 0.784f, 0.243f, 1f);
        promptText.outlineWidth = 0.2f;
        promptText.outlineColor = Color.black;
        promptText.raycastTarget = false;
        var promptLayout = promptObj.AddComponent<LayoutElement>();
        promptLayout.minHeight = 21f;
        promptLayout.preferredHeight = 21f;
        promptLayout.flexibleHeight = 0f;
        promptObj.SetActive(false);

        HideDialogueUi();
        CreateTimeTransitionUi();
        CreateRetryButton();
    }

    private void HideDialogueUi()
    {
        if (dialoguePanelObject != null)
        {
            dialoguePanelObject.SetActive(false);
        }
    }

    private void ShowDialogueUi()
    {
        if (dialoguePanelObject != null)
        {
            dialoguePanelObject.SetActive(true);
        }
    }

    private void StartConversation(List<DialogueLine> lines, ScenePhase newPhase)
    {
        if (lines == null || lines.Count == 0)
        {
            return;
        }

        dialogueActive = true;
        isTyping = false;
        waitForSpace = false;
        activeDialogue = lines;
        dialogueIndex = 0;
        phase = newPhase;
        StopDialogueVoice();
        ShowDialogueUi();

        LockPlayerControls();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        DisplayCurrentDialogue();
    }

    private void StartFocusedConversation(List<DialogueLine> lines, ScenePhase newPhase, Vector3 focusPoint)
    {
        cameraAimOverride = focusPoint;
        hasCameraAimOverride = true;
        StartConversation(lines, newPhase);
    }

    private void DisplayCurrentDialogue()
    {
        if (activeDialogue == null || dialogueIndex >= activeDialogue.Count)
        {
            FinishConversation();
            return;
        }

        var line = activeDialogue[dialogueIndex];
        StopDialogueVoice();
        pendingVoiceClip = GetUnknownManVoiceClip(line.Speaker, line.Text);
        pendingSentence = line.Text;
        bool hasSpeakerLabel = line.Speaker != "MC";
        speakerText.gameObject.SetActive(hasSpeakerLabel);
        speakerText.text = hasSpeakerLabel ? line.Speaker.ToUpperInvariant() + ":" : string.Empty;
        dialogueText.text = string.Empty;
        var panelRect = dialoguePanelObject.GetComponent<RectTransform>();
        float naturalTextWidth = dialogueText.GetPreferredValues(pendingSentence, 1200f, 0f).x;
        panelRect.sizeDelta = new Vector2(Mathf.Clamp(naturalTextWidth + 60f, 420f, 1260f), panelRect.sizeDelta.y);
        promptText.text = string.Empty;
        promptText.gameObject.SetActive(false);
        focusTarget = GetSpeakerTarget(line.Speaker);
        hasCameraAimPoint = hasCameraAimOverride;
        if (hasCameraAimOverride)
        {
            cameraAimPoint = cameraAimOverride;
            focusTarget = null;
            hasCameraAimOverride = false;
        }

        if (phase == ScenePhase.BedroomReturn && dialogueIndex == 2 && playerCamera != null)
        {
            StartCoroutine(BedroomPhoneSearchLook());
        }

        if (phase == ScenePhase.BrotherDialogue && brotherActor != null)
        {
            focusTarget = brotherActor.transform;
            hasCameraAimPoint = false;
        }
        else if (phase == ScenePhase.LittleBrotherRedRoomDialogue &&
                 line.Speaker == "Little Brother" &&
                 brotherActor != null)
        {
            focusTarget = brotherActor.transform;
            hasCameraAimPoint = false;
        }
        else if (phase == ScenePhase.Intro && pendingSentence == "Ok whatever it is I am not going to use this.")
        {
            focusTarget = fredActor != null ? fredActor.transform : null;
            hasCameraAimPoint = false;
        }
        else if (phase == ScenePhase.Intro &&
                 pendingSentence == "Oh he thinks I am a fool. I am not going to use it. Let’s get home or mom will get angry.")
        {
            hasCameraAimPoint = true;
            var awayFromFred = player != null && fredActor != null
                ? player.transform.position - fredActor.transform.position
                : player != null ? -player.transform.forward : Vector3.back;
            awayFromFred.y = 0f;
            if (awayFromFred.sqrMagnitude < 0.001f)
            {
                awayFromFred = Vector3.back;
            }

            cameraAimPoint = playerCamera != null
                ? playerCamera.transform.position + awayFromFred.normalized * 20f + Vector3.up * 3f
                : awayFromFred.normalized * 20f;
        }

        isTyping = false;
        waitForSpace = false;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        typingCoroutine = StartCoroutine(TypeSentence());
    }

    private IEnumerator TypeSentence()
    {
        while (playerCamera != null && IsCameraFocusPending())
        {
            yield return null;
        }

        isTyping = true;
        PlayDialogueVoice();
        if (pendingVoiceClip == null && audioSource != null && typingClip != null)
        {
            audioSource.clip = typingClip;
            audioSource.Play();
        }

        var words = pendingSentence.Split(' ');
        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < words.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(words[i]);
            dialogueText.text = builder.ToString();
            yield return new WaitForSecondsRealtime(0.07f);
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        dialogueText.text = pendingSentence;
        isTyping = false;
        waitForSpace = true;
        typingCoroutine = null;

        if (phase == ScenePhase.Intro && dialogueIndex == 0)
        {
            promptText.text = "PRESS SPACE TO CONTINUE";
            promptText.gameObject.SetActive(true);
        }
        else
        {
            promptText.text = string.Empty;
            promptText.gameObject.SetActive(false);
        }
    }

    private bool IsCameraFocusPending()
    {
        Vector3 targetPosition;
        if (focusTarget != null)
        {
            targetPosition = GetCharacterFaceFocusPoint(focusTarget);
        }
        else if (hasCameraAimPoint)
        {
            targetPosition = cameraAimPoint;
        }
        else
        {
            return false;
        }

        return Vector3.Angle(playerCamera.transform.forward, targetPosition - playerCamera.transform.position) > 1.5f;
    }

    private void LockPlayerControls()
    {
        if (!playerControlsLocked)
        {
            firstPersonControllerWasEnabled = firstPersonController != null && firstPersonController.enabled;
            worldFirstPersonControllerWasEnabled = worldFirstPersonController != null && worldFirstPersonController.enabled;
            pauseSettingsMenuWasEnabled = pauseSettingsMenu != null && pauseSettingsMenu.enabled;
            playerControlsLocked = true;
        }

        if (firstPersonController != null)
        {
            firstPersonController.enabled = false;
        }

        if (pauseSettingsMenu != null)
        {
            pauseSettingsMenu.enabled = false;
        }

        if (worldFirstPersonController != null)
        {
            worldFirstPersonController.enabled = false;
        }
    }

    private void ReleasePlayerControls()
    {
        if (!playerControlsLocked)
        {
            return;
        }

        SyncPlayerRotationToCamera();
        if (firstPersonController != null)
        {
            firstPersonController.enabled = firstPersonControllerWasEnabled;
        }

        if (worldFirstPersonController != null)
        {
            worldFirstPersonController.enabled = worldFirstPersonControllerWasEnabled;
        }

        if (pauseSettingsMenu != null)
        {
            pauseSettingsMenu.enabled = pauseSettingsMenuWasEnabled;
        }

        playerControlsLocked = false;
    }

    private void SyncPlayerRotationToCamera()
    {
        if (player == null || playerCamera == null)
        {
            return;
        }

        var cameraEuler = playerCamera.transform.eulerAngles;
        player.transform.rotation = Quaternion.Euler(0f, cameraEuler.y, 0f);
        playerCamera.transform.localRotation = Quaternion.Euler(cameraEuler.x, 0f, 0f);

        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        if (firstPersonController != null)
        {
            var pitchField = typeof(SimpleFirstPersonController).GetField("cameraPitch", flags);
            if (pitchField != null)
            {
                pitchField.SetValue(firstPersonController, Mathf.DeltaAngle(0f, cameraEuler.x));
            }
        }

        if (worldFirstPersonController != null)
        {
            var pitchField = typeof(WorldFirstPersonController).GetField("pitch", flags);
            if (pitchField != null)
            {
                pitchField.SetValue(worldFirstPersonController, Mathf.DeltaAngle(0f, cameraEuler.x));
            }
        }
    }

    private IEnumerator FocusHouseForObjective()
    {
        if (playerCamera == null || homeHouse == null)
        {
            yield break;
        }

        LockPlayerControls();
        cameraFocusActive = true;
        cameraFocusPoint = homeHouse.position + Vector3.up * 4f;
        var startingRotation = playerCamera.transform.rotation;
        var targetRotation = Quaternion.LookRotation(cameraFocusPoint - playerCamera.transform.position, Vector3.up);
        const float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration && phase == ScenePhase.GoHome)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            playerCamera.transform.rotation = Quaternion.Slerp(
                startingRotation,
                targetRotation,
                Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        playerCamera.transform.rotation = targetRotation;
        cameraFocusActive = false;
        if (!dialogueActive && phase == ScenePhase.GoHome)
        {
            ReleasePlayerControls();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private Transform GetSpeakerTarget(string speaker)
    {
        switch (speaker)
        {
            case "Fred":
                return fredActor != null ? fredActor.transform : player != null ? player.transform : null;
            case "Mom":
                return momActor != null ? momActor.transform : player != null ? player.transform : null;
            case "Brother":
            case "Little Brother":
                return brotherActor != null ? brotherActor.transform : player != null ? player.transform : null;
            default:
                return null;
        }
    }

    private static Transform faceFocusActor;
    private static Vector3 faceFocusCachedPoint;
    private static float faceFocusNextRefreshTime;

    private static Vector3 GetCharacterFaceFocusPoint(Transform actor)
    {
        if (actor == null)
        {
            return Vector3.zero;
        }

        // The camera-focus code calls this every frame for the whole of a conversation, and the
        // bounds pass allocates a Renderer array each time. Actors barely move while they talk, so
        // refresh a few times a second instead of every frame.
        if (actor == faceFocusActor && Time.unscaledTime < faceFocusNextRefreshTime)
        {
            return faceFocusCachedPoint;
        }

        faceFocusActor = actor;
        faceFocusNextRefreshTime = Time.unscaledTime + 0.1f;
        var renderers = actor.GetComponentsInChildren<Renderer>(true);
        var hasModelBounds = false;
        var bounds = new Bounds(actor.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].GetComponentInParent<TMP_Text>() != null)
            {
                continue;
            }

            if (!hasModelBounds)
            {
                bounds = renderers[i].bounds;
                hasModelBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        faceFocusCachedPoint = !hasModelBounds
            ? actor.position + Vector3.up * 1.4f
            : new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.86f, bounds.center.z);
        return faceFocusCachedPoint;
    }

    private void FinishConversation()
    {
        dialogueActive = false;
        isTyping = false;
        waitForSpace = false;
        focusTarget = null;
        hasCameraAimPoint = false;
        HideDialogueUi();
        StopDialogueVoice();

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (phase == ScenePhase.LittleBrotherRedRoomDialogue && hasCameraPoseBeforeLaptopCloseup && playerCamera != null)
        {
            playerCamera.transform.SetPositionAndRotation(
                cameraPositionBeforeLaptopCloseup,
                cameraRotationBeforeLaptopCloseup);
            hasCameraPoseBeforeLaptopCloseup = false;
            diningLaptopViewLocked = false;
            redRoomLaptopViewSaved = false;
        }

        if (phase == ScenePhase.BedroomReturn)
        {
            LockPlayerControls();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            StartCoroutine(StandUpFromBedroomBed());
            return;
        }

        ReleasePlayerControls();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (phase == ScenePhase.Intro)
        {
            currentObjective = "Go home";
            homeObjectiveActive = true;
            if (houseObjectiveMarker != null)
            {
                houseObjectiveMarker.SetActive(true);
            }

            SetHomeMarkerVisible(true);
            phase = ScenePhase.GoHome;
            StartCoroutine(FocusHouseForObjective());
        }
        else if (phase == ScenePhase.EnterHouseDialogue)
        {
            phase = ScenePhase.GoToMom;
            currentObjective = "Go to Mom";
        }
        else if (phase == ScenePhase.MomDialogue)
        {
            phase = ScenePhase.CheckLaptop;
            currentObjective = "Check the laptop";
        }
        else if (phase == ScenePhase.BrotherDialogue)
        {
            phase = ScenePhase.BedroomTransition;
            currentObjective = "Get into your room";
        }
        else if (phase == ScenePhase.PostPhoneDialogue)
        {
            LockPlayerControls();
            phase = ScenePhase.LaptopSequence;
            laptopImageIndex = 1;
            SetLaptopImagePlaceholder(2, bedroomLaptop);
            currentPrompt = string.Empty;
            StartCoroutine(ShowImageTwoThenTimeTransition());
        }
        else if (phase == ScenePhase.TimeTransition)
        {
            CompleteTimeTransition();
        }
        else if (phase == ScenePhase.NightLaptopDialogue)
        {
            if (laptopImageSurface != null)
            {
                laptopImageSurface.SetActive(false);
            }

            if (laptopImageLabel != null)
            {
                laptopImageLabel.gameObject.SetActive(false);
            }

            phase = ScenePhase.GoToMomAfterTimeSkip;
            currentObjective = "Go to Mom";
            currentPrompt = string.Empty;
        }
        else if (phase == ScenePhase.MissingMomDialogue)
        {
            StartCameraFocusedConversation(
                GetDiningLaptopFocusPoint(),
                new List<DialogueLine> { new DialogueLine("MC", "Let me see what lil bro used to watch.") },
                ScenePhase.LateDiningLaptopDialogue);
        }
        else if (phase == ScenePhase.LateDiningLaptopDialogue)
        {
            if (bedroomDoorInteraction != null)
            {
                bedroomDoorInteraction.enabled = true;
            }

            phase = ScenePhase.LateCheckLaptop;
            currentObjective = "Check the laptop";
            currentPrompt = string.Empty;
        }
        else if (phase == ScenePhase.RedRoomIntroDialogue)
        {
            phase = ScenePhase.RedRoomImages;
            laptopImageIndex = 4;
            currentPrompt = string.Empty;
            LockPlayerControls();
            LockCameraOnDiningLaptop();
            StartCoroutine(PlayRedRoomImages());
        }
        else if (phase == ScenePhase.LittleBrotherRedRoomDialogue)
        {
            currentObjective = "Check messages from Fred";
            currentPrompt = string.Empty;
            StartConversation(new List<DialogueLine>
            {
                new DialogueLine("MC", "Is this Fred fool?")
            }, ScenePhase.FredArgumentLeadIn);
        }
        else if (phase == ScenePhase.FredArgumentLeadIn)
        {
            phase = ScenePhase.FredArgumentPhone;
            currentObjective = "Check messages from Fred";
            currentPrompt = "↑ OPEN PHONE";
        }
        else if (phase == ScenePhase.FredLinkLeadIn)
        {
            phase = ScenePhase.FredLinkLoading;
            currentPrompt = string.Empty;
            StartCoroutine(OpenFredLinkAfterLoading());
        }
        else if (phase == ScenePhase.FredImageDialogue)
        {
            phase = ScenePhase.FredImagePhoneClose;
            currentPrompt = "↑ CLOSE PHONE";
            LockPlayerControls();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (phase == ScenePhase.RoomSecured)
        {
            // Safe inside the room: now distract the intruder to slip out of the house.
            phase = ScenePhase.WindowDistraction;
            currentObjective = "Pick up alarm clock and throw out of window";
            currentPrompt = string.Empty;
            distractionClockPickedUp = false;
            PrepareDistractionProp();
        }
        else if (phase == ScenePhase.WakeUpSequence)
        {
            phase = ScenePhase.BrotherMissingSearch;
            currentObjective = "Leave your room and find Little Brother";
            currentPrompt = string.Empty;
        }
        else if (phase == ScenePhase.BathroomShadow)
        {
            phase = ScenePhase.OutsideSearch;
            currentObjective = "Go outside and search for Little Brother";
            currentPrompt = string.Empty;
        }
        else if (phase == ScenePhase.OutsideSearch)
        {
            phase = ScenePhase.FredWarningPhone;
            currentObjective = "Go inside the house and call the cops";
            currentPrompt = string.Empty;
            StartFredWarningPhone();
        }
        else if (phase == ScenePhase.MomNoteApproach)
        {
            phase = ScenePhase.ReadMomNote;
            currentPrompt = "PRESS E TO READ NOTE";
        }
        else if (phase == ScenePhase.MomNoteText)
        {
            currentPrompt = string.Empty;
            StartConversation(new List<DialogueLine>
            {
                new DialogueLine("MC", "I have to go to Aunt Ela's house and tell Mom.")
            }, ScenePhase.MomAfterNoteDialogue);
        }
        else if (phase == ScenePhase.MomAfterNoteDialogue)
        {
            currentObjective = string.Empty;
            currentPrompt = string.Empty;
            phase = ScenePhase.MysteriousMan;
            StartMysteriousManSequence();
        }
        else if (phase == ScenePhase.MysteriousMan)
        {
            // The creature has been spotted: start the 5-second dash to the room immediately.
            StartEscapeDanger("Shit, who is that? Run!");
        }
        else if (phase == ScenePhase.GameOver)
        {
            phase = ScenePhase.RetryCheckpoint;
            HandleRetryPressed();
        }
        else if (phase == ScenePhase.RetryCheckpoint)
        {
            phase = ScenePhase.EscapeDanger;
            StartEscapeDangerTimer();
        }
        else if (phase == ScenePhase.JungleChaseSpotted)
        {
            // Panic line delivered: now the creature is actually on the player's heels.
            phase = ScenePhase.JungleChase;
            jungleChaseActive = true;
            currentObjective = "Run to Aunt Ela's house";
            currentPrompt = "RUN!";
        }
        else if (phase == ScenePhase.JungleChaseVanish)
        {
            // The creature vanished. The player shrugs it off and carries on toward Aunt Ela's
            // house; the second encounter is armed to fire 10 m from her door.
            phase = ScenePhase.AuntElaApproach;
            currentObjective = "Go to Aunt Ela's house";
            currentPrompt = string.Empty;
            auntElaSpotArmed = true;
            auntElaSpotTriggered = false;
        }
        else if (phase == ScenePhase.AuntElaSpot)
        {
            // "WTF" delivered: the creature rushes the player and the screen cuts to black.
            phase = ScenePhase.AuntElaRush;
            auntElaRushActive = true;
            auntElaRushTimer = 0f;
            currentObjective = string.Empty;
            currentPrompt = string.Empty;
            MessageBoxRushIn();
        }
        else if (phase == ScenePhase.HallwayWake)
        {
            // The initial hallway wake lines are done: free the player to explore.
            phase = ScenePhase.HallwayExplore;
            currentObjective = "Find a way to get out of here";
            currentPrompt = string.Empty;
            hallwayExplorationActive = true;
            hallwaySpawnLocalZ = player != null && hallwayRoot != null
                ? hallwayRoot.InverseTransformPoint(player.transform.position).z
                : 0f;
        }
        else if (phase == ScenePhase.HallwayEndless)
        {
            // The player realised the hallway seems endless. Reveal the office setup ahead.
            SpawnHallwayOfficeSetup();
            var pcFocus = hallwayOfficeSetup != null
                ? hallwayOfficeSetup.transform.position + Vector3.up * 1.1f
                : player != null
                    ? player.transform.position + player.transform.forward * 10f
                    : Vector3.zero;
            StartFocusedConversation(hallwayPcSpottedDialogue, ScenePhase.HallwayPcSpotted, pcFocus);
        }
        else if (phase == ScenePhase.HallwayPcSpotted)
        {
            phase = ScenePhase.HallwayGoToPc;
            currentObjective = "Go to the PC setup";
            currentPrompt = string.Empty;
            hallwayPcApproachArmed = true;
        }
        else if (phase == ScenePhase.HallwayPcDialogue)
        {
            // The developer said bye and the player's "Wait!" was cut off by a black screen.
            StartCoroutine(HallwayBlackoutToBedroom());
        }
    }

    private void AdvanceDialogue()
    {
        if (!dialogueActive)
        {
            return;
        }

        dialogueIndex++;
        if (dialogueIndex < activeDialogue.Count)
        {
            DisplayCurrentDialogue();
            return;
        }

        FinishConversation();
    }

    private void Update()
    {
        if (diningLaptopViewLocked && playerCamera != null)
        {
            playerCamera.transform.SetPositionAndRotation(
                diningLaptopCameraPosition,
                diningLaptopCameraRotation);
        }
        else if (cameraFocusActive && playerCamera != null)
        {
            var lookRotation = Quaternion.LookRotation(cameraFocusPoint - playerCamera.transform.position, Vector3.up);
            playerCamera.transform.rotation = Quaternion.RotateTowards(
                playerCamera.transform.rotation,
                lookRotation,
                240f * Time.unscaledDeltaTime);
        }
        else if (dialogueActive && focusTarget != null && playerCamera != null)
        {
            var lookPosition = GetCharacterFaceFocusPoint(focusTarget);
            var lookRotation = Quaternion.LookRotation(lookPosition - playerCamera.transform.position, Vector3.up);
            playerCamera.transform.rotation = Quaternion.RotateTowards(
                playerCamera.transform.rotation,
                lookRotation,
                240f * Time.unscaledDeltaTime);
        }
        else if (dialogueActive && hasCameraAimPoint && playerCamera != null)
        {
            var lookRotation = Quaternion.LookRotation(cameraAimPoint - playerCamera.transform.position, Vector3.up);
            playerCamera.transform.rotation = Quaternion.RotateTowards(
                playerCamera.transform.rotation,
                lookRotation,
                240f * Time.unscaledDeltaTime);
        }

        if (dialogueActive && advanceQueuedForVoice && !isTyping && !IsDialogueVoicePlaying())
        {
            advanceQueuedForVoice = false;
            AdvanceDialogue();
        }

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        bool spacePressedThisFrame = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        bool dialogueConsumedSpace = dialogueActive && spacePressedThisFrame;
        if (dialogueConsumedSpace)
        {
            if (isTyping)
            {
                StopTyping();
                return;
            }

            if (waitForSpace)
            {
                if (IsDialogueVoicePlaying())
                {
                    // The voice line is still speaking: remember the request and let it finish.
                    advanceQueuedForVoice = true;
                }
                else
                {
                    AdvanceDialogue();
                }
            }
        }

        // Once the player is free and heading to Aunt Ela's, watch for them moving forward on the
        // road. The moment they push on, something steps out of the treeline and the chase begins.
        if (!dialogueActive && phase == ScenePhase.Finished && !jungleChaseStarted && player != null)
        {
            CheckStartJungleChase();
        }

        if (jungleChaseActive && !dialogueActive)
        {
            UpdateJungleChase();
        }

        // Second encounter: once the jungle chase has vanished, watch for the player closing in on
        // Aunt Ela's house. At ~10 m the monster appears again and the player panics ("WTF").
        if (!dialogueActive && phase == ScenePhase.AuntElaApproach && auntElaSpotArmed &&
            !auntElaSpotTriggered && player != null)
        {
            CheckAuntElaSpot();
        }

        // After the hallway dream and the bedroom wake-up, the player heads to Aunt Ela's house
        // again. 200 m out, the Fred scare plays before the house encounter can fire.
        if (!dialogueActive && phase == ScenePhase.AuntElaSecondApproach && player != null)
        {
            if (!fredScareTriggered)
            {
                CheckFredScareOnRoad();
            }
            else if (auntElaSpotArmed && !auntElaSpotTriggered)
            {
                CheckAuntElaSpot();
            }
        }

        if (auntElaRushActive && !dialogueActive)
        {
            UpdateAuntElaRush();
        }

        // Endless hallway exploration: watch for the ~70 m wander and the PC approach.
        if (phase == ScenePhase.HallwayExplore || phase == ScenePhase.HallwayGoToPc ||
            phase == ScenePhase.HallwayPcSpotted || phase == ScenePhase.HallwayEndless)
        {
            UpdateHallwayInfinite();
        }

        if (!dialogueActive && player != null)
        {
            if (phase == ScenePhase.HallwayExplore && hallwayExplorationActive)
            {
                CheckHallwayEndlessLine();
            }
            else if (phase == ScenePhase.HallwayGoToPc && hallwayPcApproachArmed)
            {
                CheckHallwayPcApproach();
            }
        }

        // On the game over screen the player can retry with the button or by pressing Enter/E.
        if (phase == ScenePhase.GameOver || gameOverVisible)
        {
            bool retryPressed = (keyboard != null &&
                                  (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame ||
                                   keyboard.eKey.wasPressedThisFrame)) ||
                                 (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (retryPressed)
            {
                HandleRetryPressed();
                return;
            }

            return;
        }

        if (!dialogueActive && phase == ScenePhase.FredArgumentPhone &&
            keyboard != null && keyboard.upArrowKey.wasPressedThisFrame && phoneMessages != null)
        {
            if (!phoneMessages.IsPhoneOpen)
            {
                if (phoneObject != null)
                {
                    phoneObject.SetActive(true);
                }

                LockPlayerControls();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                StartFredArgumentPhone();
                currentPrompt = "↑ CLOSE PHONE";
            }
            else if (phoneMessages.CustomConversationComplete)
            {
                phoneMessages.ClosePhone();
                if (phoneObject != null)
                {
                    phoneObject.SetActive(false);
                }

                ReleasePlayerControls();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                BeginTwoHoursLaterTransition();
            }
            else
            {
                currentPrompt = "MESSAGES...";
            }
        }

        if (!dialogueActive && phase == ScenePhase.GoToMomAfterTimeSkip)
        {
            if (!bedroomDoorOpenedAfterTimeSkip &&
                player != null &&
                IsPlayerInside(bedroomTrigger) &&
                IsBedroomDoorInReach() &&
                ((keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                 (mouse != null && mouse.leftButton.wasPressedThisFrame)))
            {
                if (bedroomDoorInteraction != null)
                {
                    bedroomDoorInteraction.enabled = false;
                }

                SetBedroomDoorOpen(true);
            }

            if (IsBedroomDoorOpen())
            {
                bedroomDoorOpenedAfterTimeSkip = true;
            }

            currentPrompt = bedroomDoorOpenedAfterTimeSkip || !IsPlayerInside(bedroomTrigger)
                ? string.Empty
                : IsBedroomDoorInReach()
                    ? "PRESS E OR CLICK TO OPEN BEDROOM DOOR"
                    : "MOVE CLOSER TO THE BEDROOM DOOR";
        }

        if (!dialogueActive && phase == ScenePhase.EscapeDanger && player != null && !creatureRushActive)
        {
            // Allow the player to slam the bedroom door shut while the creature is closing in.
            bool interactPressed = (keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                                   (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (interactPressed && IsBedroomDoorOpen())
            {
                SetBedroomDoorOpen(false);
            }

            bool securedInRoom = IsPlayerInside(bedroomTrigger) && !IsBedroomDoorOpen();
            if (securedInRoom)
            {
                // Player made it into the room and shut the door in time: continue the story.
                escapeTimerStarted = false;
                escapeTimerExpired = false;
                DestroyPursuingCreature();
                StartEscapeRoomSurvived();
            }
            else
            {
                if (escapeTimerStarted)
                {
                    escapeTimerRemaining -= Time.unscaledDeltaTime;
                    UpdatePursuingCreature();
                }

                if (escapeTimerStarted && escapeTimerRemaining <= 0f)
                {
                    StartCreatureRush();
                }
                else if (!escapeTimerStarted && Vector3.Distance(
                             player.transform.position,
                             homeHouse != null ? homeHouse.position : player.transform.position) > 3f)
                {
                    StartEscapeDangerTimer();
                }

                if (escapeTimerStarted)
                {
                    currentPrompt = IsPlayerInside(bedroomTrigger)
                        ? "PRESS E TO CLOSE THE DOOR  •  " + Mathf.CeilToInt(Mathf.Max(0f, escapeTimerRemaining)) + "s"
                        : "GET IN YOUR ROOM  •  " + Mathf.CeilToInt(Mathf.Max(0f, escapeTimerRemaining)) + "s";
                }
                else
                {
                    currentPrompt = string.Empty;
                }
            }
        }

        if (phase == ScenePhase.FinalEscape)
        {
            UpdateFinalEscape();
        }

        if (!dialogueActive && phase == ScenePhase.FredWarningPhone && phoneMessages != null &&
            phoneMessages.IsPhoneOpen && phoneMessages.CustomConversationComplete)
        {
            currentPrompt = "↑ CLOSE PHONE";
            if (keyboard != null && keyboard.upArrowKey.wasPressedThisFrame)
            {
                phoneMessages.ClosePhone();
                if (phoneObject != null)
                {
                    phoneObject.SetActive(false);
                }

                ReleasePlayerControls();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                currentPrompt = string.Empty;
                StartConversation(new List<DialogueLine>
                {
                    new DialogueLine("MC", "Maybe he's telling the truth. I'll check once.")
                }, ScenePhase.FredLinkLeadIn);
            }
        }
        else if (!dialogueActive && phase == ScenePhase.FredWarningPhone)
        {
            currentPrompt = string.Empty;
        }

        if (!dialogueActive && phase == ScenePhase.FredImagePhoneClose && phoneMessages != null &&
            phoneMessages.IsPhoneOpen && keyboard != null && keyboard.upArrowKey.wasPressedThisFrame)
        {
            phoneMessages.ClosePhone();
            if (phoneObject != null)
            {
                phoneObject.SetActive(false);
            }

            ReleasePlayerControls();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            phase = ScenePhase.ReadMomNote;
            currentObjective = "Go inside the house and call the cops";
            currentPrompt = string.Empty;
            StartMomNoteSequence();
        }

        if (!dialogueActive && phase == ScenePhase.GoToMomAfterTimeSkip &&
            bedroomDoorOpenedAfterTimeSkip && IsKitchenViewAreaReached())
        {
            currentObjective = string.Empty;
            currentPrompt = string.Empty;
            phase = ScenePhase.MissingMomDialogue;
            StartCameraFocusedConversation(
                GetKitchenFocusPoint(),
                new List<DialogueLine> { new DialogueLine("MC", "Where did mom gone?") },
                ScenePhase.MissingMomDialogue);
        }

        if (!dialogueActive && phase == ScenePhase.LateCheckLaptop && player != null && diningLaptop != null &&
            HorizontalDistance(player.transform.position, diningLaptop.position) <= 2f)
        {
            BeginRedRoomSequence();
        }

        if (!dialogueActive && phase == ScenePhase.GoHome && !houseReached)
        {
            var triggerCollider = houseTrigger != null ? houseTrigger.GetComponent<BoxCollider>() : null;
            if (triggerCollider != null && player != null &&
                HorizontalDistance(triggerCollider.bounds.ClosestPoint(player.transform.position), player.transform.position) <= 7f)
            {
                homeObjectiveActive = false;
                currentObjective = "Enter the house";
                phase = ScenePhase.EnterHouse;
                if (houseObjectiveMarker != null)
                {
                    houseObjectiveMarker.SetActive(false);
                }

                SetHomeMarkerVisible(false);
            }
        }

        if (!dialogueActive && phase == ScenePhase.BrotherMissingSearch && player != null && diningLaptop != null &&
            HasFullyExitedBedroom() && HorizontalDistance(player.transform.position, diningLaptop.position) <= 6f && !brotherHiddenDuringSearch)
        {
            brotherHiddenDuringSearch = true;
            StartCoroutine(BrotherMissingCinematic());
        }

        if (!dialogueActive && phase == ScenePhase.BrotherMissingSearch && player != null && bathroomTrigger != null &&
            !bathroomShadowTriggered && IsPlayerInside(bathroomTrigger))
        {
            bathroomShadowTriggered = true;
            StartBathroomShadowSequence();
        }

        if (!dialogueActive && phase == ScenePhase.OutsideSearch && player != null && homeHouse != null)
        {
            ResolveBathroomAnchors();
            var bathroomSink = bathroomWindowSink;
            var bathroomCurtain = bathroomWindowCurtain;
            var bathroomOutsideDirection = bathroomCurtain != null && bathroomSink != null
                ? bathroomCurtain.position - bathroomSink.position
                : Vector3.zero;
            bathroomOutsideDirection.y = 0f;
            bathroomOutsideDirection = bathroomOutsideDirection.sqrMagnitude > 0.001f
                ? bathroomOutsideDirection.normalized
                : -homeHouse.forward;
            var outsideWindowTarget = GetBathroomWindowFocusPoint() + bathroomOutsideDirection * 1.5f;
            var playerFromBathroomWindow = player.transform.position - GetBathroomWindowFocusPoint();
            playerFromBathroomWindow.y = 0f;
            bool isOutsideBathroomWindow = Vector3.Dot(playerFromBathroomWindow, bathroomOutsideDirection) > 0.5f;
            if (isOutsideBathroomWindow && Vector3.Distance(player.transform.position, outsideWindowTarget) < 3.5f)
            {
                if (!outsideSearchTriggered)
                {
                    outsideSearchTriggered = true;
                    StartConversation(new List<DialogueLine>
                    {
                        new DialogueLine("MC", "Come on, stop messing around. I know it's you!")
                    }, ScenePhase.OutsideSearch);
                    if (notificationClip != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(notificationClip);
                    }
                }
            }
        }

        if (!dialogueActive && phase == ScenePhase.ReadMomNote && player != null && noteObject != null &&
            IsPlayerInsideHouse() &&
            Vector3.Distance(player.transform.position, noteObject.transform.position) < 3f && !noteRead)
        {
            if (!noteApproached)
            {
                noteApproached = true;
                currentPrompt = string.Empty;
                StartCameraFocusedConversation(
                    noteObject.transform.position,
                    new List<DialogueLine> { new DialogueLine("MC", "What's this?") },
                    ScenePhase.MomNoteApproach);
            }
            else if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                noteRead = true;
                ReadMomNote();
            }
            else if (phase == ScenePhase.MomNoteApproach)
            {
                currentPrompt = "PRESS E TO READ NOTE";
            }
        }

        // After surviving the dash, the player distracts the intruder to get out of the house.
        if (!dialogueActive && phase == ScenePhase.WindowDistraction && player != null &&
            distractionProp != null && !distractionClockPickedUp &&
            Vector3.Distance(player.transform.position, distractionProp.transform.position) < 1.9f)
        {
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                ThrowDistraction();
            }
            else
            {
                currentPrompt = "E  •  PICK UP ALARM CLOCK";
            }
        }

        if (!dialogueActive && phase == ScenePhase.Finished && player != null && !gameOverVisible && !escapeTimerStarted)
        {
            phase = ScenePhase.Finished;
        }

        if (!dialogueActive && phase == ScenePhase.EnterHouse && !houseReached && IsPlayerInside(houseTrigger))
        {
            houseReached = true;
            homeObjectiveActive = false;
            currentObjective = string.Empty;
            SetHomeMarkerVisible(false);
            if (houseObjectiveMarker != null)
            {
                houseObjectiveMarker.SetActive(false);
            }

            StartConversation(new List<DialogueLine> { new DialogueLine("MC", "I am home.") }, ScenePhase.EnterHouseDialogue);
        }

        if (!dialogueActive && phase == ScenePhase.GoToMom && player != null && momActor != null &&
            HorizontalDistance(player.transform.position, momActor.transform.position) <= 1.75f)
        {
            currentObjective = string.Empty;
            StartConversation(momDialogue, ScenePhase.MomDialogue);
        }

        if (!dialogueActive && phase == ScenePhase.CheckLaptop && player != null && brotherActor != null &&
            HorizontalDistance(player.transform.position, brotherActor.transform.position) <= 1.75f)
        {
            currentObjective = string.Empty;
            StartConversation(brotherDialogue, ScenePhase.BrotherDialogue);
        }

        if (!dialogueActive && phase == ScenePhase.BedroomTransition && IsPlayerInside(bedroomTrigger) &&
            bedroomBed != null && player != null &&
            HorizontalDistance(player.transform.position, bedroomBed.position) <= 2.5f)
        {
            phase = ScenePhase.BedPrompt;
            currentPrompt = "PRESS Q TO LIE ON BED";
        }
        else if (!dialogueActive && phase == ScenePhase.BedPrompt && bedroomBed != null && player != null &&
                 HorizontalDistance(player.transform.position, bedroomBed.position) > 3f)
        {
            phase = ScenePhase.BedroomTransition;
            currentPrompt = string.Empty;
        }

        if (!dialogueActive && phase == ScenePhase.BedPrompt && keyboard != null && keyboard.qKey.wasPressedThisFrame)
        {
            currentPrompt = string.Empty;
            BeginLyingDownTransition();
        }

        if (!dialogueActive && phase == ScenePhase.PhonePrompt && keyboard != null && keyboard.upArrowKey.wasPressedThisFrame)
        {
            LockPlayerControls();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (phoneObject != null)
            {
                phoneObject.SetActive(true);
            }

            if (phoneMessages != null)
            {
                phoneMessages.OpenSceneConversation();
            }

            currentPrompt = string.Empty;
            phase = ScenePhase.PhoneOpen;
        }

        if (!dialogueActive && phase == ScenePhase.PhoneOpen && phoneMessages != null)
        {
            if (phoneMessages.SceneReplySent)
            {
                currentPrompt = "↑ CLOSE PHONE";
                if (keyboard != null && keyboard.upArrowKey.wasPressedThisFrame)
                {
                    phoneMessages.CloseSceneConversation();
                    if (phoneObject != null)
                    {
                        phoneObject.SetActive(false);
                    }

                    ReleasePlayerControls();
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    currentObjective = string.Empty;
                    currentPrompt = string.Empty;
                    StartConversation(afterPhoneDialogue, ScenePhase.PostPhoneDialogue);
                }
            }
        }

        UpdateObjectiveUi();
        UpdateHomeCharacters();
        UpdateHouseMarkerScale();
        UpdatePhonePose();
        UpdateLaptopImagePlaceholder();
        UpdateSceneAudio();
    }

    private bool IsPlayerInside(GameObject triggerObject)
    {
        if (player == null || triggerObject == null)
        {
            return false;
        }

        var collider = triggerObject.GetComponent<BoxCollider>();
        return collider != null && collider.bounds.Contains(player.transform.position);
    }

    private bool IsPlayerInsideHouse()
    {
        if (player == null || homeHouse == null || houseEntranceDoor == null)
        {
            return false;
        }

        var insideDirection = homeHouse.position - houseEntranceDoor.position;
        var playerDirection = player.transform.position - houseEntranceDoor.position;
        insideDirection.y = 0f;
        playerDirection.y = 0f;
        return insideDirection.sqrMagnitude > 0.001f &&
               Vector3.Dot(playerDirection, insideDirection.normalized) > 0.5f;
    }

    private bool IsBedroomDoorOpen()
    {
        return bedroomDoorAnimator != null &&
               !string.IsNullOrEmpty(bedroomDoorOpenParameter) &&
               bedroomDoorAnimator.GetBool(bedroomDoorOpenParameter);
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    private void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        dialogueText.text = pendingSentence;
        isTyping = false;
        waitForSpace = true;
        promptText.text = "PRESS SPACE TO CONTINUE";
        promptText.gameObject.SetActive(true);
    }

    private void BeginLyingDownTransition()
    {
        if (bedroomReached)
        {
            return;
        }

        bedroomReached = true;
        phase = ScenePhase.LyingDownTransition;
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        LockPlayerControls();
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (player != null && playerCamera != null && bedroomBed != null && bedroomLaptop != null)
        {
            var screenRenderer = FindLaptopScreenRenderer(bedroomLaptop);
            var laptopLookPoint = screenRenderer != null
                ? screenRenderer.bounds.center
                : bedroomLaptop.position + Vector3.up * 0.15f;
            var targetCameraPosition = laptopLookPoint + bedroomLaptop.forward * 0.65f + Vector3.up * 0.12f;
            var targetCameraRotation = Quaternion.LookRotation(laptopLookPoint - targetCameraPosition, Vector3.up);
            var targetPlayerRotation = Quaternion.Euler(0f, targetCameraRotation.eulerAngles.y, 0f);
            var targetCameraLocalPosition = Quaternion.Inverse(targetPlayerRotation) *
                (targetCameraPosition - bedroomBed.position);
            var targetCameraLocalRotation = Quaternion.Inverse(targetPlayerRotation) * targetCameraRotation;

            SetLaptopImagePlaceholder(1, bedroomLaptop);
            StartCoroutine(AnimateLyingDown(
                player.transform.position,
                player.transform.rotation,
                playerCamera.transform.localPosition,
                playerCamera.transform.localRotation,
                bedroomBed.position,
                targetPlayerRotation,
                targetCameraLocalPosition,
                targetCameraLocalRotation));
        }
        else
        {
            Debug.LogError("Scene2EveningManager could not lie the player down because the bed, laptop, player, or camera is missing.", this);
        }
    }

    private IEnumerator AnimateLyingDown(
        Vector3 startPlayerPosition,
        Quaternion startPlayerRotation,
        Vector3 startCameraLocalPosition,
        Quaternion startCameraLocalRotation,
        Vector3 targetPlayerPosition,
        Quaternion targetPlayerRotation,
        Vector3 targetCameraLocalPosition,
        Quaternion targetCameraLocalRotation)
    {
        const float duration = 0.9f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            player.transform.SetPositionAndRotation(
                Vector3.Lerp(startPlayerPosition, targetPlayerPosition, progress),
                Quaternion.Slerp(startPlayerRotation, targetPlayerRotation, progress));
            playerCamera.transform.localPosition = Vector3.Lerp(
                startCameraLocalPosition,
                targetCameraLocalPosition,
                progress);
            playerCamera.transform.localRotation = Quaternion.Slerp(
                startCameraLocalRotation,
                targetCameraLocalRotation,
                progress);
            yield return null;
        }

        player.transform.SetPositionAndRotation(targetPlayerPosition, targetPlayerRotation);
        playerCamera.transform.localPosition = targetCameraLocalPosition;
        playerCamera.transform.localRotation = targetCameraLocalRotation;
        FinishLyingDownTransition();
    }

    private void FinishLyingDownTransition()
    {
        currentObjective = "Check messages";
        currentPrompt = "↑ OPEN PHONE";
        phase = ScenePhase.PhonePrompt;
        if (phoneMessages != null)
        {
            phoneMessages.ConfigureSceneConversation();
        }

        if (notificationClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(notificationClip);
        }
    }

    private Renderer FindLaptopScreenRenderer(Transform laptop)
    {
        if (laptop == null)
        {
            return null;
        }

        var renderers = laptop.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].gameObject.name == "PartsB")
            {
                return renderers[i];
            }
        }

        return renderers.Length > 0 ? renderers[0] : null;
    }

    private void CreateLaptopImagePlaceholder()
    {
        if (bedroomLaptopScreenRenderer == null)
        {
            Debug.LogError("Scene2EveningManager could not attach image placeholders because the existing laptop screen renderer is missing.", this);
            return;
        }

        laptopImageSurface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        laptopImageSurface.name = "LaptopImagePlaceholderSurface";
        laptopImageSurface.transform.SetParent(transform, false);
        var surfaceCollider = laptopImageSurface.GetComponent<Collider>();
        if (surfaceCollider != null)
        {
            Destroy(surfaceCollider);
        }

        var imageShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (imageShader == null)
        {
            imageShader = Shader.Find("Unlit/Texture");
        }

        laptopImageMaterial = new Material(imageShader != null ? imageShader : GetPlaceholderShader());
        laptopImageMaterial.color = Color.white;
        laptopImageSurface.GetComponent<Renderer>().sharedMaterial = laptopImageMaterial;

        var labelObject = new GameObject("LaptopImagePlaceholderLabel");
        labelObject.transform.SetParent(transform, false);
        laptopImageLabel = labelObject.AddComponent<TextMeshPro>();
        laptopImageLabel.fontSize = 0.15f;
        laptopImageLabel.enableAutoSizing = false;
        laptopImageLabel.alignment = TextAlignmentOptions.Center;
        laptopImageLabel.color = Color.white;
        laptopImageLabel.textWrappingMode = TextWrappingModes.NoWrap;
        laptopImageLabel.raycastTarget = false;

        laptopImageSurface.SetActive(false);
        labelObject.SetActive(false);
    }

    private void SetLaptopImagePlaceholder(int imageNumber)
    {
        SetLaptopImagePlaceholder(imageNumber, bedroomLaptop);
    }

    private void SetLaptopImagePlaceholder(int imageNumber, Transform targetLaptop)
    {
        if (laptopImageSurface == null || laptopImageLabel == null || laptopImageMaterial == null)
        {
            return;
        }

        placeholderLaptop = targetLaptop;
        placeholderLaptopScreenRenderer = FindLaptopScreenRenderer(targetLaptop);
        if (placeholderLaptopScreenRenderer == null)
        {
            Debug.LogError("Scene2EveningManager could not find a screen renderer for laptop placeholder IMG " + imageNumber + ".", this);
            return;
        }

        var image = LoadLaptopStoryImage(imageNumber);
        laptopImageMaterial.mainTexture = image;
        laptopImageLabel.text = "IMG " + imageNumber;
        laptopImageLabel.gameObject.SetActive(image == null);
        laptopImageSurface.SetActive(true);
        UpdateLaptopImagePlaceholder();
    }

    private static Texture2D LoadLaptopStoryImage(int imageNumber)
    {
#if UNITY_EDITOR
        string assetPath;
        switch (imageNumber)
        {
            case 1:
                assetPath = "Assets/StoryImages/image 1.PNG";
                break;
            case 2:
                assetPath = "Assets/StoryImages/image 2.png";
                break;
            case 4:
                assetPath = "Assets/StoryImages/image 4.jpg";
                break;
            case 5:
                assetPath = "Assets/StoryImages/image 5.png";
                break;
            case 6:
                assetPath = "Assets/StoryImages/image 6.png";
                break;
            case 7:
                assetPath = "Assets/StoryImages/image 7.jpg";
                break;
            default:
                return null;
        }

        return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
#else
        return null;
#endif
    }

    private Texture2D LoadPhoneLinkImage()
    {
        return fredLinkImage5 != null ? fredLinkImage5 : LoadLaptopStoryImage(5);
    }

    private void UpdateLaptopImagePlaceholder()
    {
        if (placeholderLaptopScreenRenderer == null ||
            placeholderLaptop == null ||
            laptopImageSurface == null ||
            !laptopImageSurface.activeSelf ||
            laptopImageLabel == null)
        {
            return;
        }

        var screenBounds = placeholderLaptopScreenRenderer.bounds;
        var screenTransform = placeholderLaptopScreenRenderer.transform;
        var screenNormal = screenTransform.forward;
        var screenRight = screenTransform.right;
        var screenUp = screenTransform.up;
        var extents = screenBounds.extents;
        float halfWidth =
            Mathf.Abs(screenRight.x) * extents.x +
            Mathf.Abs(screenRight.y) * extents.y +
            Mathf.Abs(screenRight.z) * extents.z;
        float halfHeight =
            Mathf.Abs(screenUp.x) * extents.x +
            Mathf.Abs(screenUp.y) * extents.y +
            Mathf.Abs(screenUp.z) * extents.z;
        var screenMesh = screenTransform.GetComponent<MeshFilter>()?.sharedMesh;
        float halfDepth = screenMesh != null
            ? screenTransform.TransformVector(Vector3.forward * screenMesh.bounds.extents.z).magnitude
            : Mathf.Abs(screenNormal.x) * extents.x +
              Mathf.Abs(screenNormal.y) * extents.y +
              Mathf.Abs(screenNormal.z) * extents.z;
        var surfaceRotation = Quaternion.LookRotation(screenNormal, screenUp);
        var surfacePosition = screenBounds.center + screenNormal * (halfDepth + 0.0005f);
        float width = Mathf.Max(0.001f, halfWidth * 1.9f);
        float height = Mathf.Max(0.001f, halfHeight * 1.9f);
        var image = laptopImageMaterial.mainTexture as Texture2D;
        if (image != null && image.width > 0 && image.height > 0)
        {
            float imageAspect = image.width / (float)image.height;
            float screenAspect = width / height;
            if (imageAspect > screenAspect)
            {
                height = width / imageAspect;
            }
            else
            {
                width = height * imageAspect;
            }
        }

        laptopImageSurface.transform.SetPositionAndRotation(
            surfacePosition,
            surfaceRotation * Quaternion.Euler(0f, 180f, 0f));
        laptopImageSurface.transform.localScale = new Vector3(width, height, 1f);
        laptopImageLabel.transform.SetPositionAndRotation(
            surfacePosition + screenNormal * 0.002f,
            surfaceRotation * Quaternion.Euler(0f, 180f, 0f));
        laptopImageLabel.rectTransform.sizeDelta = new Vector2(width * 0.88f, height * 0.8f);
    }

    private void CreateTimeTransitionUi()
    {
        timeTransitionPanel = new GameObject("TwoHoursLaterOverlay");
        timeTransitionPanel.transform.SetParent(dialogueCanvasObject.transform, false);
        var panelRect = timeTransitionPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var background = timeTransitionPanel.AddComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = false;
        timeTransitionCanvasGroup = timeTransitionPanel.AddComponent<CanvasGroup>();
        timeTransitionCanvasGroup.alpha = 1f;
        var overlayCanvas = timeTransitionPanel.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 260;

        var timeTextObject = new GameObject("TimeText");
        timeTextObject.transform.SetParent(timeTransitionPanel.transform, false);
        timeTransitionText = timeTextObject.AddComponent<TextMeshProUGUI>();
        timeTransitionText.text = "2 HOURS LATER";
        timeTransitionText.fontSize = 40f;
        timeTransitionText.fontStyle = FontStyles.Bold;
        timeTransitionText.color = Color.white;
        timeTransitionText.alignment = TextAlignmentOptions.Center;
        timeTransitionText.raycastTarget = false;
        timeTransitionTextCanvasGroup = timeTextObject.AddComponent<CanvasGroup>();
        var textRect = timeTransitionText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        timeTransitionPanel.SetActive(false);
    }

    private void ShowTimeTransition()
    {
        currentObjective = string.Empty;
        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(true);
            if (timeTransitionCanvasGroup != null)
            {
                timeTransitionCanvasGroup.alpha = 1f;
            }

            if (timeTransitionTextCanvasGroup != null)
            {
                timeTransitionTextCanvasGroup.alpha = 1f;
            }
        }
    }

    private void CreateRetryButton()
    {
        retryButtonObject = new GameObject("RetryButton");
        retryButtonObject.transform.SetParent(dialogueCanvasObject.transform, false);
        var rect = retryButtonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.15f);
        rect.anchorMax = new Vector2(0.5f, 0.15f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(180f, 50f);

        // The GAME OVER overlay uses its own canvas with a very high sorting order, so the retry
        // button needs its own canvas above it or it would be hidden (and unclickable) behind it.
        var retryCanvas = retryButtonObject.AddComponent<Canvas>();
        retryCanvas.overrideSorting = true;
        retryCanvas.sortingOrder = 300;
        retryButtonObject.AddComponent<GraphicRaycaster>();
        var buttonImage = retryButtonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.16f, 0.19f, 0.26f, 0.95f);
        retryButton = retryButtonObject.AddComponent<Button>();
        retryButton.targetGraphic = buttonImage;
        var labelObject = new GameObject("RetryLabel");
        labelObject.transform.SetParent(retryButtonObject.transform, false);
        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "RETRY";
        label.fontSize = 22f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        retryButton.onClick.AddListener(HandleRetryPressed);
        retryButtonObject.SetActive(false);
    }

    private void StartFredArgumentPhone()
    {
        if (phoneMessages == null)
        {
            return;
        }

        phoneMessages.ConfigureSceneConversation();
        phoneMessages.OpenCustomConversation(
            new List<string>
            {
                "Are you crazy? Why did you give that link to my little brother?",
                "Hey, calm down. He asked me for it himself.",
                "He's just a kid! You shouldn't have given it to him.",
                "I didn't force him. He wanted the link, so I sent it.",
                "Whatever. Just don't send my brother that link again, no matter what he says.",
                "Alright, fine."
            },
            new List<bool> { true, false, true, false, true, false },
            null);
        phase = ScenePhase.FredArgumentPhone;
    }

    private void BeginTwoHoursLaterTransition()
    {
        if (phase == ScenePhase.FredArgumentPhone)
        {
            phase = ScenePhase.TwoHoursLaterTransition;
        }

        if (phoneObject != null)
        {
            phoneObject.SetActive(false);
        }

        StartCoroutine(DoTwoHoursLaterTransition());
    }

    private IEnumerator DoTwoHoursLaterTransition()
    {
        LockPlayerControls();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (timeTransitionText != null)
        {
            timeTransitionText.text = "2 HOURS LATER";
        }

        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(true);
        }

        if (timeTransitionCanvasGroup != null)
        {
            timeTransitionCanvasGroup.alpha = 0f;
            yield return FadeCanvasGroup(timeTransitionCanvasGroup, 1f, 0.65f);
        }

        if (timeTransitionTextCanvasGroup != null)
        {
            timeTransitionTextCanvasGroup.alpha = 0f;
            yield return FadeCanvasGroup(timeTransitionTextCanvasGroup, 1f, 0.3f);
        }

        yield return new WaitForSecondsRealtime(1.7f);
        if (timeTransitionTextCanvasGroup != null)
        {
            yield return FadeCanvasGroup(timeTransitionTextCanvasGroup, 0f, 0.35f);
        }

        ApplyNightLighting();
        if (momActor != null)
        {
            momActor.SetActive(false);
        }

        if (brotherActor != null)
        {
            brotherActor.SetActive(false);
        }

        // Fred only exists in the daytime opening scene; once the story jumps to night he is gone.
        if (fredActor != null)
        {
            fredActor.SetActive(false);
        }

        bool characterControllerWasEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (player != null && bedroomBed != null)
        {
            player.transform.position = bedroomBed.position;
        }

        if (playerCamera != null)
        {
            var cameraStartPosition = standingCameraLocalPosition - Vector3.up * 0.45f;
            var cameraStartRotation = Quaternion.Euler(-22f, 0f, 0f);
            playerCamera.transform.localPosition = cameraStartPosition;
            playerCamera.transform.localRotation = cameraStartRotation;
            float elapsed = 0f;
            const float getUpDuration = 0.9f;
            while (elapsed < getUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / getUpDuration));
                playerCamera.transform.localPosition = Vector3.Lerp(cameraStartPosition, standingCameraLocalPosition, progress);
                playerCamera.transform.localRotation = Quaternion.Slerp(cameraStartRotation, Quaternion.identity, progress);
                yield return null;
            }

            playerCamera.transform.localPosition = standingCameraLocalPosition;
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        if (characterController != null)
        {
            characterController.enabled = characterControllerWasEnabled;
        }

        if (timeTransitionCanvasGroup != null)
        {
            yield return FadeCanvasGroup(timeTransitionCanvasGroup, 0f, 0.65f);
        }

        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(false);
        }

        phase = ScenePhase.WakeUpSequence;
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        if (bedroomDoorInteraction != null)
        {
            bedroomDoorInteraction.enabled = false;
        }

        if (bedroomDoorAnimator != null && !string.IsNullOrEmpty(bedroomDoorOpenParameter) && !IsBedroomDoorOpen())
        {
            SetBedroomDoorOpen(true);
        }

        // Keep both wake-up thoughts inside the bedroom; movement resumes after them.
        StartConversation(new List<DialogueLine>
        {
            new DialogueLine("MC", "Where did Mom go? Her phone is broken, so I can't even call her."),
            new DialogueLine("MC", "Let me ask my little brother. Maybe he has an update.")
        }, ScenePhase.WakeUpSequence);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private void StartFredWarningPhone()
    {
        if (phoneMessages == null)
        {
            return;
        }

        if (phoneObject != null)
        {
            phoneObject.SetActive(true);
        }

        currentObjective = string.Empty;
        currentPrompt = string.Empty;

        LockPlayerControls();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        phoneMessages.ConfigureSceneConversation();
        phoneMessages.OpenCustomConversation(
            new List<string>
            {
                "Hey, I saw your little brother on the red room livestream.",
                "You're lying. What are you talking about?",
                "No, trust me. Please believe me this time.",
                "Open the link just once. If I'm lying, you never have to talk to me again."
            },
            new List<bool> { false, true, false, false },
            null);
        warningPhoneOpened = true;
        phase = ScenePhase.FredWarningPhone;
    }

    private IEnumerator OpenFredLinkAfterLoading()
    {
        if (phoneMessages == null)
        {
            yield break;
        }

        LockPlayerControls();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (phoneObject != null)
        {
            phoneObject.SetActive(true);
        }

        phoneMessages.ShowLoadingScreen("LOADING...");
        yield return new WaitForSecondsRealtime(1f);

        var image = LoadPhoneLinkImage();
        if (image == null)
        {
            Debug.LogError("Missing in-game phone image reference: Assets/StoryImages/image 5.png", this);
        }

        phoneMessages.OpenImageViewer(image, "Assets/StoryImages/image 5.png");
        StartConversation(new List<DialogueLine>
        {
            new DialogueLine("MC", "WTF is this?")
        }, ScenePhase.FredImageDialogue);
    }

    private IEnumerator BrotherMissingCinematic()
    {
        if (brotherActor != null)
        {
            brotherActor.SetActive(false);
        }

        LockPlayerControls();
        if (playerCamera != null && homeHouse != null)
        {
            var sofa = homeHouse.Find("PFB_Sofa") ?? homeHouse.Find("PFB_SofaSmall");
            var sofaPoint = sofa != null ? sofa.position + Vector3.up * 0.8f : homeHouse.position + Vector3.up;
            var cameraStartRotation = playerCamera.transform.rotation;
            var sofaRotation = Quaternion.LookRotation(sofaPoint - playerCamera.transform.position, Vector3.up);
            float elapsed = 0f;
            while (elapsed < 1.1f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.1f));
                playerCamera.transform.rotation = Quaternion.Slerp(cameraStartRotation, sofaRotation, t);
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.5f);

            var diningTable = homeHouse.Find("PFB_DiningTable");
            var diningPoint = diningTable != null
                ? diningTable.position + Vector3.up * 0.9f
                : homeHouse.position + Vector3.up;
            cameraStartRotation = playerCamera.transform.rotation;
            var diningRotation = Quaternion.LookRotation(diningPoint - playerCamera.transform.position, Vector3.up);
            elapsed = 0f;
            while (elapsed < 1.1f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.1f));
                playerCamera.transform.rotation = Quaternion.Slerp(cameraStartRotation, diningRotation, t);
                yield return null;
            }

        }

        ReleasePlayerControls();
        currentObjective = "Find Little Brother in the house";
        phase = ScenePhase.BrotherMissingSearch;
    }

    private void StartBathroomShadowSequence()
    {
        if (homeHouse == null)
        {
            return;
        }

        // The figure the player mistakes for their little brother is the real MONSTER 1 "kitty"
        // backrooms entity seen through the bathroom window, not a placeholder capsule.
        var windowPosition = GetBathroomWindowFocusPoint();
        var windowCurtain = homeHouse.Find("PFB_Building_Base/PFB_CurtainSmall");
        var windowFocusPoint = windowPosition + GetCurtainOutsideDirection(windowCurtain) * 0.45f;
        var lookTarget = player != null ? player.transform.position : homeHouse.position;
        bathroomShadowObject = SpawnCreatureModel(
            "BathroomShadow",
            "Assets/Characters/MONSTER 1/kitty_-_backrooms_entity.glb",
            windowPosition,
            lookTarget,
            1.55f);

        LockPlayerControls();
        StartCameraFocusedConversation(windowFocusPoint, new List<DialogueLine>
        {
            new DialogueLine("MC", "Maybe he's outside.")
        }, ScenePhase.BathroomShadow);
        StartCoroutine(HideShadowAfterDelay());
    }

    private IEnumerator HideShadowAfterDelay()
    {
         while (cameraFocusActive ||
             (phase == ScenePhase.BathroomShadow && (!dialogueActive || isTyping)))
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1f);
        if (bathroomShadowObject != null)
        {
            var bathroomSink = homeHouse != null ? homeHouse.Find("PFB_BathroomSink") : null;
            var bathroomCurtain = homeHouse != null
                ? homeHouse.Find("PFB_Building_Base/PFB_CurtainSmall")
                : null;
            var runDirection = bathroomSink != null && bathroomCurtain != null
                ? bathroomCurtain.position - bathroomSink.position
                : Vector3.forward;
            runDirection.y = 0f;
            if (runDirection.sqrMagnitude < 0.001f)
            {
                runDirection = Vector3.forward;
            }

            var startPosition = bathroomShadowObject.transform.position;
            var endPosition = startPosition + runDirection.normalized * 1.4f;
            float elapsed = 0f;
            const float runDuration = 0.45f;
            while (elapsed < runDuration && bathroomShadowObject != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / runDuration));
                bathroomShadowObject.transform.position = Vector3.Lerp(startPosition, endPosition, progress);
                yield return null;
            }

            Destroy(bathroomShadowObject);
            bathroomShadowObject = null;
        }
    }

    private void StartMomNoteSequence()
    {
        if (homeHouse != null && noteObject == null && houseEntranceDoor != null)
        {
            noteObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            noteObject.name = "MomNote";
            var insideDirection = homeHouse.position - houseEntranceDoor.position;
            insideDirection.y = 0f;
            if (insideDirection.sqrMagnitude < 0.001f)
            {
                insideDirection = homeHouse.forward;
            }

            var notePosition = houseEntranceDoor.position + insideDirection.normalized * 1.25f +
                               Vector3.Cross(Vector3.up, insideDirection.normalized) * 0.45f;

            // Ground the note on the real floor under that spot. It is a flat paper-like cube whose
            // Y scale (0.02) is its thickness, so lift it by half of that plus a hair to avoid
            // z-fighting with the floor collider.
            float floorY = ResolveInteriorFloorHeight(notePosition);
            notePosition.y = floorY + 0.012f;
            noteObject.transform.position = notePosition;
            noteObject.transform.rotation = Quaternion.Euler(0f, homeHouse.eulerAngles.y, 0f);
            noteObject.transform.localScale = new Vector3(0.55f, 0.02f, 0.42f);
            var renderer = noteObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(GetPlaceholderShader());
                mat.color = new Color(0.96f, 0.94f, 0.82f);
                renderer.sharedMaterial = mat;
            }
            var collider = noteObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            // Small glowing floating marker above the note so it reads clearly on a dark floor and
            // stays visible from any angle. It is a child, so it moves with the note.
            noteMarkerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            noteMarkerObject.name = "MomNoteMarker";
            noteMarkerObject.transform.SetParent(noteObject.transform, false);
            noteMarkerObject.transform.localPosition = new Vector3(0f, 5f, 0f);
            noteMarkerObject.transform.localRotation = Quaternion.identity;
            noteMarkerObject.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            var markerCollider = noteMarkerObject.GetComponent<Collider>();
            if (markerCollider != null)
            {
                Destroy(markerCollider);
            }

            var markerRenderer = noteMarkerObject.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                var markerMaterial = new Material(GetPlaceholderShader());
                markerMaterial.color = new Color(1f, 0.86f, 0.35f, 1f);
                markerRenderer.sharedMaterial = markerMaterial;
            }
        }
    }

    // Interior floor height: find the real floor under the point by raycasting straight down and
    // taking the highest hit that is not above the door base (which rejects a roof or a ceiling,
    // and also rejects the player's own collider). This works even while the player is still
    // standing outside the house.
    private float ResolveInteriorFloorHeight(Vector3 position)
    {
        float ceilingLimit;
        if (houseEntranceDoor != null && homeHouse != null)
        {
            // Anything at or above the base of the front door is a wall/roof, not the floor.
            ceilingLimit = Mathf.Max(houseEntranceDoor.position.y, homeHouse.position.y) + 0.5f;
        }
        else
        {
            ceilingLimit = position.y + 1f;
        }

        var rayOrigin = position + Vector3.up * 4f;
        var hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            12f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            if (player != null && hits[i].transform != null &&
                (hits[i].transform == player.transform || hits[i].transform.IsChildOf(player.transform)))
            {
                continue;
            }

            float hitY = hits[i].point.y;
            if (hitY <= ceilingLimit && hitY > best)
            {
                best = hitY;
            }
        }

        if (best > float.NegativeInfinity)
        {
            return best;
        }

        return characterController != null && characterController.enabled
            ? characterController.bounds.min.y
            : player != null
                ? player.transform.position.y
                : position.y;
    }

    private void ReadMomNote()
    {
        StartConversation(new List<DialogueLine>
        {
            new DialogueLine("Note", "I'll be late coming home. I went to Aunt Ela's house. I left dinner in the fridge. Eat it.\n\n— Mom")
        }, ScenePhase.MomNoteText);
        currentPrompt = string.Empty;
    }

    private void StartMysteriousManSequence()
    {
        if (homeHouse == null)
        {
            return;
        }

        PlayImpact(horrorHitSfxClip, 0.58f, 1.04f);

        var insideDirection = houseEntranceDoor != null
            ? homeHouse.position - houseEntranceDoor.position
            : homeHouse.forward;
        insideDirection.y = 0f;
        if (insideDirection.sqrMagnitude < 0.001f)
        {
            insideDirection = homeHouse.forward;
        }

        var outwardDirection = -insideDirection.normalized;
        var silhouettePosition = (houseEntranceDoor != null ? houseEntranceDoor.position : homeHouse.position) +
                     outwardDirection * 6f;
        silhouettePosition.y = ResolveGroundHeight(silhouettePosition);

        // Use the real MONSTER 1 model (the "kitty" backrooms entity) for the intruder the player
        // mistakes for their brother, instead of the old grey capsule.
        suspiciousSilhouetteObject = SpawnCreatureModel(
            "MysteriousManSilhouette",
            "Assets/Characters/MONSTER 1/kitty_-_backrooms_entity.glb",
            silhouettePosition,
            homeHouse.position);

        StartCameraFocusedConversation(suspiciousSilhouetteObject.transform.position, new List<DialogueLine>
        {
            new DialogueLine("MC", "Shit, who is that?")
        }, ScenePhase.MysteriousMan);
    }

    // The creature has just been spotted. Kill the silhouette, make sure the player is back on
    // their feet in the house, then start the 5-second dash to the room.
    private void StartEscapeDanger(string introLine)
    {
        if (bedroomDoorInteraction != null)
        {
            bedroomDoorInteraction.enabled = false;
        }

        if (houseEntranceDoorInteraction != null)
        {
            houseEntranceDoorInteraction.enabled = false;
        }

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = standingCameraLocalPosition;
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        ReleasePlayerControls();

        currentObjective = "Run to your room and close the door!";
        currentPrompt = string.Empty;
        phase = ScenePhase.EscapeDanger;

        if (!string.IsNullOrEmpty(introLine))
        {
            StartCoroutine(StartEscapeDangerAfterIntro(introLine));
        }
        else
        {
            StartEscapeDangerTimer();
        }
    }

    private IEnumerator StartEscapeDangerAfterIntro(string introLine)
    {
        // Show the one-line reaction first so it does not clash with the countdown prompt, then
        // hand control back and let the 5 seconds begin.
        dialogueActive = true;
        activeDialogue = new List<DialogueLine> { new DialogueLine("MC", introLine) };
        dialogueIndex = 0;
        ShowDialogueUi();
        DisplayCurrentDialogue();

        while (dialogueActive)
        {
            yield return null;
        }

        UnlockAndResetMovement();
        StartEscapeDangerTimer();
    }

    private bool IsHouseEntranceDoorClosed()
    {
        return houseEntranceDoorAnimator != null &&
               !string.IsNullOrEmpty(houseEntranceDoorOpenParameter) &&
               !houseEntranceDoorAnimator.GetBool(houseEntranceDoorOpenParameter);
    }

    private bool IsHouseEntranceDoorOpen()
    {
        return houseEntranceDoorAnimator != null &&
               !string.IsNullOrEmpty(houseEntranceDoorOpenParameter) &&
               houseEntranceDoorAnimator.GetBool(houseEntranceDoorOpenParameter);
    }

    private void SetHouseEntranceDoorOpen(bool isOpen)
    {
        if (houseEntranceDoorAnimator == null || string.IsNullOrEmpty(houseEntranceDoorOpenParameter))
        {
            Debug.LogError("Scene2EveningManager cannot control the entrance door because its Animator bool is missing.", this);
            return;
        }

        houseEntranceDoorAnimator.enabled = true;
        houseEntranceDoorAnimator.SetBool(houseEntranceDoorOpenParameter, isOpen);
    }

    private bool IsDoorInReach(Transform door)
    {
        if (playerCamera == null || door == null ||
            Vector3.Distance(playerCamera.transform.position, door.position) > 3.5f)
        {
            return false;
        }

        if (!Physics.Raycast(
                playerCamera.transform.position,
                playerCamera.transform.forward,
                out var hit,
                3.5f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return hit.transform == door || hit.transform.IsChildOf(door);
    }

    private bool IsDoorNearPlayer(Transform door)
    {
        return playerCamera != null && door != null &&
               Vector3.Distance(playerCamera.transform.position, door.position) <= 3.5f;
    }

    private void ThrowDistraction()
    {
        if (distractionClockPickedUp || distractionProp == null)
        {
            return;
        }

        distractionClockPickedUp = true;
        StartCoroutine(ThrowDistractionRoutine());
    }

    private void PrepareDistractionProp()
    {
        if (playerCamera == null)
        {
            return;
        }

        if (distractionProp != null)
        {
            Destroy(distractionProp);
        }

        distractionProp = new GameObject("AlarmClockDistraction");
        distractionProp.transform.SetParent(transform, true);
        var bedPlacement = GetAlarmClockBedPosition();
        distractionProp.transform.position = bedPlacement;
        distractionProp.transform.localScale = Vector3.one * 0.11f;
        RemoveExistingBedsideTableDevice();

        // The supplied clock is split across seven OBJ mesh parts. They share the same origin,
        // so assemble them together and tint the parts because the ZIP contains no MTL/textures.
        Color[] partColors =
        {
            new Color(0.16f, 0.055f, 0.045f), // painted casing
            new Color(0.22f, 0.075f, 0.055f), // upper casing and bells
            new Color(0.83f, 0.76f, 0.61f),   // clock face
            new Color(0.62f, 0.39f, 0.16f),   // metal details
            new Color(0.10f, 0.075f, 0.055f), // hands
            new Color(0.12f, 0.045f, 0.035f), // rear casing
            new Color(0.73f, 0.65f, 0.50f)    // bezel
        };

        for (int i = 0; i < partColors.Length; i++)
        {
            var modelPart = Resources.Load<GameObject>("AlarmClock/model_" + i);
            if (modelPart == null)
            {
                Debug.LogError("Missing alarm clock model part in Resources: model_" + i, this);
                continue;
            }

            var part = Instantiate(modelPart, distractionProp.transform);
            part.name = "AlarmClockPart_" + i;
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = Vector3.one;
            var material = new Material(GetPlaceholderShader()) { color = partColors[i] };
            foreach (var partRenderer in part.GetComponentsInChildren<Renderer>(true))
            {
                partRenderer.sharedMaterial = material;
            }
        }

        var clockRenderers = distractionProp.GetComponentsInChildren<Renderer>(true);
        if (clockRenderers.Length > 0)
        {
            var clockBounds = clockRenderers[0].bounds;
            for (int i = 1; i < clockRenderers.Length; i++)
            {
                clockBounds.Encapsulate(clockRenderers[i].bounds);
            }

            // The imported meshes' origin is near the middle of the clock. Align the assembled
            // mesh bottom to the bedside surface so the feet/casing sit on it instead of floating
            // or sinking below it.
            distractionProp.transform.position += new Vector3(
                bedPlacement.x - clockBounds.center.x,
                bedPlacement.y - clockBounds.min.y,
                bedPlacement.z - clockBounds.center.z);
        }

        var towardPlayer = playerCamera.transform.position - distractionProp.transform.position;
        towardPlayer.y = 0f;
        if (towardPlayer.sqrMagnitude > 0.001f)
        {
            // The OBJ clock's dial faces local -Z; aim it toward the player at the bedside.
            distractionProp.transform.rotation = Quaternion.LookRotation(-towardPlayer.normalized, Vector3.up);
        }
    }

    private Vector3 GetAlarmClockBedPosition()
    {
        var bedsideTable = FindBedroomSideTable();
        if (bedsideTable != null)
        {
            var tableRenderers = bedsideTable.GetComponentsInChildren<Renderer>(true);
            if (tableRenderers.Length > 0)
            {
                var tableBounds = tableRenderers[0].bounds;
                for (int i = 1; i < tableRenderers.Length; i++)
                {
                    tableBounds.Encapsulate(tableRenderers[i].bounds);
                }

                // Place the clock at the middle of the tabletop; the existing tabletop device
                // is removed separately so the clock has a clear, stable spot.
                return new Vector3(tableBounds.center.x, tableBounds.max.y + 0.012f, tableBounds.center.z);
            }

            return bedsideTable.position + bedsideTable.up * 0.52f;
        }

        if (bedroomBed == null)
        {
            return playerCamera.transform.position + playerCamera.transform.forward * 1.1f;
        }

        var renderers = bedroomBed.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return bedroomBed.position + bedroomBed.up * 0.55f + bedroomBed.right * 0.35f;
        }

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        var position = bounds.center + bedroomBed.right * (bounds.extents.x * 0.58f) +
                       bedroomBed.forward * (bounds.extents.z * 0.18f);
        position.y = bounds.max.y + 0.045f;
        return position;
    }

    private Transform FindBedroomSideTable()
    {
        if (homeHouse == null || bedroomBed == null)
        {
            return null;
        }

        Transform nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (var candidate in homeHouse.GetComponentsInChildren<Transform>(true))
        {
            if (!candidate.name.StartsWith("PFB_BedsideTable", System.StringComparison.Ordinal))
            {
                continue;
            }

            float distance = HorizontalDistance(candidate.position, bedroomBed.position);
            if (distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void RemoveExistingBedsideTableDevice()
    {
        var bedsideTable = FindBedroomSideTable();
        if (bedsideTable == null || homeHouse == null)
        {
            return;
        }

        Transform device = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (var candidate in homeHouse.GetComponentsInChildren<Transform>(true))
        {
            string objectName = candidate.name;
            bool isTabletopDevice = objectName.IndexOf("PDA", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    objectName.IndexOf("Phone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    objectName.StartsWith("PFB_Lamp", System.StringComparison.Ordinal);
            if (!isTabletopDevice)
            {
                continue;
            }

            float distance = HorizontalDistance(candidate.position, bedsideTable.position);
            if (distance < 0.8f && distance < nearestDistance)
            {
                device = candidate;
                nearestDistance = distance;
            }
        }

        if (device != null)
        {
            Destroy(device.gameObject);
        }
    }

    private IEnumerator ThrowDistractionRoutine()
    {
        LockPlayerControls();
        var bedroomCurtain = FindBedroomWindowCurtain();
        var outsideDirection = GetCurtainOutsideDirection(bedroomCurtain);
        var cameraStartPosition = playerCamera.transform.position;
        var cameraStartRotation = playerCamera.transform.rotation;
        var cameraStartLocalRotation = playerCamera.transform.localRotation;
        var start = distractionProp != null ? distractionProp.transform.position : cameraStartPosition;
        var handPosition = cameraStartPosition + playerCamera.transform.forward * 0.58f -
                           playerCamera.transform.up * 0.20f;
        var clockFacing = Quaternion.LookRotation(playerCamera.transform.forward, Vector3.up);
        var pickupCameraRotation = cameraStartRotation * Quaternion.Euler(11f, 0f, 0f);
        float elapsed = 0f;
        const float pickupDuration = 0.55f;
        while (elapsed < pickupDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / pickupDuration);
            if (distractionProp != null)
            {
                distractionProp.transform.position = Vector3.Lerp(start, handPosition, progress) +
                                                     Vector3.up * Mathf.Sin(progress * Mathf.PI) * 0.10f;
                distractionProp.transform.rotation = Quaternion.Slerp(
                    distractionProp.transform.rotation, clockFacing, progress);
            }
            if (playerCamera != null)
            {
                playerCamera.transform.rotation = Quaternion.Slerp(cameraStartRotation, pickupCameraRotation, progress);
            }
            yield return null;
        }

        // Briefly hold the clock in view, then swing toward the bedroom window and throw it out.
        yield return new WaitForSecondsRealtime(0.22f);
        var throwWindowRenderer = bedroomCurtain != null ? bedroomCurtain.GetComponentInChildren<Renderer>() : null;
        var windowPosition = throwWindowRenderer != null
            ? throwWindowRenderer.bounds.center
            : bedroomCurtain != null ? bedroomCurtain.position + Vector3.up * 1.3f : handPosition + outsideDirection * 2f;
        var throwCameraWindowRotation = Quaternion.LookRotation(windowPosition - playerCamera.transform.position, Vector3.up);
        var throwStart = distractionProp != null ? distractionProp.transform.position : handPosition;
        var throwEnd = (throwWindowRenderer != null ? throwWindowRenderer.bounds.center : windowPosition) +
                       outsideDirection * 2.2f + Vector3.down * 0.25f;
        const float throwDuration = 0.65f;
        elapsed = 0f;
        while (elapsed < throwDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / throwDuration);
            if (distractionProp != null)
            {
                distractionProp.transform.position = Vector3.Lerp(throwStart, throwEnd, progress) +
                                                     Vector3.up * Mathf.Sin(progress * Mathf.PI) * 0.75f;
            }
            if (playerCamera != null)
            {
                playerCamera.transform.rotation = Quaternion.Slerp(
                    pickupCameraRotation, throwCameraWindowRotation, progress);
            }
            yield return null;
        }

        if (distractionProp != null)
        {
            distractionProp.transform.position = throwEnd;
            Destroy(distractionProp);
            distractionProp = null;
        }
        distractionThrown = true;
        if (audioSource != null && notificationClip != null)
        {
            audioSource.PlayOneShot(notificationClip);
        }
        yield return new WaitForSecondsRealtime(0.8f);
        if (playerCamera != null)
        {
            playerCamera.transform.rotation = cameraStartRotation;
            playerCamera.transform.localPosition = standingCameraLocalPosition;
        }

        // Show Monster 1 approaching from outside the bedroom window used for the distraction.
        // Keep the player safely inside during this short look, then start the escape timer.
        SpawnPursuingCreature(true);
        var bedroomWindow = FindBedroomWindowCurtain();
        var windowRenderer = bedroomWindow != null ? bedroomWindow.GetComponentInChildren<Renderer>() : null;
        var windowLookPoint = windowRenderer != null
            ? windowRenderer.bounds.center + Vector3.up * 0.15f
            : bedroomWindow != null
                ? bedroomWindow.position + Vector3.up * 1.4f
                : playerCamera != null
                    ? playerCamera.transform.position + playerCamera.transform.forward * 3f
                    : Vector3.zero;
        var cameraStartWorldRotation = playerCamera != null ? playerCamera.transform.rotation : Quaternion.identity;
        var cameraWindowRotation = playerCamera != null
            ? Quaternion.LookRotation(windowLookPoint - playerCamera.transform.position, Vector3.up)
            : cameraStartWorldRotation;
        float lookElapsed = 0f;
        const float monsterWindowRevealDuration = 1.5f;
        while (lookElapsed < monsterWindowRevealDuration)
        {
            lookElapsed += Time.unscaledDeltaTime;
            if (playerCamera != null)
            {
                playerCamera.transform.rotation = Quaternion.Slerp(
                    cameraStartWorldRotation,
                    cameraWindowRotation,
                    Mathf.SmoothStep(0f, 1f, lookElapsed / 0.35f));
            }
            UpdatePursuingCreature();
            yield return null;
        }
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = cameraStartLocalRotation;
        }
        ReleasePlayerControls();

        // The intruder is distracted. The player now has 5 seconds to burst out of the bedroom,
        // through the house and out onto the road before the intruder turns back around.
        StartFinalEscapeDash();
    }

    private void StartFinalEscapeDash()
    {
        // Open the bedroom and front doors so the tease that trapped the player actually lets them
        // out, and re-enable the entrance interaction in case it was disabled earlier.
        if (bedroomDoorInteraction != null)
        {
            bedroomDoorInteraction.enabled = false;
        }

        SetBedroomDoorOpen(true);
        SetHouseEntranceDoorOpen(true);

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = standingCameraLocalPosition;
        }

        finalEscapeActive = true;
        finalEscapeRemaining = 5f;
        currentObjective = "Run outside to the road!";
        currentPrompt = "GET OUT  •  5s";
        phase = ScenePhase.FinalEscape;
        SpawnPursuingCreature(true);
    }

    private void UpdateFinalEscape()
    {
        if (!finalEscapeActive)
        {
            return;
        }

        finalEscapeRemaining -= Time.unscaledDeltaTime;
        UpdatePursuingCreature();

        if (IsPlayerOnRoad())
        {
            finalEscapeActive = false;
            horrorAmbienceJourneyActive = false;
            DestroyPursuingCreature();
            currentObjective = string.Empty;
            currentPrompt = string.Empty;
            phase = ScenePhase.Finished;

            // The player is safely outside and now heads for Aunt Ela's house across the jungle, so
            // this is when the yellow location pin appears.
            SetAuntElaMarkerVisible(true);
            jungleChaseStarted = false;
            jungleChaseTravelledBeforeTrigger = 0f;
            jungleChaseTravelTrackingInitialized = false;

            StartConversation(new List<DialogueLine>
            {
                new DialogueLine("MC", "Made it. I am going straight to Aunt Ela's house.")
            }, ScenePhase.Finished);
            return;
        }

        if (finalEscapeRemaining <= 0f)
        {
            finalEscapeActive = false;
            StartCreatureRush();
            return;
        }

        currentPrompt = "GET OUT  •  " + Mathf.CeilToInt(Mathf.Max(0f, finalEscapeRemaining)) + "s";
    }

    // The player counts as escaped once they are outside the house and reasonably far from it.
    private bool IsPlayerOnRoad()
    {
        if (player == null)
        {
            return false;
        }

        if (IsPlayerInside(houseTrigger))
        {
            return false;
        }

        if (!IsOutsideHouse())
        {
            return false;
        }

        if (homeHouse == null)
        {
            return true;
        }

        var toPlayer = player.transform.position - homeHouse.position;
        toPlayer.y = 0f;
        return toPlayer.magnitude >= 9f;
    }

    // The creature rushes the player's screen and the escape timer runs out: game over.
    private void StartCreatureRush()
    {
        if (creatureRushActive)
        {
            return;
        }

        creatureRushActive = true;
        StartCoroutine(CreatureRushRoutine());
    }

    private IEnumerator CreatureRushRoutine()
    {
        escapeTimerStarted = false;
        LockPlayerControls();
        currentObjective = string.Empty;
        currentPrompt = string.Empty;

        // Continue from the creature the player was already watching, so the failure reads as the
        // same creature lunging forward instead of a brand new object popping into view.
        GameObject creature = pursuingCreatureObject;
        pursuingCreatureObject = null;
        pursuingCreatureAtBedroomWindow = false;
        if (creature == null && playerCamera != null)
        {
            creature = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            creature.name = "RushingCreature";
            var creatureCollider = creature.GetComponent<Collider>();
            if (creatureCollider != null)
            {
                Destroy(creatureCollider);
            }

            var creatureRenderer = creature.GetComponent<Renderer>();
            if (creatureRenderer != null)
            {
                var creatureMaterial = new Material(GetPlaceholderShader());
                creatureMaterial.color = new Color(0.02f, 0.02f, 0.03f, 1f);
                creatureRenderer.sharedMaterial = creatureMaterial;
            }

            creature.transform.position =
                playerCamera.transform.position + playerCamera.transform.forward * 9f + Vector3.up * 1.5f;
            creature.transform.localScale = new Vector3(0.9f, 1.9f, 0.9f);
        }

        if (creature != null && playerCamera != null)
        {
            var renderers = creature.GetComponentsInChildren<Renderer>(true);
            var startBounds = GetCombinedRendererBounds(renderers, creature.transform.position);
            var startPosition = creature.transform.position;
            var startScale = creature.transform.localScale;
            var impactCenter = playerCamera.transform.position + playerCamera.transform.forward * 0.52f;
            float viewHeight = 2f * 0.52f * Mathf.Tan(playerCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float scaleForFrameFill = viewHeight * 2.6f / Mathf.Max(0.01f, startBounds.size.y);
            var impactScale = startScale * Mathf.Clamp(Mathf.Max(2.2f, scaleForFrameFill), 2.2f, 4.5f);
            var startCenter = startBounds.center;
            float elapsed = 0f;
            const float rushDuration = 0.5f;
            while (elapsed < rushDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / rushDuration));
                if (creature != null)
                {
                    creature.transform.localScale = Vector3.Lerp(startScale, impactScale, progress);
                    creature.transform.rotation = Quaternion.LookRotation(-playerCamera.transform.forward, Vector3.up);
                    creature.transform.position = Vector3.Lerp(startPosition, impactCenter, progress);

                    // Monster assets have different pivot heights. Align their visible mesh bounds
                    // to the camera target each frame so the lunge fills the view instead of rising
                    // above it as the model scales around its feet.
                    var currentBounds = GetCombinedRendererBounds(renderers, creature.transform.position);
                    var desiredCenter = Vector3.Lerp(startCenter, impactCenter, progress);
                    creature.transform.position += desiredCenter - currentBounds.center;
                }

                yield return null;
            }

            if (creature != null)
            {
                var finalBounds = GetCombinedRendererBounds(renderers, creature.transform.position);
                creature.transform.position += impactCenter - finalBounds.center;
            }
        }

        if (audioSource != null && notificationClip != null)
        {
            audioSource.PlayOneShot(notificationClip);
        }

        if (creature != null)
        {
            Destroy(creature);
        }

        creatureRushActive = false;
        TriggerGameOver();
    }

    private static Bounds GetCombinedRendererBounds(Renderer[] renderers, Vector3 fallbackPosition)
    {
        if (renderers == null || renderers.Length == 0)
            return new Bounds(fallbackPosition, Vector3.one);

        bool found = false;
        var bounds = new Bounds(fallbackPosition, Vector3.one);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            if (!found)
            {
                bounds = renderers[i].bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }
        return bounds;
    }

    // Player reached the room and closed the door in time: the room is safe, so the story goes on.
    private void StartEscapeRoomSurvived()
    {
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        phase = ScenePhase.RoomSecured;
        StartConversation(new List<DialogueLine>
        {
            new DialogueLine("MC", "That was too close..."),
            new DialogueLine("MC", "I have to distract him and get out of here.")
        }, ScenePhase.RoomSecured);
    }

    private void StartEscapeDangerTimer()
    {
        if (escapeTimerStarted)
        {
            return;
        }

        // Make sure the room door is open when the dash begins, otherwise the challenge would be
        // satisfied instantly and the player would skip it.
        if (bedroomDoorAnimator != null && !string.IsNullOrEmpty(bedroomDoorOpenParameter) &&
            !IsBedroomDoorOpen())
        {
            SetBedroomDoorOpen(true);
        }

        if (bedroomDoorInteraction != null)
        {
            bedroomDoorInteraction.enabled = false;
        }

        escapeTimerStarted = true;
        escapeTimerRemaining = 5f;
        escapeTimerExpired = false;
        currentObjective = "Get into your room and close the door!";

        // Keep the first Monster 1 visible throughout the player's "Shit, who is that?" reaction.
        // Replace it with the active pursuer only when the countdown actually begins.
        if (suspiciousSilhouetteObject != null)
        {
            Destroy(suspiciousSilhouetteObject);
            suspiciousSilhouetteObject = null;
        }

        SpawnPursuingCreature();
    }

    // Spawns the creature a short distance away and lets it creep closer during the countdown so
    // the player can actually see the threat instead of it appearing out of nowhere on failure.
    private void SpawnPursuingCreature(bool approachBedroomWindow = false)
    {
        if (pursuingCreatureObject != null)
        {
            return;
        }

        pursuingCreatureAtBedroomWindow = approachBedroomWindow;
        var bedroomCurtain = FindBedroomWindowCurtain();
        if (approachBedroomWindow && bedroomCurtain != null)
        {
            var curtainRenderer = bedroomCurtain.GetComponentInChildren<Renderer>();
            var windowPosition = curtainRenderer != null ? curtainRenderer.bounds.center : bedroomCurtain.position;
            var outsideDirection = GetCurtainOutsideDirection(bedroomCurtain);
            // Keep the monster outdoors, directly beyond the same bedroom window the player used.
            // Keep its body centered on the window opening: terrain raycasts can hit the roof or
            // overhead foliage here and incorrectly place the monster above the bedroom.
            var windowTargetPosition = windowPosition + outsideDirection * 1.2f;
            var windowStartPosition = windowTargetPosition + outsideDirection * 8f;
            pursuingCreatureObject = SpawnCreatureModel(
                "PursuingMonster1AtBedroomWindow",
                "Assets/Characters/MONSTER 1/kitty_-_backrooms_entity.glb",
                windowStartPosition,
                windowPosition + Vector3.up * 1.4f,
                1.8f);
            var monsterRenderers = pursuingCreatureObject.GetComponentsInChildren<Renderer>(true);
            if (monsterRenderers.Length > 0)
            {
                var monsterBounds = monsterRenderers[0].bounds;
                for (int i = 1; i < monsterRenderers.Length; i++)
                {
                    monsterBounds.Encapsulate(monsterRenderers[i].bounds);
                }

                // Center the actual rendered mesh in the window regardless of its imported pivot
                // or the ground height returned by the exterior raycast.
                pursuingCreatureObject.transform.position += Vector3.up * (windowPosition.y - monsterBounds.center.y);
            }

            pursuingCreatureTargetPosition = windowTargetPosition;
            pursuingCreatureTargetPosition.y = pursuingCreatureObject.transform.position.y;
            return;
        }

        var approachDirection = bedroomCurtain != null && player != null
            ? bedroomCurtain.position - player.transform.position
            : homeHouse != null && player != null
                ? homeHouse.position - player.transform.position
                : player != null
                    ? player.transform.forward
                    : Vector3.forward;
        approachDirection.y = 0f;
        if (approachDirection.sqrMagnitude < 0.001f)
        {
            approachDirection = player != null ? player.transform.forward : Vector3.forward;
        }

        pursuingCreatureTargetPosition = player != null
            ? player.transform.position + approachDirection.normalized * 1.6f
            : approachDirection.normalized * 1.6f;
        pursuingCreatureTargetPosition.y = ResolveInteriorFloorHeight(pursuingCreatureTargetPosition);

        var targetFloorPosition = pursuingCreatureTargetPosition;
        var startPosition = targetFloorPosition - approachDirection.normalized * 4.5f;
        float startFloorY = ResolveInteriorFloorHeight(startPosition);
        startPosition.y = startFloorY;
        var lookAtPosition = player != null ? player.transform.position : targetFloorPosition;
        pursuingCreatureObject = SpawnCreatureModel(
            "PursuingMonster1",
            "Assets/Characters/MONSTER 1/kitty_-_backrooms_entity.glb",
            startPosition,
            lookAtPosition,
            1.85f);

        // SpawnCreatureModel normally grounds outdoor creatures. This chase is indoors, so place
        // the same Monster 1 mesh on the real bedroom floor and store its own pivot-to-feet offset.
        var renderers = pursuingCreatureObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            pursuingCreatureObject.transform.position += Vector3.up * (startFloorY - bounds.min.y);
        }

        var feetOffset = pursuingCreatureObject.GetComponent<MonsterFeetOffset>();
        if (feetOffset == null)
        {
            feetOffset = pursuingCreatureObject.AddComponent<MonsterFeetOffset>();
        }
        feetOffset.feetOffset = pursuingCreatureObject.transform.position.y - startFloorY;
    }

    // Called every frame while the countdown runs: the creature slowly closes the distance.
    private void UpdatePursuingCreature()
    {
        if (pursuingCreatureObject == null)
        {
            return;
        }

        var target = pursuingCreatureAtBedroomWindow
            ? pursuingCreatureTargetPosition
            : pursuingCreatureTargetPosition + Vector3.up * GetMonsterFeetOffset(pursuingCreatureObject);
        pursuingCreatureObject.transform.position = Vector3.MoveTowards(
            pursuingCreatureObject.transform.position,
            target,
            (pursuingCreatureAtBedroomWindow ? 2.8f : 1.15f) * Time.unscaledDeltaTime);
        if (pursuingCreatureAtBedroomWindow && homeHouse != null)
        {
            var lookDirection = homeHouse.position - pursuingCreatureObject.transform.position;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                pursuingCreatureObject.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            }
        }
    }

    private void DestroyPursuingCreature()
    {
        if (pursuingCreatureObject != null)
        {
            Destroy(pursuingCreatureObject);
            pursuingCreatureObject = null;
        }
        pursuingCreatureAtBedroomWindow = false;
    }

    // ---------------------------------------------------------------------------------------
    // Jungle chase: after "Made it" the player heads for Aunt Ela's house. The moment they get
    // out onto the road, a creature rises up BEHIND them, the player panics ("What is that?" /
    // "I have to run!") and it gives chase at just under the player's sprint speed. Roughly 70 m
    // short of Aunt Ela's house it suddenly vanishes ("Where did it go?"). If it catches the
    // player, the game-over panel appears and retry restarts the run from the road, not earlier.
    // ---------------------------------------------------------------------------------------

    // The player's own house is behind them now; if they push forward from the road the ambush
    // fires. Distance-based so it works no matter how the road is laid out.
    private void CheckStartJungleChase()
    {
        if (jungleChaseStarted || player == null)
        {
            return;
        }

        var auntEla = ResolveAuntElaPosition();
        if (auntEla == Vector3.zero)
        {
            return;
        }

        // Measure actual player movement after the Aunt Ela trip begins. The house-center distance
        // is misleading because the door can already be 11 m from the house pivot; this requires
        // a real 10 m of travel and still allows the player to take the side route.
        var currentPosition = player.transform.position;
        currentPosition.y = 0f;
        if (!jungleChaseTravelTrackingInitialized)
        {
            jungleChaseLastTrackedPosition = currentPosition;
            jungleChaseTravelTrackingInitialized = true;
            return;
        }

        var movement = currentPosition - jungleChaseLastTrackedPosition;
        movement.y = 0f;
        jungleChaseTravelledBeforeTrigger += movement.magnitude;
        jungleChaseLastTrackedPosition = currentPosition;
        if (jungleChaseTravelledBeforeTrigger < 10f)
        {
            return;
        }

        jungleChaseStarted = true;
        StartJungleChase();
    }

    private void StartJungleChase()
    {
        if (player == null)
        {
            return;
        }

        var auntEla = ResolveAuntElaPosition();

        // The monster rises up BEHIND the player, on the road they just walked down, so it feels
        // like something has been following them and finally makes itself known.
        var toAuntEla = auntEla - player.transform.position;
        toAuntEla.y = 0f;
        if (toAuntEla.sqrMagnitude < 0.001f)
        {
            toAuntEla = player.transform.forward;
        }

        var forward = toAuntEla.normalized;      // direction the player is travelling
        var behind = -forward;                   // opposite: directly behind the player
        // Give the player a little more room to turn and start running, while keeping the
        // creature close enough to feel like an immediate chase.
        var spawnPoint = player.transform.position + behind * 10.5f;
        spawnPoint.y = ResolveExteriorGroundHeight(spawnPoint);

        jungleChaseMonster = SpawnCreatureModel(
            "JungleChaseMonster",
            "Assets/Characters/MONSTER 2/the_backrooms-entity_3_smiler.glb",
            spawnPoint,
            player.transform.position);

        heartbeatActive = true;
        PlayImpact(horrorHitSfxClip, 0.62f, 0.96f);

        // Just under the player's sprint speed: it never truly outruns them, but it never lets
        // them relax either. If the player never sprints it will catch up, so they must run.
        jungleChaseSpeed = Mathf.Max(2f, GetPlayerSprintSpeed() * 0.9f);
        jungleChaseVanishShown = false;
        jungleChaseCatchTimer = 0f;

        // Remember where the chase began so a death restarts the run right here, not back at the
        // house or any earlier mission.
        jungleChaseCheckpointPosition = player.transform.position;
        jungleChaseCheckpointValid = true;
        gameOverFromJungleChase = false;

        // A short panic beat before the run begins. The camera swings onto the creature so the
        // player actually sees what is stalking them instead of only hearing about it.
        var monsterFocusPoint = jungleChaseMonster.transform.position + Vector3.up * 1.1f;
        StartFocusedConversation(jungleChaseStartDialogue, ScenePhase.JungleChaseSpotted, monsterFocusPoint);
    }

    // Spawns the real MONSTER 2 model (the rigged backrooms "smiler"), pointed at the player, with
    // its bundled animation playing on loop so it reads as a running/pursuing creature. Falls back
    // to a dark capsule placeholder if the model cannot be loaded.
    // Spawns a rigged creature model at the given spot, facing the player, with its bundled clip
    // looping so it reads as a running/pursuing creature. The model is automatically placed so its
    // feet sit exactly on the ground (using renderer bounds) and scaled to a human-ish height, which
    // is what stops it from sinking into or floating above the terrain.
    private GameObject SpawnCreatureModel(
        string name,
        string assetPath,
        Vector3 position,
        Vector3 lookAtPosition,
        float targetHeight = 1.85f)
    {
        var facing = lookAtPosition - position;
        facing.y = 0f;
        var rotation = facing.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(facing.normalized, Vector3.up)
            : Quaternion.identity;

#if UNITY_EDITOR
        var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (model != null)
        {
            // Keep the asset's imported root rotation beneath a separate world-facing pivot.
            // Instantiating the GLB as the scene root overwrites its authored 270° X rotation;
            // its nested FBX then stays sideways and bounds scaling stretches it horizontally.
            var instance = new GameObject(name);
            instance.transform.SetParent(transform, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            var modelInstance = Instantiate(model, instance.transform, false);
            modelInstance.name = model.name;
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = model.transform.localRotation;
            modelInstance.transform.localScale = model.transform.localScale;

            // Strip any colliders the model may carry so it never blocks or launches the player.
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }

            // The glTF import brings a single looping clip but no Animator controller, so a bare
            // Animator.Play() leaves the rig in its bind pose. Play the clip through a graph.
            PlayCreatureAnimation(instance, assetPath);

            // Scale to the target height, then lift/lower so the lowest rendered point touches the
            // ground. Without this the model's own pivot decides its height and it can end up
            // buried in the terrain. The returned offset is the distance from the transform origin
            // down to the feet, which the chase needs so it can keep the FEET on sloping terrain.
            var feetOffset = ScaleAndGroundModel(instance, position, targetHeight);
            ConfigureModelSkinning(instance);
            var offsetHolder = instance.GetComponent<MonsterFeetOffset>();
            if (offsetHolder == null)
            {
                offsetHolder = instance.AddComponent<MonsterFeetOffset>();
            }
            offsetHolder.feetOffset = feetOffset;
            return instance;
        }
#endif

        var placeholder = SpawnMonsterPlaceholder(name, position, rotation);
        placeholder.transform.position = new Vector3(position.x, position.y, position.z);
        var placeholderOffset = placeholder.GetComponent<MonsterFeetOffset>();
        if (placeholderOffset == null)
        {
            placeholderOffset = placeholder.AddComponent<MonsterFeetOffset>();
        }
        // The capsule is 2 units tall centred on its origin, so the feet sit 1 * scale below it.
        placeholderOffset.feetOffset = placeholder.transform.lossyScale.y;
        return placeholder;
    }

    // The creature GLBs are imported by glTFast, which adds a controller-less Animator; that is
    // why the smiler used to stand in its bind pose. The bundled clip is played through a
    // playable graph and wrapped by hand, since PlayClip alone holds the last frame.
    private void PlayCreatureAnimation(GameObject instance, string assetPath)
    {
        var animator = instance.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = instance.AddComponent<Animator>();

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        animator.enabled = true;

        if (animator.runtimeAnimatorController != null)
        {
            animator.Play(0, 0, 0f);
            return;
        }

        AnimationClip clip = null;
#if UNITY_EDITOR
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip candidate && candidate.length > 0.05f &&
                !candidate.name.StartsWith("__preview__", System.StringComparison.Ordinal))
            {
                clip = candidate;
                break;
            }
        }
#endif
        if (clip == null)
        {
            Debug.LogWarning("Creature model has no animation clip: " + assetPath, this);
            return;
        }

        animator.runtimeAnimatorController = null;
        var playable = UnityEngine.Playables.AnimationPlayableUtilities.PlayClip(
            animator, clip, out var graph);
        animator.Update(0f);
        StartCoroutine(LoopCreatureClip(instance, playable, graph, clip.length));
    }

    private IEnumerator LoopCreatureClip(
        GameObject creature,
        UnityEngine.Animations.AnimationClipPlayable playable,
        UnityEngine.Playables.PlayableGraph graph,
        float length)
    {
        float time = 0f;
        while (creature != null)
        {
            time += Time.deltaTime;
            if (length > 0.01f)
                UnityEngine.Playables.PlayableExtensions.SetTime(playable, time % length);
            yield return null;
        }

        if (graph.IsValid())
            graph.Destroy();
    }

    // Distance from a spawned creature's transform origin down to its feet. Stored per instance so
    // the jungle monster and the Aunt Ela monster can never overwrite each other's value.
    private static float GetMonsterFeetOffset(GameObject creature)
    {
        if (creature == null)
        {
            return 0f;
        }

        var holder = creature.GetComponent<MonsterFeetOffset>();
        return holder != null ? holder.feetOffset : 0f;
    }

    // Fits the model to a target height and snaps its lowest point onto the terrain at the given
    // XZ. Uses the combined renderer bounds, so it works no matter how the imported model's pivot
    // is authored.
    private float ScaleAndGroundModel(GameObject instance, Vector3 groundPosition, float targetHeight)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            instance.transform.position = groundPosition;
            return 0f;
        }

        // Measure the model as spawned (scale = 1).
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float currentHeight = bounds.size.y;
        if (currentHeight > 0.0001f && targetHeight > 0.0001f)
        {
            float scale = targetHeight / currentHeight;
            instance.transform.localScale *= scale;
        }

        // Re-measure after scaling and drop the feet onto the ground.
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float groundY = ResolveExteriorGroundHeight(groundPosition);
        float feetY = bounds.min.y;
        instance.transform.position += new Vector3(0f, groundY - feetY, 0f);

        // How far the feet end up below the transform origin once the model is placed. The
        // chase re-uses this each frame so the FEET - not the pivot - stay glued to the terrain.
        return Mathf.Max(0f, instance.transform.position.y - groundY);
    }

    // The glTF importer marks skinned meshes as "update when offscreen", which keeps skinning
    // every bone on the CPU/GPU even when the creature is behind the player. Unity already keeps
    // the last skinned pose, so disabling it costs nothing visually and removes that per-frame work.
    private static void ConfigureModelSkinning(GameObject instance)
    {
        var skinnedMeshes = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinnedMeshes.Length; i++)
        {
            skinnedMeshes[i].updateWhenOffscreen = false;
        }
    }

    private GameObject SpawnMonsterPlaceholder(string name, Vector3 position, Quaternion rotation)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.position = position;
        go.transform.rotation = rotation;
        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = new Material(GetPlaceholderShader());
            material.color = new Color(0.02f, 0.02f, 0.03f, 1f);
            renderer.sharedMaterial = material;
        }

        go.transform.localScale = new Vector3(0.75f, 2.2f, 0.75f);
        return go;
    }

    private float GetPlayerSprintSpeed()
    {
        // Read the controllers' real serialized fields so the chase scales with whatever the
        // designer set. SimpleFirstPersonController sprints at moveSpeed * sprintMultiplier;
        // WorldFirstPersonController has no sprint, so its walk speed is the reference.
        const float fallbackSprintSpeed = 8.5f;
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public;

        if (firstPersonController != null)
        {
            var moveField = typeof(SimpleFirstPersonController).GetField("moveSpeed", flags);
            var multField = typeof(SimpleFirstPersonController).GetField("sprintMultiplier", flags);
            float moveSpeed = moveField != null && moveField.GetValue(firstPersonController) is float m ? m : 5f;
            float multiplier = multField != null && multField.GetValue(firstPersonController) is float s ? s : 2.5f;
            float sprint = moveSpeed * multiplier;
            if (sprint > 0.1f)
            {
                return sprint;
            }
        }

        if (worldFirstPersonController != null)
        {
            var walkField = typeof(WorldFirstPersonController).GetField("walkSpeed", flags);
            if (walkField != null && walkField.GetValue(worldFirstPersonController) is float walk && walk > 0.1f)
            {
                return walk;
            }
        }

        return fallbackSprintSpeed;
    }

    // Runs every frame while the chase is live: the monster hunts the player down the road and
    // disappears once the player is close enough to Aunt Ela's house.
    private void UpdateJungleChase()
    {
        if (!jungleChaseActive || jungleChaseMonster == null || player == null)
        {
            return;
        }

        var auntEla = ResolveAuntElaPosition();
        var playerPos = player.transform.position;

        // Vanish roughly 70 m short of Aunt Ela's house: it stops chasing and blinks out.
        if (auntEla != Vector3.zero)
        {
            var toHouse = auntEla - playerPos;
            toHouse.y = 0f;
            if (toHouse.magnitude <= 70f)
            {
                VanishJungleMonster();
                return;
            }
        }

        var flatTarget = new Vector3(playerPos.x, 0f, playerPos.z);
        var monsterPos = jungleChaseMonster.transform.position;
        var flatMonster = new Vector3(monsterPos.x, 0f, monsterPos.z);

        // Move toward the player at just under sprint speed. It only tracks on the horizontal
        // plane, and its FEET are kept on the terrain: the transform origin sits above the ground
        // by the creature's feet offset, so we ground the origin and then add that offset back.
        // (Snapping the raw origin to the ground here used to bury the running model in the terrain.)
        var nextFlat = Vector3.MoveTowards(flatMonster, flatTarget, jungleChaseSpeed * Time.deltaTime);
        nextFlat.y = ResolveExteriorGroundHeight(nextFlat) + GetMonsterFeetOffset(jungleChaseMonster);
        jungleChaseMonster.transform.position = nextFlat;

        // Face the player so it always looks like it is bearing down on them.
        var lookDir = flatTarget - new Vector3(nextFlat.x, 0f, nextFlat.z);
        if (lookDir.sqrMagnitude > 0.001f)
        {
            jungleChaseMonster.transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }

        // Caught: the monster is right on top of the player. Give it a tiny grace window so a
        // single clipped frame does not fail the run, then trigger the normal game over / retry.
        var catchDistance = Vector3.Distance(
            new Vector3(nextFlat.x, 0f, nextFlat.z),
            flatTarget);
        if (catchDistance <= jungleChaseCatchDistance)
        {
            jungleChaseCatchTimer += Time.deltaTime;
            if (jungleChaseCatchTimer >= jungleChaseCatchReset)
            {
                EndJungleChaseCaught();
            }
        }
        else
        {
            jungleChaseCatchTimer = 0f;
        }

        currentPrompt = "RUN!";
    }

    private void VanishJungleMonster()
    {
        jungleChaseActive = false;
        jungleChaseStarted = true;
        jungleChaseVanishShown = true;
        heartbeatActive = false;
        currentPrompt = string.Empty;

        if (jungleChaseMonster != null)
        {
            Destroy(jungleChaseMonster);
            jungleChaseMonster = null;
        }

        // The player whirls around to look back down the road where the creature was - the camera
        // aims BEHIND them - and reacts to it having vanished into thin air.
        var behindPoint = player != null
            ? player.transform.position - player.transform.forward * 12f + Vector3.up * 1.6f
            : (playerCamera != null ? playerCamera.transform.position + Vector3.forward * 12f : Vector3.zero);
        StartFocusedConversation(jungleChaseVanishDialogue, ScenePhase.JungleChaseVanish, behindPoint);
    }

    private void EndJungleChaseCaught()
    {
        jungleChaseActive = false;
        heartbeatActive = false;
        currentPrompt = string.Empty;
        gameOverFromJungleChase = true;

        if (jungleChaseMonster != null)
        {
            Destroy(jungleChaseMonster);
            jungleChaseMonster = null;
        }

        // Reuse the existing failure flow: GAME OVER panel plus the retry button / Enter / E.
        TriggerGameOver();
    }

    // ---------------------------------------------------------------------------------------
    // SECOND ENCOUNTER (outside Aunt Ela's house)
    // After the jungle creature vanishes, the player walks toward Aunt Ela's. When they get within
    // ~10 m the monster appears again in front of them, the player says "WTF", and it rushes them.
    // The screen then cuts to black and the player wakes up in their own bed, realising it was all
    // a bad dream.
    // ---------------------------------------------------------------------------------------

    // ---------------------------------------------------------------------------------------
    // FRED SCARE (~200 m before Aunt Ela's house)
    // A strange sound plays behind the player, they say "What's that?", turn around, and Fred
    // is standing there. The screen then cuts instantly to black.
    // ---------------------------------------------------------------------------------------
    private bool fredScareTriggered;
    [SerializeField] private float fredScareDistance = 200f;

    private void CheckFredScareOnRoad()
    {
        if (player == null)
        {
            return;
        }

        var auntEla = ResolveAuntElaPosition();
        if (auntEla == Vector3.zero)
        {
            return;
        }

        var toHouse = auntEla - player.transform.position;
        toHouse.y = 0f;
        if (toHouse.magnitude > fredScareDistance)
        {
            return;
        }

        fredScareTriggered = true;
        StartCoroutine(FredScareSequence());
    }

    private IEnumerator FredScareSequence()
    {
        LockPlayerControls();

        // 1) The strange sound comes from behind the player.
        if (audioSource != null)
        {
            var scareClip = chainsawClip != null ? chainsawClip : notificationClip;
            if (scareClip != null)
            {
                audioSource.PlayOneShot(scareClip);
            }
        }

        // 2) The player says "What's that?" while still facing forward.
        StartConversation(fredScareWhatsThatDialogue, ScenePhase.AuntElaApproachFredScare);

        // Wait until the "What's that?" line finishes.
        while (dialogueActive || isTyping)
        {
            yield return null;
        }

        // 3) Turn the player around - Fred is standing there.
        if (player != null)
        {
            var behind = -player.transform.forward;
            behind.y = 0f;
            if (behind.sqrMagnitude > 0.001f)
            {
                player.transform.rotation = Quaternion.LookRotation(behind.normalized, Vector3.up);
            }
        }

        SpawnScareFred();
        PlayImpact(fredHitSfxClip, 0.7f, 1f);

        // 3b) Swing the camera smoothly onto Fred's face and hold on him.
        if (playerCamera != null && scareFredActor != null)
        {
            playerCamera.transform.localPosition = standingCameraLocalPosition;
            playerCamera.transform.localRotation = Quaternion.identity;

            var fredFocus = scareFredActor.transform.position + Vector3.up * 1.6f;
            var startingRotation = playerCamera.transform.rotation;
            var targetRotation = Quaternion.LookRotation(fredFocus - playerCamera.transform.position, Vector3.up);
            const float focusDuration = 0.7f;
            float elapsed = 0f;
            while (elapsed < focusDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / focusDuration));
                playerCamera.transform.rotation = Quaternion.Slerp(startingRotation, targetRotation, progress);
                yield return null;
            }

            playerCamera.transform.rotation = targetRotation;
        }

        // Start the fall as soon as the camera finishes focusing. Keep Fred visible in frame
        // throughout the fall so the focus and fall stay synchronized.
        if (playerCamera != null)
        {
            var cameraStartPosition = playerCamera.transform.localPosition;
            var cameraStartRotation = playerCamera.transform.rotation;
            var cameraFallPosition = cameraStartPosition + Vector3.down * 0.78f + Vector3.forward * 0.18f;
            const float fallDuration = 0.9f;
            float fallElapsed = 0f;
            while (fallElapsed < fallDuration)
            {
                fallElapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fallElapsed / fallDuration));
                playerCamera.transform.localPosition = Vector3.Lerp(cameraStartPosition, cameraFallPosition, progress);

                if (scareFredActor != null)
                {
                    var focus = scareFredActor.transform.position + Vector3.up * 1.15f;
                    var direction = focus - playerCamera.transform.position;
                    if (direction.sqrMagnitude > 0.001f)
                    {
                        var look = Quaternion.LookRotation(direction.normalized, Vector3.up) *
                                   Quaternion.Euler(0f, 0f, 52f * progress);
                        playerCamera.transform.rotation = Quaternion.Slerp(cameraStartRotation, look, progress);
                    }
                }

                yield return null;
            }
        }

        if (playerCamera != null)
        {
            // A short, damped wobble after landing gives the player a dizzy moment while Fred
            // remains framed. The shake settles before the blackout begins.
            const float dizzyDuration = 0.8f;
            float dizzyElapsed = 0f;
            var dizzyCameraPosition = playerCamera.transform.localPosition;
            while (dizzyElapsed < dizzyDuration)
            {
                dizzyElapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(dizzyElapsed / dizzyDuration);
                float fade = 1f - Mathf.SmoothStep(0f, 1f, progress);
                playerCamera.transform.localPosition = dizzyCameraPosition + new Vector3(
                    Mathf.Sin(dizzyElapsed * 9f) * 0.035f * fade,
                    Mathf.Sin(dizzyElapsed * 13f) * 0.018f * fade,
                    0f);

                if (scareFredActor != null)
                {
                    var focus = scareFredActor.transform.position + Vector3.up * 1.1f;
                    var direction = focus - playerCamera.transform.position;
                    if (direction.sqrMagnitude > 0.001f)
                    {
                        float roll = 52f + Mathf.Sin(dizzyElapsed * 11f) * 5f * fade;
                        playerCamera.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) *
                                                          Quaternion.Euler(0f, 0f, roll);
                    }
                }

                yield return null;
            }
        }

        // 4) Sudden black screen.
        phase = ScenePhase.AuntElaFredScareBlackout;
        currentObjective = string.Empty;
        currentPrompt = string.Empty;

        if (timeTransitionPanel != null)
        {
            if (timeTransitionCanvasGroup != null)
            {
                timeTransitionCanvasGroup.alpha = 0f;
            }

            timeTransitionPanel.SetActive(true);
            var text = timeTransitionPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = string.Empty;
            }

            if (timeTransitionCanvasGroup != null)
            {
                yield return FadeCanvasGroup(timeTransitionCanvasGroup, 1f, 0.15f);
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.15f);
        }

        // 5) Let the blackout fully cover Fred before loading the repeat scene. Keep Fred present
        // until the black overlay is opaque so the player never sees him disappear early.
        yield return new WaitForSecondsRealtime(0.7f);
        if (scareFredActor != null)
        {
            Destroy(scareFredActor);
            scareFredActor = null;
        }

        // Keep the screen black while the repeat Scene 1 loads on top of the world.
        LoadScene1RepeatOverBlack();
    }

    // Loads the Scene 1 repeat scene (a full copy of Scene 1_Opening) additively on top of the
    // world while the screen is black, then unloads the world scene so only the replay runs.
private void LoadScene1RepeatOverBlack()
    {
        const string repeatScenePath = "Assets/Scenes/Scene1_Opening_Repeat.unity";
        // Clear Scene 2-only HUD/world markers before compositing the repeat scene.
        SetAuntElaMarkerVisible(false);
        SetHomeMarkerVisible(false);
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        UpdateObjectiveUi();
        var repeatScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(repeatScenePath);
        if (repeatScene.IsValid() && repeatScene.isLoaded)
        {
            StartCoroutine(ReleaseScene2BlackAfterRepeatLoad());
            return;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
        var loadOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
            repeatScenePath,
            UnityEngine.SceneManagement.LoadSceneMode.Additive);
        if (loadOperation == null)
        {
            Debug.LogError("Scene1 repeat scene could not be loaded: " + repeatScenePath, this);
            return;
        }

        loadOperation.completed += _ =>
        {
            var activeWorld = gameObject.scene;
            if (activeWorld.IsValid() && activeWorld.isLoaded)
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(activeWorld);

            // Scene 1 has its own opaque fade canvas at sorting order 400. Once the
            // repeat scene is active, remove Scene 2's lower order 260 blackout.
            StartCoroutine(ReleaseScene2BlackAfterRepeatLoad());
        };
    }

private IEnumerator ReleaseScene2BlackAfterRepeatLoad()
    {
        yield return null;
        yield return null;
        if (timeTransitionPanel != null)
            timeTransitionPanel.SetActive(false);
    }


    // Fred appears a few metres behind the player, facing them, on the actual ground.
    private void SpawnScareFred()
    {
        if (player == null)
        {
            return;
        }

        var behind = -player.transform.forward;
        behind.y = 0f;
        if (behind.sqrMagnitude < 0.001f)
        {
            behind = Vector3.back;
        }

        behind.Normalize();
        var spawnPosition = player.transform.position + behind * 4f;
        spawnPosition.y = ResolveExteriorGroundHeight(spawnPosition);

        var facingPlayer = player.transform.position - spawnPosition;
        facingPlayer.y = 0f;
        var rotation = facingPlayer.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(facingPlayer.normalized, Vector3.up)
            : Quaternion.identity;

#if UNITY_EDITOR
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/FRED/fred.glb");
        if (asset != null)
        {
            scareFredActor = Instantiate(asset, spawnPosition, rotation, transform);
            scareFredActor.name = "ScareFred";
            scareFredActor.transform.localScale = Vector3.one * 1.08f;
            GroundActor(scareFredActor, spawnPosition.y);
            return;
        }
#endif
        scareFredActor = CreateHumanPlaceholder("ScareFred", spawnPosition, rotation, transform, new Color(0.75f, 0.67f, 0.56f));
    }

    private GameObject scareFredActor;

    private void CheckAuntElaSpot()
    {
        if (auntElaSpotTriggered || player == null)
        {
            return;
        }

        var auntEla = ResolveAuntElaPosition();
        if (auntEla == Vector3.zero)
        {
            return;
        }

        var toHouse = auntEla - player.transform.position;
        toHouse.y = 0f;
        if (toHouse.magnitude > auntElaSpotDistance)
        {
            return;
        }

        auntElaSpotTriggered = true;
        SpawnAuntElaMonster();

        // The monster appears about 10 m ahead of the player, in the direction they are facing,
        // and the camera swings onto it while the player blurts out "WTF".
        var focusPoint = auntElaMonster != null
            ? auntElaMonster.transform.position + Vector3.up * 1.2f
            : (playerCamera != null ? playerCamera.transform.position + playerCamera.transform.forward * 10f : Vector3.zero);
        StartFocusedConversation(auntElaSpotDialogue, ScenePhase.AuntElaSpot, focusPoint);
    }

    private void SpawnAuntElaMonster()
    {
        if (auntElaMonster != null || player == null)
        {
            return;
        }

        // Place it a little ahead of the player, facing them.
        var ahead = player.transform.forward;
        ahead.y = 0f;
        if (ahead.sqrMagnitude < 0.001f)
        {
            ahead = Vector3.forward;
        }

        var spawnPoint = player.transform.position + ahead.normalized * auntElaSpotDistance;
        spawnPoint.y = ResolveExteriorGroundHeight(spawnPoint);

        auntElaMonster = SpawnCreatureModel(
            "AuntElaMonster",
            "Assets/Characters/MONSTER 2/the_backrooms-entity_3_smiler.glb",
            spawnPoint,
            player.transform.position);
    }

    // The monster rushes from ~10 m away straight to right in front of the player in about a
    // second, then the screen cuts to black and the dream-wake sequence begins.
    private void UpdateAuntElaRush()
    {
        if (auntElaMonster == null || playerCamera == null)
        {
            return;
        }

        auntElaRushTimer += Time.deltaTime;
        const float rushDuration = 1f;
        float progress = Mathf.Clamp01(auntElaRushTimer / rushDuration);

        var start = auntElaMonster.transform.position;
        var target = playerCamera.transform.position + playerCamera.transform.forward * 0.5f;
        auntElaMonster.transform.position = Vector3.Lerp(start, target, progress);

        // Face the player during the lunge.
        auntElaMonster.transform.rotation = Quaternion.LookRotation(
            (playerCamera.transform.position - new Vector3(auntElaMonster.transform.position.x, 0f,
                 auntElaMonster.transform.position.z)).normalized,
            Vector3.up);

        if (progress >= 1f)
        {
            auntElaRushActive = false;
            StartCoroutine(HallwayWakeSequence());
        }
    }

    private void MessageBoxRushIn()
    {
        // Called the moment the "WTF" line finishes. The monster is right in front of the player,
        // so make sure the camera is on it while the lunge plays out.
        if (auntElaMonster != null)
        {
            cameraAimPoint = auntElaMonster.transform.position + Vector3.up * 1.2f;
            hasCameraAimPoint = true;
        }
    }

    // The old bedroom "Is that a bad dream?" wake-up was replaced by HallwayWakeSequence().


    // Death during the jungle chase restarts the run from where it began: the player is put back
    // on the road with the monster behind them again. This deliberately does NOT jump back to any
    // earlier mission or into the house.
    private void RetryJungleChaseCheckpoint()
    {
        gameOverVisible = false;
        creatureRushActive = false;
        DestroyPursuingCreature();
        CleanupJungleChase();
        gameOverFromJungleChase = false;
        UnlockAndResetMovement();

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (player != null)
        {
            var respawnPosition = jungleChaseCheckpointValid
                ? jungleChaseCheckpointPosition
                : player.transform.position;
            respawnPosition.y = ResolveExteriorGroundHeight(respawnPosition);
            player.transform.position = respawnPosition;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (retryButtonObject != null)
        {
            retryButtonObject.SetActive(false);
        }

        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(false);
        }

        // Clear the run prompt and re-arm the ambush from the checkpoint.
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        jungleChaseStarted = false;
        jungleChaseVanishShown = false;
        fredScareTriggered = false;
        phase = ScenePhase.Finished;
    }

    // Removes the jungle monster (if any) and clears the chase state so a retry starts fresh.
    private void CleanupJungleChase()
    {
        jungleChaseActive = false;
        jungleChaseCatchTimer = 0f;

        if (jungleChaseMonster != null)
        {
            Destroy(jungleChaseMonster);
            jungleChaseMonster = null;
        }
    }

    // Ground height for outdoor terrain. There is no interior floor to snap to out here, so cast
    // down from high above and fall back to the player's own height if nothing is hit.
    private float ResolveExteriorGroundHeight(Vector3 worldPosition)
    {
        var probeOrigin = new Vector3(worldPosition.x, worldPosition.y + 200f, worldPosition.z);
        if (Physics.Raycast(probeOrigin, Vector3.down, out var hit, 600f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return player != null ? player.transform.position.y : worldPosition.y;
    }

    private void TriggerGameOver()
    {
        escapeTimerStarted = false;
        escapeTimerExpired = true;
        gameOverVisible = true;
        DestroyPursuingCreature();
        CleanupJungleChase();
        LockPlayerControls();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        phase = ScenePhase.GameOver;
        if (retryButtonObject != null)
        {
            retryButtonObject.SetActive(true);
        }
        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(true);
            var text = timeTransitionPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = "GAME OVER";
            }
        }
    }

    // Picks the right checkpoint for a retry: dying in the jungle chase restarts the run from the
    // road (monster behind you again), while the older escape challenge still restarts in the house.
    private void HandleRetryPressed()
    {
        if (gameOverFromJungleChase)
        {
            RetryJungleChaseCheckpoint();
        }
        else
        {
            RetryEscapeCheckpoint();
        }
    }

    private void RetryEscapeCheckpoint()
    {
        gameOverVisible = false;
        creatureRushActive = false;
        DestroyPursuingCreature();
        CleanupJungleChase();
        // Allow the jungle ambush to fire again once the player is back outside on the road.
        jungleChaseStarted = false;
        jungleChaseVanishShown = false;
        fredScareTriggered = false;
        UnlockAndResetMovement();
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Restart the same challenge: drop the player back in the house (outside the room) with the
        // bedroom door open, so they must reach the room and close the door again within 5 seconds.
        if (player != null)
        {
            Vector3 respawnPosition = diningLaptop != null
                ? diningLaptop.position
                : homeHouse != null
                    ? homeHouse.position
                    : player.transform.position;

            // Snap to the real floor so the player never respawns floating or inside furniture.
            respawnPosition.y = ResolveInteriorFloorHeight(respawnPosition);
            player.transform.position = respawnPosition;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (bedroomDoorInteraction != null)
        {
            bedroomDoorInteraction.enabled = false;
        }

        if (bedroomDoorAnimator != null && !string.IsNullOrEmpty(bedroomDoorOpenParameter))
        {
            SetBedroomDoorOpen(true);
        }

        if (houseEntranceDoorAnimator != null && !string.IsNullOrEmpty(houseEntranceDoorOpenParameter))
        {
            houseEntranceDoorAnimator.enabled = true;
            houseEntranceDoorAnimator.SetBool(houseEntranceDoorOpenParameter, true);
        }

        escapeTimerStarted = false;
        escapeTimerExpired = false;
        escapeTimerRemaining = 5f;
        finalEscapeActive = false;
        finalEscapeRemaining = 5f;
        if (retryButtonObject != null)
        {
            retryButtonObject.SetActive(false);
        }

        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(false);
        }
        noteRead = true;
        distractionThrown = true;
        phase = ScenePhase.EscapeDanger;
        currentObjective = "Get into your room and close the door!";
        currentPrompt = string.Empty;
        StartEscapeDangerTimer();
    }

    private bool IsOutsideHouse()
    {
        if (player == null || homeHouse == null)
        {
            return false;
        }

        if (houseEntranceDoor == null || IsPlayerInside(houseTrigger))
        {
            return false;
        }

        var interiorDirection = homeHouse.position - houseEntranceDoor.position;
        var playerDirection = player.transform.position - houseEntranceDoor.position;
        interiorDirection.y = 0f;
        playerDirection.y = 0f;
        if (interiorDirection.sqrMagnitude < 0.001f)
        {
            return false;
        }

        return Vector3.Dot(playerDirection, -interiorDirection.normalized) > 0.5f;
    }

    private void UnlockAndResetMovement()
    {
        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = standingCameraLocalPosition;
            playerCamera.transform.localRotation = Quaternion.identity;
        }
        ReleasePlayerControls();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator ShowImageTwoThenTimeTransition()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        if (phase != ScenePhase.LaptopSequence)
        {
            yield break;
        }

        if (laptopImageSurface != null)
        {
            laptopImageSurface.SetActive(false);
        }

        if (laptopImageLabel != null)
        {
            laptopImageLabel.gameObject.SetActive(false);
        }

        phase = ScenePhase.TimeTransition;
        ApplyEveningLighting();
        ShowTimeTransition();
        yield return new WaitForSecondsRealtime(1.5f);
        if (phase == ScenePhase.TimeTransition)
        {
            CompleteTimeTransition();
        }
    }

    private void CompleteTimeTransition()
    {
        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(false);
        }

        if (momActor != null)
        {
            momActor.SetActive(false);
        }

        if (brotherActor != null)
        {
            brotherActor.SetActive(false);
        }

        if (laptopImageSurface != null)
        {
            laptopImageSurface.SetActive(false);
        }

        if (laptopImageLabel != null)
        {
            laptopImageLabel.gameObject.SetActive(false);
        }

        if (bedroomDoorInteraction != null)
        {
            bedroomDoorInteraction.enabled = false;
        }

        SetBedroomDoorOpen(false);
        bedroomDoorOpenedAfterTimeSkip = false;
        ApplyNightLighting();
        PreparePlayerToStandFromBed();
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        phase = ScenePhase.NightLaptopDialogue;
        SetLaptopImagePlaceholder(3, bedroomLaptop);
        StartCameraFocusedConversation(
            GetBedroomLaptopFocusPoint(),
            new List<DialogueLine> { new DialogueLine("MC", "Let me ask Mom for dinner.") },
            ScenePhase.NightLaptopDialogue);
    }

    private void HideDaytimeSkySphere()
    {
        SetSceneSkySphereActive(false);
    }

    private void ShowDaytimeSkySphere()
    {
        SetSceneSkySphereActive(true);
    }

    private void SetSceneSkySphereActive(bool active)
    {
        var skyObjects = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < skyObjects.Length; i++)
        {
            var skyTransform = skyObjects[i];
            if (skyTransform == null || skyTransform.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            var objectName = skyTransform.name.ToLowerInvariant();
            if (objectName.Contains("sky") &&
                (objectName.Contains("sphere") || objectName.Contains("dome")))
            {
                skyTransform.gameObject.SetActive(active);
            }
        }
    }

    private void ApplyNightLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.028f, 0.040f, 0.072f);
        RenderSettings.ambientIntensity = 0.42f;
        HideDaytimeSkySphere();

        // Use the matching moonlit panorama from the same Fantasy Skybox pack as dusk.
        // Clone at runtime to leave the shared pack material unchanged.
        var nightSky = nightSkyboxMaterial;
#if UNITY_EDITOR
        if (nightSky == null)
        {
            nightSky = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Fantasy Skybox FREE/Panoramics/FS013/FS013_Night.mat");
        }
#endif
        if (nightSky != null)
        {
            if (sceneSkyboxMaterial != null && sceneSkyboxMaterial != nightSky)
                Destroy(sceneSkyboxMaterial);
            sceneSkyboxMaterial = new Material(nightSky)
            {
                name = "Scene2 Moonlit Night Skybox"
            };
            if (sceneSkyboxMaterial.HasProperty("_Tint"))
                sceneSkyboxMaterial.SetColor("_Tint", new Color(0.66f, 0.72f, 0.86f, 1.0f));
            if (sceneSkyboxMaterial.HasProperty("_Exposure"))
                sceneSkyboxMaterial.SetFloat("_Exposure", 1.0f);
            RenderSettings.skybox = sceneSkyboxMaterial;
        }

        // Keep the night readable with a little cool haze rather than dense blue fog.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.035f, 0.050f, 0.085f);
        RenderSettings.fogDensity = 0.012f;
        SetSceneSun(new Color(0.42f, 0.52f, 0.78f), 0.16f, Quaternion.Euler(40f, 220f, 0f));
        ApplyLiminalVolume();
        DynamicGI.UpdateEnvironment();
    }
    private void ApplyEveningLighting()
    {
        // Warm dusk ambience kept subtle so the sky carries the sunset mood.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.34f, 0.30f);
        RenderSettings.ambientEquatorColor = new Color(0.68f, 0.40f, 0.27f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.13f, 0.11f);
        RenderSettings.ambientIntensity = 0.82f;
        ShowDaytimeSkySphere();

        // Light distance haze with a muted amber tone; keep contrast and visibility.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.55f, 0.34f, 0.25f);
        RenderSettings.fogDensity = 0.0045f;

        // Use the project's authored panoramic sunset from the Fantasy Skybox pack.
        // Clone it at runtime so the shared pack asset remains untouched.
        var sunsetSky = sunsetSkyboxMaterial;
#if UNITY_EDITOR
        if (sunsetSky == null)
        {
            sunsetSky = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Fantasy Skybox FREE/Panoramics/FS013/FS013_Sunset.mat");
        }
#endif
        if (sunsetSky != null)
        {
            if (sceneSkyboxMaterial != null && sceneSkyboxMaterial != sunsetSky)
                Destroy(sceneSkyboxMaterial);
            sceneSkyboxMaterial = new Material(sunsetSky)
            {
                name = "Scene2 Warm Sunset Skybox"
            };
            if (sceneSkyboxMaterial.HasProperty("_Tint"))
                sceneSkyboxMaterial.SetColor("_Tint", new Color(1.0f, 0.80f, 0.62f, 1.0f));
            if (sceneSkyboxMaterial.HasProperty("_Exposure"))
                sceneSkyboxMaterial.SetFloat("_Exposure", 1.05f);
            RenderSettings.skybox = sceneSkyboxMaterial;
        }

        SetSceneSun(new Color(1.0f, 0.70f, 0.48f), 0.82f, Quaternion.Euler(3.5f, 32f, 0f));
        ApplyLiminalVolume();
        DynamicGI.UpdateEnvironment();
    }
    private void CaptureSceneLighting()
    {
        originalSkyboxMaterial = RenderSettings.skybox;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientSkyColor = RenderSettings.ambientSkyColor;
        originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
        originalAmbientGroundColor = RenderSettings.ambientGroundColor;
        originalAmbientLight = RenderSettings.ambientLight;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalFogEnabled = RenderSettings.fog;
        originalFogMode = RenderSettings.fogMode;
        originalFogColor = RenderSettings.fogColor;
        originalFogDensity = RenderSettings.fogDensity;

        if (originalSkyboxMaterial != null)
        {
            sceneSkyboxMaterial = new Material(originalSkyboxMaterial)
            {
                name = "Scene2 Runtime Skybox"
            };
            RenderSettings.skybox = sceneSkyboxMaterial;
        }
        else
        {
            var proceduralSkyShader = Shader.Find("Skybox/Procedural");
            if (proceduralSkyShader != null)
            {
                sceneSkyboxMaterial = new Material(proceduralSkyShader)
                {
                    name = "Scene2 Runtime Procedural Skybox"
                };
                RenderSettings.skybox = sceneSkyboxMaterial;
            }
        }

        sceneDirectionalLight = RenderSettings.sun;
        if (sceneDirectionalLight == null)
        {
            var lights = FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    sceneDirectionalLight = lights[i];
                    break;
                }
            }
        }

        if (sceneDirectionalLight != null)
        {
            originalDirectionalColor = sceneDirectionalLight.color;
            originalDirectionalIntensity = sceneDirectionalLight.intensity;
            originalDirectionalRotation = sceneDirectionalLight.transform.rotation;
        }
    }

    private void SetSceneSkybox(float exposure, Color tint)
    {
        if (sceneSkyboxMaterial == null)
        {
            return;
        }

        if (sceneSkyboxMaterial.HasProperty("_Exposure"))
        {
            sceneSkyboxMaterial.SetFloat("_Exposure", exposure);
        }

        if (sceneSkyboxMaterial.HasProperty("_TintColor"))
        {
            sceneSkyboxMaterial.SetColor("_TintColor", tint);
        }

        // Procedural skybox (Skybox/Procedural) uses _SkyTint/_GroundColor instead of a
        // plain tint, so drive those too. Evening/night sets a matching horizon colour.
        if (sceneSkyboxMaterial.HasProperty("_SkyTint"))
        {
            sceneSkyboxMaterial.SetColor("_SkyTint", tint);
        }

        // HDRI skyboxes use _Tint; set it too so their default white is not left untouched.
        if (sceneSkyboxMaterial.HasProperty("_Tint"))
        {
            sceneSkyboxMaterial.SetColor("_Tint", tint);
        }

        if (sceneSkyboxMaterial.HasProperty("_GroundColor"))
        {
            var groundTint = new Color(tint.r * 0.55f, tint.g * 0.5f, tint.b * 0.45f, 1f);
            sceneSkyboxMaterial.SetColor("_GroundColor", groundTint);
        }
    }

    private void SetSceneSun(Color color, float intensity, Quaternion rotation)
    {
        if (sceneDirectionalLight == null)
        {
            return;
        }

        sceneDirectionalLight.color = color;
        sceneDirectionalLight.intensity = intensity;
        sceneDirectionalLight.transform.rotation = rotation;
    }

    // Builds (once) a global post-processing volume that gives the scene its liminal feel:
    // slightly drained colour, soft haze/bloom, faint grain and a gentle vignette that
    // frames the emptiness. Intentionally subtle so evening stays evening and night stays
    // night - this only changes mood, never the time of day.
    private void ApplyLiminalVolume()
    {
        if (liminalVolumeObject == null)
        {
            liminalVolumeObject = new GameObject("LiminalSpaceVolume");
            liminalVolumeObject.transform.SetParent(transform, false);
            var volume = liminalVolumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Scene2 Liminal Profile (Runtime)";
            volume.sharedProfile = profile;

            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.value = 0.03f;
            colorAdjustments.contrast.value = 6f;
            colorAdjustments.saturation.value = -6f;
            colorAdjustments.colorFilter.value = new Color(1f, 0.98f, 0.95f, 1f);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.1f;
            bloom.intensity.value = 0.22f;
            bloom.scatter.value = 0.68f;

            var vignette = profile.Add<Vignette>(true);
            vignette.color.value = Color.black;
            vignette.intensity.value = 0.22f;
            vignette.smoothness.value = 0.5f;

            var filmGrain = profile.Add<FilmGrain>(true);
            filmGrain.active = true;
            filmGrain.intensity.value = 0.10f;
            filmGrain.response.value = 0.75f;

            var chromaticAberration = profile.Add<ChromaticAberration>(true);
            chromaticAberration.active = true;
            chromaticAberration.intensity.value = 0.05f;

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;
        }

        // Make sure the player camera actually renders the volume stack. Scenes shipped
        // with the camera's post-processing left off would otherwise ignore the profile.
        if (playerCamera != null)
        {
            playerCamera.allowHDR = true;
            var cameraData = playerCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = playerCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            cameraData.renderPostProcessing = true;
        }
    }

    private void OnDestroy()
    {
        CleanupJungleChase();

        if (sceneDirectionalLight != null)
        {
            sceneDirectionalLight.color = originalDirectionalColor;
            sceneDirectionalLight.intensity = originalDirectionalIntensity;
            sceneDirectionalLight.transform.rotation = originalDirectionalRotation;
        }

        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientSkyColor = originalAmbientSkyColor;
        RenderSettings.ambientEquatorColor = originalAmbientEquatorColor;
        RenderSettings.ambientGroundColor = originalAmbientGroundColor;
        RenderSettings.ambientLight = originalAmbientLight;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;

        if (liminalVolumeObject != null)
        {
            Destroy(liminalVolumeObject);
            liminalVolumeObject = null;
        }

        if (hallwayWallPattern != null) Destroy(hallwayWallPattern);
        if (hallwayFloorPattern != null) Destroy(hallwayFloorPattern);
        if (wallMaterial != null) Destroy(wallMaterial);
        if (floorMaterial != null) Destroy(floorMaterial);
        if (ceilingMaterial != null) Destroy(ceilingMaterial);
        if (lightMaterial != null) Destroy(lightMaterial);

        if (sceneSkyboxMaterial != null)
        {
            if (RenderSettings.skybox == sceneSkyboxMaterial)
            {
                RenderSettings.skybox = originalSkyboxMaterial;
            }

            Destroy(sceneSkyboxMaterial);
        }

        var lights = FindObjectsByType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional)
            {
                lights[i].intensity = Mathf.Max(lights[i].intensity, 1f);
            }
        }
    }

    private void PreparePlayerToStandFromBed()
    {
        if (player == null || bedroomBed == null || playerCamera == null)
        {
            Debug.LogError("Scene2EveningManager could not stand the player because the player, bed, or camera is missing.", this);
            return;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        playerCamera.transform.localPosition = standingCameraLocalPosition;
        playerCamera.transform.localRotation = Quaternion.identity;

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    private void StartCameraFocusedConversation(
        Vector3 focusPoint,
        List<DialogueLine> lines,
        ScenePhase conversationPhase,
        Transform closeupLaptop = null)
    {
        if (playerCamera == null)
        {
            Debug.LogError("Scene2EveningManager cannot focus the camera because the player camera is missing.", this);
            StartFocusedConversation(lines, conversationPhase, focusPoint);
            return;
        }

        if (closeupLaptop != null && !hasCameraPoseBeforeLaptopCloseup)
        {
            cameraPositionBeforeLaptopCloseup = playerCamera.transform.position;
            cameraRotationBeforeLaptopCloseup = playerCamera.transform.rotation;
            hasCameraPoseBeforeLaptopCloseup = true;
        }

        LockPlayerControls();
        cameraFocusPoint = focusPoint;
        cameraFocusActive = true;
        var cameraTargetPosition = closeupLaptop != null
            ? focusPoint + closeupLaptop.forward * 0.65f + Vector3.up * 0.1f
            : playerCamera.transform.position;
        StartCoroutine(FocusThenStartConversation(
            focusPoint,
            lines,
            conversationPhase,
            cameraTargetPosition,
            closeupLaptop != null));
    }

    private IEnumerator FocusThenStartConversation(
        Vector3 focusPoint,
        List<DialogueLine> lines,
        ScenePhase conversationPhase,
        Vector3 cameraTargetPosition,
        bool moveCameraToLaptop)
    {
        float elapsed = 0f;
        var cameraStartPosition = playerCamera.transform.position;
        const float cameraMoveDuration = 0.75f;
        while (elapsed < 1.25f)
        {
            elapsed += Time.unscaledDeltaTime;
            if (moveCameraToLaptop)
            {
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / cameraMoveDuration));
                playerCamera.transform.position = Vector3.Lerp(cameraStartPosition, cameraTargetPosition, progress);
            }

            bool cameraIsFocused =
                Vector3.Angle(playerCamera.transform.forward, focusPoint - playerCamera.transform.position) <= 1.5f;
            if (cameraIsFocused && (!moveCameraToLaptop || elapsed >= cameraMoveDuration))
            {
                break;
            }

            yield return null;
        }

        if (moveCameraToLaptop)
        {
            playerCamera.transform.position = cameraTargetPosition;
            playerCamera.transform.rotation = Quaternion.LookRotation(
                focusPoint - cameraTargetPosition,
                Vector3.up);
            diningLaptopCameraPosition = playerCamera.transform.position;
            diningLaptopCameraRotation = playerCamera.transform.rotation;
            redRoomLaptopViewSaved = true;
            diningLaptopViewLocked = true;
        }

        cameraFocusActive = false;
        StartFocusedConversation(lines, conversationPhase, focusPoint);
    }

    private Vector3 GetBedroomLaptopFocusPoint()
    {
        var screen = FindLaptopScreenRenderer(bedroomLaptop);
        return screen != null ? screen.bounds.center : bedroomLaptop.position + Vector3.up * 0.2f;
    }

    private Vector3 GetDiningLaptopFocusPoint()
    {
        var screen = FindLaptopScreenRenderer(diningLaptop);
        return screen != null ? screen.bounds.center : diningLaptop.position + Vector3.up * 0.2f;
    }

    private Vector3 GetKitchenFocusPoint()
    {
        return homeHouse != null
            ? homeHouse.TransformPoint(new Vector3(-4.46f, 1.5f, 0.7f))
            : playerCamera != null
                ? playerCamera.transform.position + playerCamera.transform.forward * 10f
                : Vector3.zero;
    }

    private bool HasFullyExitedBedroom()
    {
        if (player == null || bedroomBed == null || bedroomDoor == null || bedroomTrigger == null ||
            IsPlayerInside(bedroomTrigger))
        {
            return false;
        }

        // Require the player to clear the door plane, not merely leave the room trigger bounds.
        var outwardDirection = bedroomDoorwayCenter - bedroomBed.position;
        outwardDirection.y = 0f;
        if (outwardDirection.sqrMagnitude < 0.001f)
        {
            outwardDirection = bedroomDoor.position - bedroomBed.position;
            outwardDirection.y = 0f;
        }

        if (outwardDirection.sqrMagnitude < 0.001f)
        {
            return false;
        }

        outwardDirection.Normalize();
        float clearedDistance = Vector3.Dot(player.transform.position - bedroomDoorwayCenter, outwardDirection);
        float requiredClearance = characterController != null
            ? Mathf.Max(1f, characterController.radius + 0.55f)
            : 1f;
        return clearedDistance >= requiredClearance;
    }

    private bool IsKitchenViewAreaReached()
    {
        if (player == null || playerCamera == null || bedroomTrigger == null)
        {
            return false;
        }

        if (!HasFullyExitedBedroom())
        {
            return false;
        }

        if (!bedroomDoorOpenedAfterTimeSkip || !IsBedroomDoorOpen())
        {
            return false;
        }

        Vector3 kitchenPoint = GetKitchenFocusPoint();
        if (Vector3.Distance(player.transform.position, kitchenPoint) > 7.5f)
        {
            return false;
        }

        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(kitchenPoint);
        return viewportPoint.z > 0f &&
               viewportPoint.x >= 0.15f && viewportPoint.x <= 0.85f &&
               viewportPoint.y >= 0.1f && viewportPoint.y <= 0.9f;
    }

    private void BeginRedRoomSequence()
    {
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        redRoomLaptopViewSaved = false;
        diningLaptopViewLocked = false;
        phase = ScenePhase.RedRoomIntroDialogue;
        SetLaptopImagePlaceholder(4, diningLaptop);
        StartCameraFocusedConversation(
            GetDiningLaptopFocusPoint(),
            new List<DialogueLine> { new DialogueLine("MC", "What is this? What kind of live stream is that?") },
            ScenePhase.RedRoomIntroDialogue,
            diningLaptop);
    }

    private IEnumerator PlayRedRoomImages()
    {
        while (phase == ScenePhase.RedRoomImages && laptopImageIndex < 7)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (phase != ScenePhase.RedRoomImages)
            {
                yield break;
            }

            laptopImageIndex++;
            SetLaptopImagePlaceholder(laptopImageIndex, diningLaptop);
            LockCameraOnDiningLaptop();
            if (laptopImageIndex == 6)
            {
                PlayChainsawSound();
            }
            else if (laptopImageIndex == 7)
            {
                PlayImpact(killSfxClip, 0.82f, 0.98f);
            }
        }

        yield return new WaitForSecondsRealtime(1f);
        if (phase != ScenePhase.RedRoomImages || laptopImageIndex != 7)
        {
            yield break;
        }

        if (laptopImageSurface != null)
        {
            laptopImageSurface.SetActive(false);
        }

        if (laptopImageLabel != null)
        {
            laptopImageLabel.gameObject.SetActive(false);
        }

        currentPrompt = string.Empty;
        phase = ScenePhase.ClosingDiningLaptop;
        LockCameraOnDiningLaptop();
        if (diningLaptopScreenLid != null)
        {
            StartCoroutine(CloseDiningLaptopThenContinue());
        }
        else
        {
            Debug.LogError("Scene2EveningManager could not close the dining laptop because its screen transform is missing.", this);
            ContinueWithLittleBrother();
        }
    }

    private IEnumerator CloseDiningLaptopThenContinue()
    {
        LockCameraOnDiningLaptop();
        var closedRotation = diningLaptopScreenOpenRotation * Quaternion.Euler(90f, 0f, 0f);
        var startRotation = diningLaptopScreenLid.localRotation;
        const float closeDuration = 0.9f;
        float elapsed = 0f;
        while (elapsed < closeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / closeDuration));
            diningLaptopScreenLid.localRotation = Quaternion.Slerp(startRotation, closedRotation, progress);
            yield return null;
        }

        diningLaptopScreenLid.localRotation = closedRotation;
        LockCameraOnDiningLaptop();
        ContinueWithLittleBrother();
    }

    private void ContinueWithLittleBrother()
    {
        LockCameraOnDiningLaptop();
        diningLaptopViewLocked = false;
        redRoomLaptopViewSaved = false;

        if (brotherActor != null)
        {
            var bathroomDoor = homeHouse != null
                ? homeHouse.Find("PFB_Building_Base/PFB_DoorSingle_Open (1)")
                : null;
            if (bathroomDoor != null)
            {
                var brotherPosition = bathroomDoor.position + bathroomDoor.forward * 1.35f;
                brotherPosition.y = bathroomDoor.position.y;
                brotherActor.transform.position = brotherPosition;

                var brotherFacing = playerCamera != null
                    ? playerCamera.transform.position - brotherPosition
                    : player != null ? player.transform.position - brotherPosition : Vector3.zero;
                brotherFacing.y = 0f;
                if (brotherFacing.sqrMagnitude > 0.001f)
                {
                    brotherActor.transform.rotation = Quaternion.LookRotation(-brotherFacing.normalized, Vector3.up);
                }
            }

            brotherActor.SetActive(true);
            GroundActor(brotherActor, bathroomDoor != null ? bathroomDoor.position.y : brotherActor.transform.position.y);
        }

        phase = ScenePhase.LittleBrotherRedRoomDialogue;
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        StartCameraFocusedConversation(
            diningLaptop != null
                ? GetDiningLaptopFocusPoint()
                : playerCamera.transform.position + playerCamera.transform.forward * 5f,
            new List<DialogueLine>
            {
                new DialogueLine("MC", "WTF is that?"),
                new DialogueLine("Little Brother", "Hey what are you doing with my laptop?"),
                new DialogueLine("MC", "What are you watching?"),
                new DialogueLine("Little Brother", "It's a red room on dark web."),
                new DialogueLine("MC", "Wait dark web, are you mad or what?"),
                new DialogueLine("Little Brother", "My classmates used to watch it, so I decided to watch too."),
                new DialogueLine("MC", "It's not a cartoon. Who gave you the link?"),
                new DialogueLine("Little Brother", "I got it from your friend Fred."),
                new DialogueLine("MC", "I'll tell Mom."),
                new DialogueLine("Little Brother", "Please don't tell Mom. I promise I'll never open it again. I barely watched it because you closed the laptop."),
                new DialogueLine("MC", "Okay. But don't ever open it again."),
                new DialogueLine("MC", "Where is Mom?"),
                new DialogueLine("Little Brother", "Maybe she went to buy groceries.")
            },
            ScenePhase.LittleBrotherRedRoomDialogue);
    }

    private void LockCameraOnDiningLaptop()
    {
        if (playerCamera == null || !redRoomLaptopViewSaved)
        {
            return;
        }

        playerCamera.transform.SetPositionAndRotation(
            diningLaptopCameraPosition,
            diningLaptopCameraRotation);
        cameraFocusActive = false;
        diningLaptopViewLocked = true;
    }

    private void PlayChainsawSound()
    {
        if (chainsawClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(chainsawClip);
        }
        else if (!chainsawWarningLogged)
        {
            Debug.LogWarning("Scene2EveningManager reached the chainsaw moment, but no chainsaw SFX is available. Assign the chainsaw clip on the Scene2EveningManager component.", this);
            chainsawWarningLogged = true;
        }
    }

    private void RestoreStandingCameraPose()
    {
        if (playerCamera == null)
        {
            return;
        }

        playerCamera.transform.localPosition = standingCameraLocalPosition;
        playerCamera.transform.localRotation = Quaternion.identity;
    }

    private void UpdatePhonePose()
    {
        if (phoneObject == null || !phoneObject.activeSelf || playerCamera == null)
        {
            return;
        }

        phoneObject.transform.SetPositionAndRotation(
            playerCamera.transform.TransformPoint(new Vector3(0.52f, -0.28f, 1.0f)),
            playerCamera.transform.rotation);
    }

    private AudioClip GetUnknownManVoiceClip(string speaker, string text)
    {
        if (speaker != "Unknown Man")
        {
            return null;
        }

        switch (text)
        {
            case "Hey, how did you get in there?":
                return unknownManGreetingVoiceClip;
            case "Yeah, you can't see me. I am your developer.":
                return unknownManDeveloperVoiceClip;
            case "Nothing.":
                return unknownManNothingVoiceClip;
            case "Oh, you don't have to know this. It's a setup of a future project.":
                return unknownManSetupVoiceClip;
            case "Ok, bye.":
                return unknownManByeVoiceClip;
            default:
                return null;
        }
    }

    private void PlayDialogueVoice()
    {
        if (unknownManVoiceSource == null || pendingVoiceClip == null)
        {
            return;
        }

        unknownManVoiceSource.Stop();
        unknownManVoiceSource.clip = pendingVoiceClip;
        unknownManVoiceSource.Play();
    }

    private void StopDialogueVoice()
    {
        advanceQueuedForVoice = false;
        pendingVoiceClip = null;
        if (unknownManVoiceSource != null)
        {
            unknownManVoiceSource.Stop();
        }
    }

    private bool IsDialogueVoicePlaying()
    {
        return pendingVoiceClip != null && unknownManVoiceSource != null && unknownManVoiceSource.isPlaying;
    }

    private AudioClip LoadAudioClip(string assetPath)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#else
        return null;
#endif
    }

    public void DebugPrepareForLiminalEndingTest()
    {
        StopAllCoroutines();
        typingCoroutine = null;
        HideDialogueUi();
        StopDialogueVoice();
        dialogueActive = false;
        isTyping = false;
        waitForSpace = false;
        pendingSentence = string.Empty;
        currentPrompt = string.Empty;
        activeDialogue = null;
        dialogueIndex = 0;
        currentObjective = string.Empty;
        if (phoneMessages != null) phoneMessages.ClosePhone();
        if (timeTransitionPanel != null) timeTransitionPanel.SetActive(false);
        if (retryButtonObject != null) retryButtonObject.SetActive(false);
        UpdateObjectiveUi();
    }
    public void SetLiminalEndingObjective(string objective)
    {
        currentObjective = objective ?? string.Empty;
        UpdateObjectiveUi();
    }

    private void UpdateObjectiveUi()
    {
        if (objectiveBlock == null)
        {
            return;
        }

        bool visible = !string.IsNullOrEmpty(currentObjective);
        objectiveBlock.SetActive(visible);
        if (objectiveCanvasGroup != null)
        {
            objectiveCanvasGroup.alpha = 1f;
        }

        if (objectiveText != null && !string.Equals(lastObjectiveLayoutText, currentObjective, System.StringComparison.Ordinal))
        {
            objectiveText.text = currentObjective;
            var objectiveRect = objectiveBlock.GetComponent<RectTransform>();
            if (objectiveRect != null)
            {
                objectiveText.ForceMeshUpdate();
                float availableTextWidth = Mathf.Max(120f, objectiveRect.rect.width - 32f);
                float preferredTextHeight = objectiveText.GetPreferredValues(currentObjective, availableTextWidth, 240f).y;
                float panelHeight = Mathf.Clamp(preferredTextHeight + 20f, 64f, 108f);
                objectiveRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
            }

            lastObjectiveLayoutText = currentObjective;
        }
        if (phonePromptText != null)
        {
            phonePromptText.text = currentPrompt;
            phonePromptText.gameObject.SetActive(!string.IsNullOrEmpty(currentPrompt));
        }
    }

public bool DebugStartObjective(string phaseName)
    {
        if (player == null || string.IsNullOrEmpty(phaseName) ||
            !System.Enum.TryParse(phaseName, out ScenePhase targetPhase) ||
            !System.Enum.IsDefined(typeof(ScenePhase), targetPhase))
        {
            Debug.LogError("Scene 2 debug menu could not resolve objective: " + phaseName, this);
            return false;
        }

        StopAllCoroutines();
        typingCoroutine = null;
        HideDialogueUi();
        StopDialogueVoice();
        if (timeTransitionPanel != null) timeTransitionPanel.SetActive(false);
        if (retryButtonObject != null) retryButtonObject.SetActive(false);
        if (phoneMessages != null) phoneMessages.ClosePhone();

        dialogueActive = false;
        isTyping = false;
        waitForSpace = false;
        pendingSentence = string.Empty;
        currentPrompt = string.Empty;
        activeDialogue = null;
        dialogueIndex = 0;
        cameraFocusActive = false;
        hasCameraAimOverride = false;
        hasCameraAimPoint = false;
        diningLaptopViewLocked = false;
        redRoomLaptopViewSaved = false;
        homeObjectiveActive = false;
        houseReached = true;
        bedroomDoorOpenedAfterTimeSkip = false;
        houseDoorClosedForStory = false;
        noteRead = false;
        noteApproached = false;
        bathroomShadowTriggered = false;
        brotherHiddenDuringSearch = false;
        outsideSearchTriggered = false;
        warningPhoneOpened = false;
        distractionThrown = false;
        escapeTimerStarted = false;
        escapeTimerExpired = false;
        escapeTimerRemaining = 0f;
        gameOverVisible = false;
        creatureRushActive = false;
        jungleChaseActive = false;
        jungleChaseStarted = false;
        jungleChaseVanishShown = false;
        jungleChaseCatchTimer = 0f;
        auntElaSpotArmed = false;
        auntElaSpotTriggered = false;
        auntElaRushActive = false;
        auntElaRushTimer = 0f;
        finalEscapeActive = false;
        bedroomReached = false;
        fredScareTriggered = false;
        hallwayExplorationActive = false;
        hallwayPcApproachArmed = false;
        hallwayEndlessShown = false;
        currentObjective = string.Empty;

        ReleasePlayerControls();
        UnlockAndResetMovement();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;

        if (pursuingCreatureObject != null) Destroy(pursuingCreatureObject);
        if (jungleChaseMonster != null) Destroy(jungleChaseMonster);
        if (auntElaMonster != null) Destroy(auntElaMonster);
        if (distractionProp != null) Destroy(distractionProp);
        if (hallwayOfficeSetup != null && targetPhase != ScenePhase.HallwayGoToPc)
        {
            Destroy(hallwayOfficeSetup);
            hallwayOfficeSetup = null;
        }

        if (momActor != null) momActor.SetActive(true);
        if (brotherActor != null) brotherActor.SetActive(true);
        if (bedroomDoorInteraction != null) bedroomDoorInteraction.enabled = true;
        if (houseEntranceDoorInteraction != null) houseEntranceDoorInteraction.enabled = true;
        if (bedroomDoorAnimator != null && !string.IsNullOrEmpty(bedroomDoorOpenParameter))
            SetBedroomDoorOpen(false);
        if (houseEntranceDoorAnimator != null && !string.IsNullOrEmpty(houseEntranceDoorOpenParameter))
            SetHouseEntranceDoorOpen(false);
        SetAuntElaMarkerVisible(false);
        SetHomeMarkerVisible(false);

        phase = targetPhase;
        bool nightObjective = targetPhase == ScenePhase.GoToMomAfterTimeSkip ||
            targetPhase == ScenePhase.LateCheckLaptop || targetPhase == ScenePhase.FredArgumentPhone ||
            targetPhase == ScenePhase.WindowDistraction || targetPhase == ScenePhase.BrotherMissingSearch ||
            targetPhase == ScenePhase.OutsideSearch || targetPhase == ScenePhase.FredWarningPhone ||
            targetPhase == ScenePhase.ReadMomNote || targetPhase == ScenePhase.EscapeDanger ||
            targetPhase == ScenePhase.FinalEscape || targetPhase == ScenePhase.Finished ||
            targetPhase == ScenePhase.AuntElaApproach || targetPhase == ScenePhase.HallwayExplore ||
            targetPhase == ScenePhase.HallwayGoToPc || targetPhase == ScenePhase.AuntElaSecondApproach;
        if (nightObjective) ApplyNightLighting(); else ApplyEveningLighting();

        switch (targetPhase)
        {
            case ScenePhase.GoHome:
                homeObjectiveActive = true;
                houseReached = false;
                SetHomeMarkerVisible(true);
                currentObjective = "Go home";
                break;
            case ScenePhase.EnterHouse:
                houseReached = false;
                currentObjective = "Enter the house";
                break;
            case ScenePhase.GoToMom: currentObjective = "Go to Mom"; break;
            case ScenePhase.CheckLaptop: currentObjective = "Check the laptop"; break;
            case ScenePhase.BedroomTransition: currentObjective = "Go to your bedroom"; break;
            case ScenePhase.PhonePrompt:
                currentObjective = "Check your messages";
                currentPrompt = "UP ARROW  •  OPEN PHONE";
                break;
            case ScenePhase.GoToMomAfterTimeSkip:
                bedroomDoorOpenedAfterTimeSkip = false;
                SetBedroomDoorOpen(false);
                currentObjective = "Go find Mom";
                break;
            case ScenePhase.LateCheckLaptop: currentObjective = "Check the laptop"; break;
            case ScenePhase.FredArgumentPhone:
                StartFredArgumentPhone();
                break;
            case ScenePhase.WindowDistraction:
                PrepareDistractionProp();
                if (distractionProp != null)
                {
                    Vector3 spawn = distractionProp.transform.position - player.transform.forward * 1.4f;
                    spawn.y = player.transform.position.y;
                    SetDebugPlayerPosition(spawn);
                }
                distractionClockPickedUp = false;
                currentObjective = "Pick up alarm clock and throw out of window";
                currentPrompt = "E  •  PICK UP ALARM CLOCK";
                break;
            case ScenePhase.BrotherMissingSearch:
                currentObjective = "Find Little Brother in the house";
                if (diningLaptop != null)
                {
                    Vector3 spawn = diningLaptop.position + Vector3.back * 2f;
                    spawn.y = player.transform.position.y;
                    SetDebugPlayerPosition(spawn);
                }
                break;
            case ScenePhase.OutsideSearch:
                currentObjective = "Search outside for Little Brother";
                if (homeHouse != null)
                {
                    var sink = homeHouse.Find("PFB_BathroomSink");
                    var curtain = homeHouse.Find("PFB_Building_Base/PFB_CurtainSmall");
                    Vector3 outsideDirection = curtain != null && sink != null
                        ? curtain.position - sink.position : -homeHouse.forward;
                    outsideDirection.y = 0f;
                    outsideDirection = outsideDirection.sqrMagnitude > 0.001f
                        ? outsideDirection.normalized : -homeHouse.forward;
                    Vector3 windowTarget = GetBathroomWindowFocusPoint() + outsideDirection * 1.5f;
                    windowTarget.y = player.transform.position.y;
                    SetDebugPlayerPosition(windowTarget);
                }
                break;
            case ScenePhase.FredWarningPhone:
                StartFredWarningPhone();
                break;
            case ScenePhase.ReadMomNote:
                StartMomNoteSequence();
                if (noteObject != null)
                {
                    Vector3 spawn = noteObject.transform.position + Vector3.up * 1.65f - player.transform.forward * 0.8f;
                    SetDebugPlayerPosition(spawn);
                }
                currentObjective = "Read Mom's note";
                break;
            case ScenePhase.EscapeDanger:
                noteRead = true;
                distractionThrown = true;
                StartEscapeDangerTimer();
                currentObjective = "Get into your room and close the door!";
                break;
            case ScenePhase.FinalEscape:
                StartFinalEscapeDash();
                break;
            case ScenePhase.Finished:
                jungleChaseStarted = true;
                StartJungleChase();
                break;
            case ScenePhase.AuntElaApproach:
            case ScenePhase.AuntElaSecondApproach:
                auntElaSpotArmed = true;
                auntElaSpotTriggered = false;
                currentObjective = "Go to Aunt Ela's house";
                SetAuntElaMarkerVisible(true);
                break;
            case ScenePhase.HallwayExplore:
                hallwayExplorationActive = true;
                hallwaySpawnLocalZ = hallwayRoot != null ? hallwayRoot.InverseTransformPoint(player.transform.position).z : 0f;
                currentObjective = "Find a way to get out of here";
                break;
            case ScenePhase.HallwayGoToPc:
                SpawnHallwayOfficeSetup();
                hallwayPcApproachArmed = true;
                currentObjective = "Go to the PC setup";
                break;
            default:
                currentObjective = phaseName;
                break;
        }

        UpdateObjectiveUi();
        return true;
    }

private void SetDebugPlayerPosition(Vector3 position)
    {
        if (player == null) return;
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.position = position;
        if (controller != null) controller.enabled = true;
    }


    private void UpdateHomeCharacters()
    {
        if (houseObjectiveMarker != null && houseObjectiveMarker.activeSelf != homeObjectiveActive)
        {
            houseObjectiveMarker.SetActive(homeObjectiveActive);
        }

        float momBreathing = Mathf.Sin(Time.unscaledTime * 2f) * 0.015f;
        if (momModelRoot != null)
        {
            momModelRoot.localPosition = Vector3.up * momBreathing * 0.25f;
        }
        else if (momPlaceholderBody != null)
        {
            momPlaceholderBody.localScale = new Vector3(0.45f, 0.9f + momBreathing, 0.45f);
        }

        float brotherBreathing = Mathf.Sin(Time.unscaledTime * 2f + 0.7f) * 0.015f;
        if (brotherModelRoot != null)
        {
            brotherModelRoot.localPosition = Vector3.up * brotherBreathing * 0.25f;
        }
        else if (brotherPlaceholderBody != null)
        {
            brotherPlaceholderBody.localScale = new Vector3(0.45f, 0.9f + brotherBreathing, 0.45f);
        }
    }

    private void UpdateHouseMarkerScale()
    {
        UpdateObjectiveMarkerScale(houseObjectiveMarker);
        UpdateScreenMarker(auntElaMarker);
        UpdateScreenMarker(homeMarker);
    }

    private void UpdateObjectiveMarkerScale(GameObject marker)
    {
        if (marker == null || !marker.activeSelf || playerCamera == null)
        {
            return;
        }

        float distance = Vector3.Distance(playerCamera.transform.position, marker.transform.position);
        float scale = Mathf.Clamp(distance * 0.02f, 1f, 6f);
        marker.transform.localScale = Vector3.one * scale;
    }

    // Fade to black, stand the player up in the bedroom doorway / hallway, then fade back in.
    // This is the non-bed variant of the wake-up: instead of sitting up in bed the player simply
    // finds themselves already standing in the hallway when the screen clears.
    private IEnumerator HallwayWakeSequence()
    {
        LockPlayerControls();

        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(true);
            var text = timeTransitionPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = string.Empty;
            }

            if (timeTransitionCanvasGroup != null)
            {
                timeTransitionCanvasGroup.alpha = 0f;
                yield return FadeCanvasGroup(timeTransitionCanvasGroup, 1f, 0.4f);
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.4f);
        }

        // The rush is over and the screen is black. This monster used to stay alive for the whole
        // rest of the game, animating and skinning offscreen; remove it before the hallway starts.
        if (auntElaMonster != null)
        {
            Destroy(auntElaMonster);
            auntElaMonster = null;
        }

        // Open the bedroom door so the hallway is reachable, and clear any leftover objectives.
        if (bedroomDoorAnimator != null && !string.IsNullOrEmpty(bedroomDoorOpenParameter))
        {
            SetBedroomDoorOpen(true);
        }

        homeObjectiveActive = false;
        if (houseObjectiveMarker != null)
        {
            houseObjectiveMarker.SetActive(false);
        }

        SetHomeMarkerVisible(false);
        SetAuntElaMarkerVisible(false);
        if (auntElaMarkerObject != null)
        {
            auntElaMarkerObject.SetActive(false);
        }

        // Stand the player inside the backrooms hallway instead of the bedroom doorway:
        // the monster caught them, the screen cut to black, and now they "wake up" standing
        // in the endless yellow hallway far away from the map.
        if (player != null)
        {
            var wakePosition = hallwayRoot != null
                ? hallwayRoot.TransformPoint(new Vector3(0f, 0.05f, 1.5f))
                : new Vector3(5000f, 0.05f, 5001.5f);
            wakePosition.y += 0.95f; // feet on the hallway floor, controller pivot above it

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            player.transform.position = wakePosition;

            // Face down the hallway so the first thing seen is the endless yellow corridor.
            var hallwayForward = hallwayRoot != null ? hallwayRoot.forward : Vector3.forward;
            hallwayForward.y = 0f;
            if (hallwayForward.sqrMagnitude > 0.001f)
            {
                player.transform.rotation = Quaternion.LookRotation(hallwayForward.normalized, Vector3.up);
            }

            if (characterController != null)
            {
                characterController.enabled = true;
            }
        }

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = standingCameraLocalPosition;
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        yield return new WaitForSecondsRealtime(0.4f);

        // Fade back in already standing in the hallway.
        if (timeTransitionCanvasGroup != null)
        {
            yield return FadeCanvasGroup(timeTransitionCanvasGroup, 0f, 0.8f);
        }

        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(false);
        }

        ReleasePlayerControls();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // The player looks around and says where they are.
        StartConversation(hallwayWakeDialogue, ScenePhase.HallwayWake);
    }

    // Endless yellow "backrooms" hallway, built far away from the map on a spot that is normally
    // invisible and unreachable. Walls/floor/ceiling are simple boxes with a mono-yellow look,
    // buzzing fluorescent panels, and it is fully enclosed so the skybox is never visible.
    private Transform hallwayRoot;

    // Infinite hallway bookkeeping: the walk-in checks and the office setup approach.
    private bool hallwayExplorationActive;
    private bool hallwayPcApproachArmed;
    private bool hallwayEndlessShown;
    private float hallwaySpawnLocalZ;
    private GameObject hallwayOfficeSetup;
    private const float HallwayEndlessDistance = 70f;
    private const float HallwayPcApproachDistance = 2.5f;

    // Fired once the player has wandered ~70 m from the hallway spawn in either direction.
    private void CheckHallwayEndlessLine()
    {
        if (hallwayRoot == null || hallwayEndlessShown)
        {
            return;
        }

        float localZ = hallwayRoot.InverseTransformPoint(player.transform.position).z;
        if (Mathf.Abs(localZ - hallwaySpawnLocalZ) < HallwayEndlessDistance)
        {
            return;
        }

        hallwayEndlessShown = true;
        hallwayExplorationActive = false;
        currentObjective = string.Empty;
        StartFocusedConversation(
            hallwayEndlessDialogue,
            ScenePhase.HallwayEndless,
            player.transform.position + player.transform.forward * 12f);
    }

    // Fired once the player closes in on the office setup in the hallway.
    private void CheckHallwayPcApproach()
    {
        if (hallwayOfficeSetup == null)
        {
            return;
        }

        var pcPosition = hallwayOfficeSetup.transform.position;
        var toPc = pcPosition - player.transform.position;
        toPc.y = 0f;
        if (toPc.magnitude > HallwayPcApproachDistance)
        {
            return;
        }

        hallwayPcApproachArmed = false;
        currentObjective = string.Empty;
        currentPrompt = string.Empty;
        StartCameraFocusedConversation(
            pcPosition + Vector3.up * 1.1f,
            hallwayPcDialogue,
            ScenePhase.HallwayPcDialogue);
    }

    // The player's "Wait!" is cut off by an abrupt black screen, then they wake up back in
    // their own bedroom - the whole hallway was a dream inside the dream.
    private IEnumerator HallwayBlackoutToBedroom()
    {
        LockPlayerControls();

        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(true);
            var text = timeTransitionPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = string.Empty;
            }

            if (timeTransitionCanvasGroup != null)
            {
                timeTransitionCanvasGroup.alpha = 0f;
                yield return FadeCanvasGroup(timeTransitionCanvasGroup, 1f, 0.3f);
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.3f);
        }

        // Place the player at the bed's edge, lying back, while the screen is still black.
        if (player != null)
        {
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            player.transform.position = GetBedroomBedSeatRootPosition();
            player.transform.rotation = GetBedroomBedFacing();

            if (characterController != null)
            {
                characterController.enabled = true;
            }
        }

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = GetBedroomSeatedCameraPosition(0.52f);
            playerCamera.transform.localRotation = Quaternion.Euler(-68f, 0f, -3f);
        }

        yield return new WaitForSecondsRealtime(0.2f);

        if (timeTransitionCanvasGroup != null)
        {
            yield return FadeCanvasGroup(timeTransitionCanvasGroup, 0f, 1.05f);
        }

        if (timeTransitionPanel != null)
        {
            timeTransitionPanel.SetActive(false);
        }

        // Ease from lying down into a seated pose at the bed corner before the wake-up dialogue.
        if (playerCamera != null)
        {
            var lyingPosition = playerCamera.transform.localPosition;
            var lyingRotation = playerCamera.transform.localRotation;
            var seatedPosition = GetBedroomSeatedCameraPosition(0.92f);
            const float sitDuration = 0.95f;
            float elapsed = 0f;
            while (elapsed < sitDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / sitDuration));
                playerCamera.transform.localPosition = Vector3.Lerp(lyingPosition, seatedPosition, progress);
                playerCamera.transform.localRotation = Quaternion.Slerp(
                    lyingRotation,
                    Quaternion.identity,
                    progress);
                yield return null;
            }

            playerCamera.transform.localPosition = seatedPosition;
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        StartConversation(bedroomReturnDialogue, ScenePhase.BedroomReturn);
    }

    private Vector3 GetBedroomBedSeatRootPosition()
    {
        if (bedroomBed == null)
        {
            return player != null ? player.transform.position : Vector3.zero;
        }

        var renderers = bedroomBed.GetComponentsInChildren<Renderer>();
        var bedBounds = new Bounds(bedroomBed.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!hasBounds)
            {
                bedBounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bedBounds.Encapsulate(renderers[i].bounds);
            }
        }

        float halfWidth = hasBounds ? Mathf.Max(0.35f, bedBounds.extents.x) : 0.8f;
        float halfDepth = hasBounds ? Mathf.Max(0.35f, bedBounds.extents.z) : 1f;
        var seat = bedroomBed.position + bedroomBed.right * (halfWidth * 0.55f) +
                   bedroomBed.forward * (halfDepth * 0.55f);
        seat.y = bedroomBed.position.y + 0.58f;
        return seat;
    }

    private Quaternion GetBedroomBedFacing()
    {
        if (player == null || bedroomDoor == null)
        {
            return player != null ? player.transform.rotation : Quaternion.identity;
        }

        var towardDoor = bedroomDoor.position - GetBedroomBedSeatRootPosition();
        towardDoor.y = 0f;
        return towardDoor.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(towardDoor.normalized, Vector3.up)
            : player.transform.rotation;
    }

    private Vector3 GetBedroomSeatedCameraPosition(float height)
    {
        return new Vector3(standingCameraLocalPosition.x, height, standingCameraLocalPosition.z);
    }

    private IEnumerator BedroomPhoneSearchLook()
    {
        if (playerCamera == null)
        {
            yield break;
        }

        const float glanceDuration = 0.38f;
        Quaternion center = playerCamera.transform.localRotation;
        Quaternion left = Quaternion.Euler(4f, -29f, -2f);
        Quaternion right = Quaternion.Euler(2f, 31f, 2f);
        Quaternion[] targets = { left, right, center };
        for (int i = 0; i < targets.Length; i++)
        {
            var start = playerCamera.transform.localRotation;
            float elapsed = 0f;
            while (elapsed < glanceDuration && phase == ScenePhase.BedroomReturn && dialogueActive)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / glanceDuration));
                playerCamera.transform.localRotation = Quaternion.Slerp(start, targets[i], progress);
                yield return null;
            }

            if (phase != ScenePhase.BedroomReturn || !dialogueActive)
            {
                yield break;
            }
        }

        playerCamera.transform.localRotation = center;
    }

    private IEnumerator StandUpFromBedroomBed()
    {
        if (player == null || playerCamera == null || bedroomBed == null)
        {
            phase = ScenePhase.AuntElaSecondApproach;
            currentObjective = "Go to Aunt Ela's house";
            currentPrompt = string.Empty;
            fredScareTriggered = false;
            SetAuntElaMarkerVisible(true);
            ReleasePlayerControls();
            yield break;
        }

        Vector3 startRoot = player.transform.position;
        Vector3 endRoot = startRoot + bedroomBed.right * 0.55f;
        endRoot.y = bedroomBed.position.y;
        Vector3 startCamera = playerCamera.transform.localPosition;
        Vector3 endCamera = standingCameraLocalPosition;
        Quaternion startRotation = playerCamera.transform.localRotation;
        const float standDuration = 0.85f;
        float elapsed = 0f;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        while (elapsed < standDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / standDuration));
            player.transform.position = Vector3.Lerp(startRoot, endRoot, progress);
            playerCamera.transform.localPosition = Vector3.Lerp(startCamera, endCamera, progress);
            playerCamera.transform.localRotation = Quaternion.Slerp(startRotation, Quaternion.identity, progress);
            yield return null;
        }

        player.transform.position = endRoot;
        playerCamera.transform.localPosition = standingCameraLocalPosition;
        playerCamera.transform.localRotation = Quaternion.identity;
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // Resume the story at Aunt Ela's house only after the player has stood up and stepped
        // down from the mattress.
        phase = ScenePhase.AuntElaSecondApproach;
        currentObjective = "Go to Aunt Ela's house";
        currentPrompt = string.Empty;
        fredScareTriggered = false;
        SetAuntElaMarkerVisible(true);
        ReleasePlayerControls();
    }

    // Spawns the office workspace model ~10 m ahead of the player inside the hallway.
    private void SpawnHallwayOfficeSetup()
    {
        if (player == null || hallwayOfficeSetup != null)
        {
            return;
        }

        // Keep the setup on the hallway's walkable axis. If the player is looking into a side
        // wall when the 70 m dialogue ends, using their raw forward vector would place the PC
        // through that wall and make the objective impossible to reach.
        Vector3 spawnPosition;
        if (hallwayRoot != null)
        {
            var playerLocal = hallwayRoot.InverseTransformPoint(player.transform.position);
            var flatLook = player.transform.forward;
            flatLook.y = 0f;
            float lookAlongHallway = Vector3.Dot(flatLook.normalized, hallwayRoot.forward);
            float travelDirection = Mathf.Abs(lookAlongHallway) > 0.35f
                ? Mathf.Sign(lookAlongHallway)
                : (playerLocal.z >= hallwaySpawnLocalZ ? 1f : -1f);
            spawnPosition = hallwayRoot.TransformPoint(new Vector3(
                0f,
                0.1f,
                playerLocal.z + travelDirection * 10f));
        }
        else
        {
            spawnPosition = player.transform.position + player.transform.forward * 10f;
            spawnPosition.y = 0.1f;
        }

        var setup = SpawnOfficeWorkspaceModel(spawnPosition, player.transform.position);
        if (setup == null)
        {
            // Fallback: a simple desk-shaped placeholder so the beat still works.
            setup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            setup.name = "HallwayOfficeSetup";
            setup.transform.position = spawnPosition + Vector3.up * 0.4f;
            setup.transform.localScale = new Vector3(1.8f, 0.8f, 0.9f);
            var renderer = setup.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(GetPlaceholderShader());
                material.color = new Color(0.45f, 0.45f, 0.5f, 1f);
                renderer.sharedMaterial = material;
            }
        }

        hallwayOfficeSetup = setup;
        currentObjective = "Go to the PC setup";
    }

    // Loads the "office setup/Office Work Space" OBJ parts and assembles them into one setup.
    private GameObject SpawnOfficeWorkspaceModel(Vector3 position, Vector3 lookAtPosition)
    {
#if UNITY_EDITOR
        const string folder = "Assets/office setup/Office Work Space";
        var parts = new GameObject("HallwayOfficeSetup");
        parts.transform.position = position;

        var facing = lookAtPosition - position;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
        {
            parts.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
        }

        var combinedBounds = new Bounds(position, Vector3.zero);
        bool hasBounds = false;
        var anyRenderer = false;
        for (int i = 0; i < 7; i++)
        {
            var assetPath = folder + "/model_" + i + ".obj";
            var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
            {
                continue;
            }

            var part = Instantiate(model, parts.transform);
            part.name = "OfficePart_" + i;

            var partRenderers = part.GetComponentsInChildren<Renderer>();
            if (partRenderers.Length > 0)
            {
                anyRenderer = true;
                var partBounds = partRenderers[0].bounds;
                for (int r = 1; r < partRenderers.Length; r++)
                {
                    partBounds.Encapsulate(partRenderers[r].bounds);
                }

                if (!hasBounds)
                {
                    combinedBounds = partBounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(partBounds);
                }
            }
        }

        if (!anyRenderer || !hasBounds)
        {
            Destroy(parts);
            return null;
        }

        // OBJ units vary per asset, so normalise the whole setup to a sensible desk height and
        // then sit its lowest point exactly on the hallway floor at the spawn position.
        float currentHeight = combinedBounds.size.y;
        if (currentHeight > 0.001f)
        {
            const float targetHeight = 1.5f;
            float scale = targetHeight / currentHeight;
            if (!Mathf.Approximately(scale, 1f))
            {
                parts.transform.localScale = Vector3.one * scale;

                // Recompute bounds after scaling.
                combinedBounds = new Bounds(position, Vector3.zero);
                hasBounds = false;
                foreach (var r in parts.GetComponentsInChildren<Renderer>())
                {
                    if (!hasBounds)
                    {
                        combinedBounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(r.bounds);
                    }
                }
            }
        }

        // Sit the whole setup on the hallway floor and center it on the spawn point.
        float bottomOffset = position.y - combinedBounds.min.y;
        parts.transform.position += Vector3.up * bottomOffset;

        var horizontalCenter = new Vector3(
            combinedBounds.center.x - position.x,
            0f,
            combinedBounds.center.z - position.z);
        parts.transform.position -= horizontalCenter;
        return parts;
#else
        return null;
#endif
    }

    private void BuildBackroomsHallway(Transform parent)
    {
        const float hallwayOriginX = 5000f;
        const float hallwayOriginZ = 5000f;
        const float floorY = 0f;
        const float width = 4f;
        const float height = 3.2f;
        const float segmentLength = 4f;
        const int segmentCount = 40;

        var hallway = new GameObject("BackroomsHallway");
        hallway.transform.SetParent(parent, false);
        hallway.transform.position = new Vector3(hallwayOriginX, floorY, hallwayOriginZ);
        hallwayRoot = hallway.transform;

        wallMaterial = new Material(GetPlaceholderShader());
        wallMaterial.color = Color.white;
        floorMaterial = new Material(GetPlaceholderShader());
        floorMaterial.color = Color.white;
        ceilingMaterial = new Material(GetPlaceholderShader());
        ceilingMaterial.color = new Color(0.74f, 0.70f, 0.54f, 1f);
        hallwayWallPattern = CreateHallwayPattern(false);
        hallwayFloorPattern = CreateHallwayPattern(true);
        SetHallwayMaterialTexture(wallMaterial, hallwayWallPattern, new Vector2(2f, 1.5f));
        SetHallwayMaterialTexture(floorMaterial, hallwayFloorPattern, new Vector2(3f, 3f));
        lightMaterial = CreateEmissiveMaterial(new Color(1f, 0.92f, 0.67f));

        for (int i = 0; i < segmentCount; i++)
        {
            BuildHallwaySegment(hallway.transform, i, segmentLength, width, height);
        }

        // Fully sealed ends so the hallway reads as endless and the sky is never visible.
        // The front end wall is removed again the moment the player spawns so the corridor can
        // extend infinitely in whichever direction they walk.
        hallwayBackEndWall = CreateHallwayBox("EndWallBack", hallway.transform, new Vector3(0f, height * 0.5f, -0.1f),
            new Vector3(width + 0.4f, height, 0.2f), wallMaterial);
        hallwayFrontEndWall = CreateHallwayBox("EndWallFront", hallway.transform,
            new Vector3(0f, height * 0.5f, segmentCount * segmentLength + 0.1f),
            new Vector3(width + 0.4f, height, 0.2f), wallMaterial);
        SetHallwayEndWall(hallwayBackEndWall);
        SetHallwayEndWall(hallwayFrontEndWall);

        hallwaySegmentLength = segmentLength;
        hallwayWidth = width;
        hallwayHeight = height;
        hallwayMaxSegmentBuilt = segmentCount - 1;
        hallwayMinSegmentBuilt = 0;
    }

    private static void SetHallwayEndWall(GameObject endWall)
    {
        if (endWall == null)
        {
            return;
        }

        var collider = endWall.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
    }

    private static void SetHallwayMaterialTexture(Material material, Texture2D texture, Vector2 tiling)
    {
        if (material == null || texture == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiling);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", tiling);
        }
    }

    private static Texture2D CreateHallwayPattern(bool carpet)
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, false)
        {
            name = carpet ? "Liminal Carpet Pattern (Runtime)" : "Aged Liminal Wallpaper (Runtime)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float v = (float)y / size;
                float grain = Mathf.Sin((x * 19 + y * 13) * 0.73f) * 0.035f +
                              Mathf.Sin((x * 7 - y * 23) * 0.31f) * 0.025f;
                Color color;
                if (carpet)
                {
                    float grid = ((x / 8 + y / 8) & 1) == 0 ? 0.025f : -0.018f;
                    float fiber = Mathf.Sin((x * 31 + y * 17) * 0.41f) * 0.035f;
                    color = new Color(0.43f, 0.39f, 0.27f) * (0.96f + grain + grid + fiber);
                }
                else
                {
                    float stripe = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 12f)) < 0.08f ? -0.09f : 0f;
                    float motifX = Mathf.Abs(Mathf.Repeat(u * 6f, 1f) - 0.5f);
                    float motifY = Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f);
                    float motif = motifX + motifY < 0.13f ? 0.055f : 0f;
                    color = new Color(0.69f, 0.64f, 0.43f) * (0.97f + grain + stripe + motif);
                }

                pixels[y * size + x] = new Color(
                    Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
        return texture;
    }

    // One floor/ceiling/both-walls/light unit of the corridor, at segment index i.
    private void BuildHallwaySegment(Transform hallway, int index, float segmentLength, float width, float height)
    {
        float z = index * segmentLength;

        CreateHallwayBox("Floor", hallway, new Vector3(0f, -0.1f, z + segmentLength * 0.5f),
            new Vector3(width + 0.4f, 0.2f, segmentLength), floorMaterial);
        CreateHallwayBox("Ceiling", hallway, new Vector3(0f, height + 0.1f, z + segmentLength * 0.5f),
            new Vector3(width + 0.4f, 0.2f, segmentLength), ceilingMaterial);
        CreateHallwayBox("WallLeft", hallway, new Vector3(-width * 0.5f - 0.1f, height * 0.5f, z + segmentLength * 0.5f),
            new Vector3(0.2f, height, segmentLength), wallMaterial);
        CreateHallwayBox("WallRight", hallway, new Vector3(width * 0.5f + 0.1f, height * 0.5f, z + segmentLength * 0.5f),
            new Vector3(0.2f, height, segmentLength), wallMaterial);

        if (index % 2 == 0)
        {
            CreateHallwayBox("FluorescentPanel", hallway, new Vector3(0f, height - 0.03f, z + segmentLength * 0.5f),
                new Vector3(1.2f, 0.05f, 2.2f), lightMaterial);

            var light = new GameObject("HallwayLight");
            light.transform.SetParent(hallway, false);
            light.transform.localPosition = new Vector3(0f, height - 0.15f, z + segmentLength * 0.5f);
            var lightSource = light.AddComponent<Light>();
            lightSource.type = LightType.Point;
            lightSource.color = new Color(1f, 0.95f, 0.80f);
            lightSource.intensity = 1.4f;
            lightSource.range = 9f;
        }
    }

    // ---- Infinite extension ----
    private Material wallMaterial;
    private Material floorMaterial;
    private Material ceilingMaterial;
    private Material lightMaterial;
    private GameObject hallwayBackEndWall;
    private GameObject hallwayFrontEndWall;
    private float hallwaySegmentLength = 4f;
    private float hallwayWidth = 4f;
    private float hallwayHeight = 3.2f;
    private int hallwayMaxSegmentBuilt;
    private int hallwayMinSegmentBuilt;
    private Texture2D hallwayWallPattern;
    private Texture2D hallwayFloorPattern;

    // Keeps a distant, non-blocking end wall in view while the corridor grows behind it.
    private void UpdateHallwayInfinite()
    {
        if (hallwayRoot == null || player == null || dialogueActive)
        {
            return;
        }

        float localZ = hallwayRoot.InverseTransformPoint(player.transform.position).z;
        int playerSegment = Mathf.FloorToInt(localZ / hallwaySegmentLength);
        const int buildAhead = 10;

        if (playerSegment + buildAhead > hallwayMaxSegmentBuilt)
        {
            for (int i = hallwayMaxSegmentBuilt + 1; i <= playerSegment + buildAhead; i++)
            {
                BuildHallwaySegment(hallwayRoot, i, hallwaySegmentLength, hallwayWidth, hallwayHeight);
            }

            hallwayMaxSegmentBuilt = playerSegment + buildAhead;
        }

        if (playerSegment - buildAhead < hallwayMinSegmentBuilt)
        {
            for (int i = hallwayMinSegmentBuilt - 1; i >= playerSegment - buildAhead; i--)
            {
                BuildHallwaySegment(hallwayRoot, i, hallwaySegmentLength, hallwayWidth, hallwayHeight);
            }

            hallwayMinSegmentBuilt = playerSegment - buildAhead;
        }

        // The sealed perspective wall sits at each end of the visible build range. As the
        // player approaches it, the corridor grows and the wall shifts to the new far end.
        if (hallwayFrontEndWall != null)
        {
            hallwayFrontEndWall.transform.localPosition = new Vector3(
                0f, hallwayHeight * 0.5f, (playerSegment + buildAhead + 1) * hallwaySegmentLength);
        }

        if (hallwayBackEndWall != null)
        {
            hallwayBackEndWall.transform.localPosition = new Vector3(
                0f, hallwayHeight * 0.5f, (playerSegment - buildAhead) * hallwaySegmentLength);
        }
    }

    private static GameObject CreateHallwayBox(
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = localScale;
        var renderer = box.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        return box;
    }
}

// Tiny data holder attached to a spawned creature model. It records how far the creature's feet sit
// below its transform origin, so the chase logic can keep the feet on the terrain instead of
// driving the transform origin (and therefore the whole model) down into the ground.
public sealed class MonsterFeetOffset : MonoBehaviour
{
    public float feetOffset;
}
