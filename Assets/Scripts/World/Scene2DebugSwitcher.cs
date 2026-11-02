using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// TEMPORARY bug-testing helper (Scene 2). Creates itself automatically in Play Mode.
// Press U: opens a checkpoint menu listing every objective/phase the Scene 2 story reached,
// duplicates numbered ("Go to Aunt Ela's house (1)", "(2)", ...). Press the number (or click)
// to jump straight back to that story point: player teleports there, phase restores.
public sealed class Scene2DebugSwitcher : MonoBehaviour
{
    private sealed class Checkpoint
    {
        public string Label;
        public string PhaseName;
        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation;
        public bool WasNight;
    }

    private static Scene2DebugSwitcher instance;
    private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
    private string lastSeenPhase;
    private bool checkpointsInitialized;
    private Scene2EveningManager manager;
    private Transform playerTransform;
    private Vector2 scroll;
    private bool menuOpen;
    private float nextCheckpointPollTime;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private GUIStyle hintStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isEditor && !Debug.isDebugBuild)
        {
            // Temporary bug-testing helper: never ship it in a release build.
            return;
        }

        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveForActiveScene();
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        ResolveForActiveScene();
    }

    private static void ResolveForActiveScene()
    {
        // The helper must never outlive the Scene 2 manager it inspects: once that object is
        // destroyed its Update would throw every frame, so leave Scene 2 by tearing it down.
        var sceneManager = FindFirstObjectByType<Scene2EveningManager>(FindObjectsInactive.Include);
        if (sceneManager == null)
        {
            if (instance != null)
            {
                Destroy(instance.gameObject);
                instance = null;
            }

            return;
        }

        if (instance == null)
        {
            var host = new GameObject("Scene2DebugSwitcher (TEMP)");
            instance = host.AddComponent<Scene2DebugSwitcher>();
            DontDestroyOnLoad(host);
        }

        instance.manager = sceneManager;
        instance.playerTransform = null;
        instance.checkpointsInitialized = false;
        instance.checkpoints.Clear();
        instance.lastSeenPhase = null;
    }

private void Update()
    {
        if (manager == null)
        {
            // The Scene 2 manager is not alive (not spawned yet, or its scene was unloaded).
            // OnSceneLoaded recreates or removes this helper, so do no work here.
            return;
        }

        if (playerTransform == null)
        {
            var playerObject = GameObject.Find("TestingPlayer");
            if (playerObject == null) playerObject = GameObject.Find("Player");
            playerTransform = playerObject != null ? playerObject.transform : null;
            if (playerTransform == null)
            {
                var cameras = manager.GetComponentsInChildren<Camera>(true);
                if (cameras.Length > 0) playerTransform = cameras[0].transform.root;
            }
        }

        if (!checkpointsInitialized)
        {
            checkpointsInitialized = true;
            var phaseField = manager.GetType().GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance);
            if (phaseField != null)
            {
                var startPhase = phaseField.GetValue(manager);
                lastSeenPhase = startPhase != null ? startPhase.ToString() : string.Empty;
            }
            BuildAllObjectiveCheckpoints();
        }

        // Phase tracking uses reflection, so poll a few times a second instead of every frame.
        if (Time.unscaledTime >= nextCheckpointPollTime)
        {
            nextCheckpointPollTime = Time.unscaledTime + 0.1f;
            RecordCheckpoints();
        }

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.uKey.wasPressedThisFrame)
        {
            if (menuOpen) CloseMenu();
            else OpenMenu();
        }
        else if (menuOpen && keyboard.escapeKey.wasPressedThisFrame)
        {
            CloseMenu();
        }
        HandleMenuHotkeys();
    }

    private void OpenMenu()
    {
        menuOpen = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseMenu()
    {
        menuOpen = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    private void BuildAllObjectiveCheckpoints()
    {
        if (manager == null)
        {
            return;
        }

        var managerType = manager.GetType();
        var phaseField = managerType.GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance);
        var sceneType = managerType.GetNestedType("ScenePhase", BindingFlags.NonPublic);
        if (phaseField == null || sceneType == null)
        {
            return;
        }

        // Build every objective first so test builds can jump ahead from the start.
        // Earlier objectives are removed from the menu when the story advances.
        var phaseNames = System.Enum.GetNames(sceneType);
        var currentPhase = phaseField.GetValue(manager)?.ToString();
        Vector3 fallbackPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
        Quaternion fallbackRotation = playerTransform != null ? playerTransform.rotation : Quaternion.identity;

        for (int i = 0; i < phaseNames.Length; i++)
        {
            var phaseName = phaseNames[i];
            if (!IsObjectiveCheckpointPhase(phaseName))
            {
                continue;
            }

            var checkpoint = new Checkpoint
            {
                Label = ObjectiveLabelForPhase(phaseName),
                PhaseName = phaseName,
                PlayerPosition = fallbackPosition,
                PlayerRotation = fallbackRotation,
                WasNight = IsNightPhase(phaseName)
            };
            ResolveCheckpointSpawn(checkpoint);
            checkpoints.Add(checkpoint);
        }

        checkpoints.Sort((a, b) => GetCheckpointOrder(a.PhaseName).CompareTo(GetCheckpointOrder(b.PhaseName)));
        PruneCompletedCheckpoints(currentPhase);
        var current = checkpoints.Find(c => c.PhaseName == currentPhase);
        if (current != null && playerTransform != null)
        {
            current.PlayerPosition = playerTransform.position;
            current.PlayerRotation = playerTransform.rotation;
        }
    }

    private static bool IsObjectiveCheckpointPhase(string phaseName)
    {
        switch (phaseName)
        {
            case "GoHome":
            case "EnterHouse":
            case "GoToMom":
            case "CheckLaptop":
            case "BedroomTransition":
            case "PhonePrompt":
            case "GoToMomAfterTimeSkip":
            case "LateCheckLaptop":
            case "FredArgumentPhone":
            case "WindowDistraction":
            case "BrotherMissingSearch":
            case "OutsideSearch":
            case "FredWarningPhone":
            case "ReadMomNote":
            case "EscapeDanger":
            case "FinalEscape":
            case "Finished":
            case "AuntElaApproach":
            case "HallwayExplore":
            case "HallwayGoToPc":
            case "AuntElaSecondApproach":
                return true;
            default:
                return false;
        }
    }



    private static string ObjectiveLabelForPhase(string phaseName)
    {
        switch (phaseName)
        {
            case "GoHome": return "Go home";
            case "EnterHouse": return "Enter the house";
            case "GoToMom": return "Go to Mom";
            case "CheckLaptop": return "Check the laptop";
            case "BedroomTransition": return "Get into your room";
            case "PhonePrompt": return "Check messages";
            case "GoToMomAfterTimeSkip": return "Go to Mom (night)";
            case "LateCheckLaptop": return "Check the laptop (night)";
            case "FredArgumentPhone": return "Check messages from Fred";
            case "WindowDistraction": return "Distract the intruder";
            case "BrotherMissingSearch": return "Find Little Brother in the house";
            case "OutsideSearch": return "Go outside and search for Little Brother";
            case "FredWarningPhone": return "Go inside the house and call the cops";
            case "ReadMomNote": return "Go inside the house and call the cops";
            case "EscapeDanger": return "Get into your room and close the door!";
            case "FinalEscape": return "Run outside to the road!";
            case "Finished": return "Go to Aunt Ela's house (chase start)";
            case "AuntElaApproach": return "Go to Aunt Ela's house";
            case "HallwayExplore": return "Find a way to get out of here";
            case "HallwayGoToPc": return "Go to the PC setup";
            case "AuntElaSecondApproach": return "Go to Aunt Ela's house (after hallway)";
            default: return PrettifyPhase(phaseName);
        }
    }

    private void PruneCompletedCheckpoints(string currentPhase)
    {
        if (manager == null || string.IsNullOrEmpty(currentPhase))
        {
            return;
        }

        var phaseField = manager.GetType().GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance);
        var phaseType = phaseField != null ? phaseField.FieldType : null;
        if (phaseType == null || !System.Enum.IsDefined(phaseType, currentPhase) ||
            currentPhase == "GameOver" || currentPhase == "RetryCheckpoint")
        {
            return;
        }

        int currentOrder = GetCheckpointOrder(currentPhase);
        checkpoints.RemoveAll(checkpoint =>
            checkpoint != null && GetCheckpointOrder(checkpoint.PhaseName) < currentOrder);
    }

    private int GetCheckpointOrder(string phaseName)
    {
        var phaseField = manager != null
            ? manager.GetType().GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance)
            : null;
        var phaseType = phaseField != null ? phaseField.FieldType : null;
        if (phaseType == null || !System.Enum.IsDefined(phaseType, phaseName))
        {
            return int.MaxValue;
        }

        // "Finished" starts the first Aunt Ela trip; the later second approach happens after
        // the hallway sequence, despite its enum declaration appearing before the hallway phases.
        if (phaseName == "Finished")
            return System.Convert.ToInt32(System.Enum.Parse(phaseType, "FinalEscape")) + 1;
        if (phaseName == "AuntElaSecondApproach")
            return System.Convert.ToInt32(System.Enum.Parse(phaseType, "BedroomReturn"));

        return System.Convert.ToInt32(System.Enum.Parse(phaseType, phaseName));
    }



    private void ResolveCheckpointSpawn(Checkpoint checkpoint)
    {
        if (manager == null || checkpoint == null) return;
        var managerType = manager.GetType();
        var homeHouseField = managerType.GetField("homeHouse", BindingFlags.NonPublic | BindingFlags.Instance);
        var homeHouse = homeHouseField != null ? homeHouseField.GetValue(manager) as Transform : null;
        var playerCameraField = managerType.GetField("playerCamera", BindingFlags.NonPublic | BindingFlags.Instance);
        var playerCamera = playerCameraField != null ? playerCameraField.GetValue(manager) as Camera : Camera.main;

        // For all unvisited phases, put the player at a useful location near the home house,
        // instead of waiting until they naturally reach that objective in a complete playthrough.
        if (homeHouse != null)
        {
            var door = homeHouse.Find("PFB_Building_Base/PFB_DoorDouble");
            var bed = homeHouse.Find("PFB_Bed");
            var laptop = GameObject.Find("SM_PKS_Laptop_low (1)")?.transform;
            var diningLaptop = GameObject.Find("SM_PKS_Laptop_low")?.transform;
            var mom = GameObject.Find("Mom");
            var brother = GameObject.Find("LittleBrother");
            var note = GameObject.Find("MomNote");
            var phase = checkpoint.PhaseName;

            if (phase == "Intro" || phase == "GoHome" || phase == "Finished" ||
                phase == "JungleChase" || phase == "JungleChaseSpotted" || phase == "JungleChaseVanish" ||
                phase == "AuntElaApproach" || phase == "AuntElaSecondApproach" ||
                phase == "AuntElaApproachFredScare" || phase == "AuntElaFredScareBlackout" ||
                phase == "AuntElaSpot" || phase == "AuntElaRush" || phase.StartsWith("Hallway"))
            {
                checkpoint.PlayerPosition = homeHouse.position + homeHouse.forward * 12f + Vector3.up;
            }
            else if (phase == "EnterHouse" || phase == "EnterHouseDialogue")
            {
                checkpoint.PlayerPosition = door != null ? door.position + homeHouse.forward * 1.5f + Vector3.up : homeHouse.position + Vector3.up;
            }
            else if (phase == "GoToMom" || phase == "MomDialogue")
            {
                checkpoint.PlayerPosition = mom != null ? mom.transform.position + Vector3.back * 2f : homeHouse.position + Vector3.up;
            }
            else if (phase == "CheckLaptop" || phase == "BrotherDialogue" || phase == "RedRoomIntroDialogue" ||
                     phase == "RedRoomImages" || phase == "ClosingDiningLaptop" || phase == "LittleBrotherRedRoomDialogue")
            {
                checkpoint.PlayerPosition = diningLaptop != null ? diningLaptop.position + Vector3.back * 2f : homeHouse.position + Vector3.up;
            }
            else if (phase == "BedroomTransition" || phase == "BedPrompt" || phase == "LyingDownTransition" ||
                     phase == "PhonePrompt" || phase == "PhoneOpen" || phase == "PostPhoneDialogue" ||
                     phase == "LaptopSequence" || phase == "TimeTransition" || phase == "NightLaptopDialogue")
            {
                checkpoint.PlayerPosition = bed != null ? bed.position + Vector3.back * 2f + Vector3.up : homeHouse.position + Vector3.up;
            }
            else if (phase == "LateCheckLaptop")
            {
                checkpoint.PlayerPosition = diningLaptop != null ? diningLaptop.position + Vector3.back * 2f : homeHouse.position + Vector3.up;
            }
            else if (phase == "GoToMomAfterTimeSkip" || phase == "MissingMomDialogue")
            {
                checkpoint.PlayerPosition = bed != null ? bed.position + Vector3.back * 2f + Vector3.up : homeHouse.position + Vector3.up;
            }
            else if (phase == "BrotherMissingSearch" || phase == "BathroomShadow" || phase == "OutsideSearch")
            {
                checkpoint.PlayerPosition = homeHouse.position + Vector3.up;
            }
            else if (phase == "FredWarningPhone" || phase == "FredLinkLeadIn" || phase == "FredLinkLoading" ||
                     phase == "FredImageDialogue" || phase == "FredImagePhoneClose" || phase == "MomNoteApproach" ||
                     phase == "MomNoteText" || phase == "MomAfterNoteDialogue" || phase == "ReadMomNote" ||
                     phase == "MysteriousMan" || phase == "EscapeDoorSetup" || phase == "WindowDistraction" ||
                     phase == "EscapeDanger" || phase == "RoomSecured" || phase == "FinalEscape")
            {
                checkpoint.PlayerPosition = note != null ? note.transform.position + Vector3.back * 1.5f + Vector3.up : homeHouse.position + Vector3.up;
            }

            if (phase == "ReadMomNote" && door != null)
            {
                var insideDirection = homeHouse.position - door.position;
                insideDirection.y = 0f;
                if (insideDirection.sqrMagnitude < 0.001f) insideDirection = homeHouse.forward;
                checkpoint.PlayerPosition = door.position + insideDirection.normalized * 1.25f +
                    Vector3.Cross(Vector3.up, insideDirection.normalized) * 0.45f + Vector3.up;
            }
            else if (phase == "WindowDistraction" || phase == "EscapeDanger")
            {
                var bedroomDoorField = managerType.GetField("bedroomDoor", BindingFlags.NonPublic | BindingFlags.Instance);
                var bedroomDoor = bedroomDoorField != null ? bedroomDoorField.GetValue(manager) as Transform : null;
                checkpoint.PlayerPosition = bedroomDoor != null
                    ? bedroomDoor.position + bedroomDoor.forward * 2f + Vector3.up
                    : (bed != null ? bed.position + Vector3.back * 2f + Vector3.up : homeHouse.position + Vector3.up);
            }
            else if (phase == "FinalEscape")
            {
                checkpoint.PlayerPosition = bed != null
                    ? bed.position + Vector3.back * 2f + Vector3.up
                    : homeHouse.position + Vector3.up;
            }

            if (phase.StartsWith("Hallway"))
            {
                var hallwayField = managerType.GetField("hallwayRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                var hallwayRoot = hallwayField != null ? hallwayField.GetValue(manager) as Transform : null;
                checkpoint.PlayerPosition = hallwayRoot != null
                    ? hallwayRoot.TransformPoint(new Vector3(0f, 1f, 1.5f))
                    : new Vector3(5000f, 1f, 5001.5f);
                checkpoint.PlayerRotation = hallwayRoot != null
                    ? Quaternion.LookRotation(hallwayRoot.forward, Vector3.up)
                    : Quaternion.identity;
            }

            if (playerCamera != null)
            {
                Vector3 target = door != null ? door.position : homeHouse.position;
                if (phase == "GoToMom" || phase == "MomDialogue")
                    target = mom != null ? mom.transform.position : target;
                else if (phase == "CheckLaptop" || phase == "LateCheckLaptop" ||
                         phase == "BrotherMissingSearch" || phase == "BrotherDialogue")
                    target = diningLaptop != null ? diningLaptop.position : target;
                else if (phase == "BedroomTransition" || phase == "PhonePrompt" ||
                         phase == "GoToMomAfterTimeSkip" || phase == "FinalEscape")
                    target = bed != null ? bed.position : target;
                else if (phase == "ReadMomNote" && note != null)
                    target = note.transform.position;
                else if (phase == "WindowDistraction")
                {
                    var curtain = homeHouse.Find("PFB_Building_Base/PFB_CurtainSmall");
                    target = curtain != null ? curtain.position : target;
                }
                else if (phase == "OutsideSearch")
                {
                    var focusMethod = managerType.GetMethod("GetBathroomWindowFocusPoint", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (focusMethod != null) target = (Vector3)focusMethod.Invoke(manager, null);
                }
                else if (phase.StartsWith("AuntEla") || phase == "Finished" || phase.StartsWith("Jungle"))
                {
                    var auntPositionMethod = managerType.GetMethod("ResolveAuntElaPosition", BindingFlags.NonPublic | BindingFlags.Instance);
                    target = auntPositionMethod != null
                        ? (Vector3)auntPositionMethod.Invoke(manager, null)
                        : checkpoint.PlayerPosition + homeHouse.forward * 10f;
                }
                else if (phase.StartsWith("Hallway"))
                {
                    var hallwayField = managerType.GetField("hallwayRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                    var hallwayRoot = hallwayField != null ? hallwayField.GetValue(manager) as Transform : null;
                    target = hallwayRoot != null ? checkpoint.PlayerPosition + hallwayRoot.forward * 10f : checkpoint.PlayerPosition + Vector3.forward * 10f;
                }

                var look = target - checkpoint.PlayerPosition;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                    checkpoint.PlayerRotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }
        }
    }

    private void RecordCheckpoints()
    {
        if (manager == null)
        {
            return;
        }

        var managerType = manager.GetType();
        var phaseField = managerType.GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance);
        if (phaseField == null)
        {
            return;
        }

        var phaseValue = phaseField.GetValue(manager);
        var phaseName = phaseValue != null ? phaseValue.ToString() : string.Empty;
        if (string.IsNullOrEmpty(phaseName))
        {
            return;
        }

        // The active objective stays selectable. Objectives from earlier story phases are complete.
        PruneCompletedCheckpoints(phaseName);

        if (phaseName == lastSeenPhase)
        {
            return;
        }
        lastSeenPhase = phaseName;

        if (!IsObjectiveCheckpointPhase(phaseName))
        {
            return;
        }

        var existing = checkpoints.Find(c => c.PhaseName == phaseName);
        if (existing != null)
        {
            existing.PlayerPosition = playerTransform != null ? playerTransform.position : existing.PlayerPosition;
            existing.PlayerRotation = playerTransform != null ? playerTransform.rotation : existing.PlayerRotation;
            existing.WasNight = IsNightPhase(phaseName);
            return;
        }

        var objectiveField = managerType.GetField("currentObjective", BindingFlags.NonPublic | BindingFlags.Instance);
        string objective = objectiveField != null ? objectiveField.GetValue(manager) as string : null;
        checkpoints.Add(new Checkpoint
        {
            Label = string.IsNullOrEmpty(objective) ? ObjectiveLabelForPhase(phaseName) : objective,
            PhaseName = phaseName,
            PlayerPosition = playerTransform != null ? playerTransform.position : Vector3.zero,
            PlayerRotation = playerTransform != null ? playerTransform.rotation : Quaternion.identity,
            WasNight = IsNightPhase(phaseName)
        });
        checkpoints.Sort((a, b) => GetCheckpointOrder(a.PhaseName).CompareTo(GetCheckpointOrder(b.PhaseName)));
    }

    private static bool IsNightPhase(string phaseName)
    {
        switch (phaseName)
        {
            case "GoToMomAfterTimeSkip":
            case "MissingMomDialogue":
            case "LateDiningLaptopDialogue":
            case "LateCheckLaptop":
            case "RedRoomIntroDialogue":
            case "RedRoomImages":
            case "ClosingDiningLaptop":
            case "LittleBrotherRedRoomDialogue":
            case "FredArgumentLeadIn":
            case "FredArgumentPhone":
            case "TwoHoursLaterTransition":
            case "WakeUpSequence":
            case "BrotherMissingSearch":
            case "BathroomShadow":
            case "OutsideSearch":
            case "FredWarningPhone":
            case "FredLinkLeadIn":
            case "FredLinkLoading":
            case "FredImageDialogue":
            case "FredImagePhoneClose":
            case "MomNoteApproach":
            case "MomNoteText":
            case "MomAfterNoteDialogue":
            case "ReadMomNote":
            case "MysteriousMan":
            case "EscapeDoorSetup":
            case "WindowDistraction":
            case "EscapeDanger":
            case "RoomSecured":
            case "FinalEscape":
                return true;
            default:
                return false;
        }
    }

    private static string PrettifyPhase(string phaseName)
    {
        switch (phaseName)
        {
            case "Intro": return "Intro (Fred dark web talk)";
            case "GoHome": return "Go home";
            case "EnterHouse": return "Enter the house";
            case "EnterHouseDialogue": return "I am home (dialogue)";
            case "GoToMom": return "Go to Mom";
            case "MomDialogue": return "Mom dialogue";
            case "CheckLaptop": return "Check the laptop";
            case "BrotherDialogue": return "Little brother dialogue";
            case "BedroomTransition": return "Get into your room";
            case "BedPrompt": return "Lie on the bed (press Q)";
            case "LyingDownTransition": return "Lying down (laptop view)";
            case "PhonePrompt": return "Check messages (open phone)";
            case "PhoneOpen": return "Phone open (chat with Fred)";
            case "PostPhoneDialogue": return "Post-phone dialogue";
            case "LaptopSequence": return "Laptop image sequence";
            case "TimeTransition": return "2 HOURS LATER";
            case "NightLaptopDialogue": return "Night laptop (ask Mom for dinner)";
            case "GoToMomAfterTimeSkip": return "Go to Mom (night)";
            case "MissingMomDialogue": return "Where did mom gone?";
            case "LateCheckLaptop": return "Check lil bro's laptop (night)";
            case "RedRoomIntroDialogue": return "Red room intro";
            case "RedRoomImages": return "Red room images";
            case "LittleBrotherRedRoomDialogue": return "Little brother red room talk";
            case "FredArgumentLeadIn": return "Is this Fred fool?";
            case "FredArgumentPhone": return "Argument with Fred (phone)";
            case "TwoHoursLaterTransition": return "2 HOURS LATER (transition)";
            case "WakeUpSequence": return "Waking up";
            case "BrotherMissingSearch": return "Find Little Brother in the house";
            case "BathroomShadow": return "Bathroom shadow";
            case "OutsideSearch": return "Go outside and search for Little Brother";
            case "FredWarningPhone": return "Fred warning (phone)";
            case "FredLinkLeadIn": return "Maybe he's telling the truth";
            case "FredLinkLoading": return "Opening Fred's link";
            case "FredImageDialogue": return "WTF is this? (image)";
            case "FredImagePhoneClose": return "Close the phone (image)";
            case "MomNoteApproach": return "What's this? (note)";
            case "MomNoteText": return "Reading Mom's note";
            case "MomAfterNoteDialogue": return "Go to Aunt Ela's house and tell Mom";
            case "ReadMomNote": return "Press E to read note";
            case "MysteriousMan": return "Mysterious man outside";
            case "EscapeDoorSetup": return "Escape setup";
            case "WindowDistraction": return "Distract the intruder";
            case "EscapeDanger": return "Get into your room and close the door!";
            case "RoomSecured": return "That was too close...";
            case "FinalEscape": return "Run outside to the road!";
            case "JungleChaseSpotted": return "Something is moving out there";
            case "JungleChase": return "Run to Aunt Ela's house (chase)";
            case "JungleChaseVanish": return "Where did it go?";
            case "AuntElaApproach": return "Go to Aunt Ela's house (first)";
            case "AuntElaSecondApproach": return "Go to Aunt Ela's house (second, after hallway)";
            case "AuntElaApproachFredScare": return "What's that? (Fred scare)";
            case "AuntElaFredScareBlackout": return "Fred scare blackout";
            case "AuntElaSpot": return "WTF? Not again!";
            case "AuntElaRush": return "Creature rush";
            case "HallwayWake": return "Where am I now? (hallway)";
            case "HallwayExplore": return "Find a way to get out of here (hallway)";
            case "HallwayEndless": return "Is this place endless?";
            case "HallwayPcSpotted": return "What's that? (hallway PC)";
            case "HallwayGoToPc": return "Go to the PC setup";
            case "HallwayPcDialogue": return "Developer conversation";
            case "BedroomReturn": return "Was that a dream? (bedroom return)";
            default: return phaseName;
        }
    }

    private void HandleMenuHotkeys()
    {
        if (!menuOpen || manager == null)
        {
            return;
        }

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        for (int i = 0; i < checkpoints.Count && i < 9; i++)
        {
            bool pressed = i switch
            {
                0 => keyboard.digit1Key.wasPressedThisFrame,
                1 => keyboard.digit2Key.wasPressedThisFrame,
                2 => keyboard.digit3Key.wasPressedThisFrame,
                3 => keyboard.digit4Key.wasPressedThisFrame,
                4 => keyboard.digit5Key.wasPressedThisFrame,
                5 => keyboard.digit6Key.wasPressedThisFrame,
                6 => keyboard.digit7Key.wasPressedThisFrame,
                7 => keyboard.digit8Key.wasPressedThisFrame,
                8 => keyboard.digit9Key.wasPressedThisFrame,
                _ => false
            };

            if (pressed)
            {
                JumpTo(i);
                CloseMenu();
                return;
            }
        }
    }

private void JumpTo(int index)
    {
        if (index < 0 || index >= checkpoints.Count || manager == null) return;
        var checkpoint = checkpoints[index];

        // Resolve from the live scene at selection time. BuildAllObjectiveCheckpoints may run
        // before the story manager has finished creating actors, doors, and the hallway.
        ResolveCheckpointSpawn(checkpoint);

        var managerType = manager.GetType();

        if (playerTransform != null)
        {
            var controller = playerTransform.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            playerTransform.SetPositionAndRotation(checkpoint.PlayerPosition, checkpoint.PlayerRotation);
            if (controller != null) controller.enabled = true;
        }
        var startObjective = managerType.GetMethod("DebugStartObjective", BindingFlags.Public | BindingFlags.Instance);
        if (startObjective == null)
        {
            Debug.LogError("[Scene2DebugSwitcher] Scene2EveningManager.DebugStartObjective is missing.", this);
            return;
        }
        bool started = (bool)startObjective.Invoke(manager, new object[] { checkpoint.PhaseName });
        if (!started) return;
        managerType.GetMethod("UpdateObjectiveUi", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(manager, null);
        lastSeenPhase = checkpoint.PhaseName;
        PruneCompletedCheckpoints(checkpoint.PhaseName);
        Debug.Log("[Scene2DebugSwitcher] Started objective from clean state: " + checkpoint.Label, this);
    }

    private void PrepareObjectiveJump(Checkpoint checkpoint, System.Type managerType)
    {
        switch (checkpoint.PhaseName)
        {
            case "GoHome":
                SetManagerField(managerType, "houseReached", false);
                break;
            case "GoToMomAfterTimeSkip":
                SetManagerField(managerType, "bedroomDoorOpenedAfterTimeSkip", true);
                managerType.GetMethod("SetBedroomDoorOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(manager, new object[] { true });
                break;
            case "PhonePrompt":
            case "FredArgumentPhone":
                SetObjectivePhoneVisible(managerType);
                break;
            case "WindowDistraction":
                managerType.GetMethod("PrepareDistractionProp", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(manager, null);
                break;
            case "FredWarningPhone":
                managerType.GetMethod("StartFredWarningPhone", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(manager, null);
                break;
            case "ReadMomNote":
                SetManagerField(managerType, "noteRead", false);
                managerType.GetMethod("StartMomNoteSequence", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(manager, null);
                break;
            case "EscapeDanger":
                SetManagerField(managerType, "escapeTimerStarted", false);
                SetManagerField(managerType, "escapeTimerExpired", false);
                managerType.GetMethod("StartEscapeDangerTimer", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(manager, null);
                break;
            case "FinalEscape":
                managerType.GetMethod("StartFinalEscapeDash", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(manager, null);
                break;
            case "Finished":
                managerType.GetMethod("CleanupJungleChase", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(manager, null);
                SetManagerField(managerType, "jungleChaseStarted", true);
                SetManagerField(managerType, "jungleChaseVanishShown", false);
                managerType.GetMethod("StartJungleChase", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(manager, null);
                break;
            case "AuntElaApproach":
                SetManagerField(managerType, "auntElaSpotArmed", true);
                SetManagerField(managerType, "auntElaSpotTriggered", false);
                break;
            case "HallwayExplore":
                SetManagerField(managerType, "hallwayExplorationActive", true);
                SetManagerField(managerType, "hallwayEndlessShown", false);
                var hallwayField = managerType.GetField("hallwayRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                var hallwayRoot = hallwayField != null ? hallwayField.GetValue(manager) as Transform : null;
                if (hallwayRoot != null)
                    SetManagerField(managerType, "hallwaySpawnLocalZ", hallwayRoot.InverseTransformPoint(playerTransform.position).z);
                break;
            case "HallwayGoToPc":
                managerType.GetMethod("SpawnHallwayOfficeSetup", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(manager, null);
                SetManagerField(managerType, "hallwayPcApproachArmed", true);
                break;
        }
    }

    private void SetObjectivePhoneVisible(System.Type managerType)
    {
        var phoneField = managerType.GetField("phoneObject", BindingFlags.NonPublic | BindingFlags.Instance);
        var phoneObject = phoneField != null ? phoneField.GetValue(manager) as GameObject : null;
        if (phoneObject != null)
        {
            phoneObject.SetActive(true);
        }
    }

    private void SetManagerField(System.Type managerType, string fieldName, object value)
    {
        managerType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(manager, value);
    }


    private System.Collections.IEnumerator RunLiminalEndingTest()
    {
        const string repeatScenePath = "Assets/Scenes/Scene1_Opening_Repeat.unity";
        var repeatScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("Scene1_Opening_Repeat");
        if (!repeatScene.IsValid() || !repeatScene.isLoaded)
        {
            var load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                repeatScenePath,
                UnityEngine.SceneManagement.LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError("[Scene2DebugSwitcher] Could not load the repeat scene for the liminal ending test.", this);
                yield break;
            }
            while (!load.isDone) yield return null;
            yield return null;
        }

        Scene1OpeningManager endingManager = null;
        var managers = FindObjectsByType<Scene1OpeningManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null && managers[i].gameObject.scene.name == "Scene1_Opening_Repeat")
            {
                endingManager = managers[i];
                break;
            }
        }
        if (endingManager == null || !endingManager.DebugStartLiminalEndingTest())
        {
            Debug.LogError("[Scene2DebugSwitcher] Liminal ending test could not start; check the repeat-scene manager and assigned assets.", this);
        }
    }
private void OnGUI()
    {
        if (manager == null)
            return;

        if (!menuOpen)
        {
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                hintStyle.normal.textColor = new Color(1f, 0.82f, 0.34f);
            }
            GUI.Label(new Rect(18, 14, 360, 28), "[U]  MISSION SELECT", hintStyle);
            return;
        }
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(16, 12, 5, 5)
            };
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = Color.white;
            boxStyle = new GUIStyle(GUI.skin.box);
        }
        float width = Mathf.Min(760f, Screen.width - 32f);
        float height = Mathf.Min(760f, Screen.height - 36f);
        float x = (Screen.width - width) * 0.5f;
        float y = (Screen.height - height) * 0.5f;
        GUI.Box(new Rect(x, y, width, height), GUIContent.none, boxStyle);
        GUI.Label(new Rect(x + 28f, y + 18f, width - 56f, 34f), "SCENE 2  /  MISSION SELECT", headerStyle);
        var sub = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        sub.normal.textColor = new Color(0.78f, 0.84f, 0.9f);
        GUI.Label(new Rect(x + 30f, y + 56f, width - 60f, 40f),
            "TEST MODE • Select any unfinished objective to reset story state and start that mission. Press U or Esc to close.", sub);
        if (GUI.Button(new Rect(x + 30f, y + 104f, width - 60f, 42f), "TEST ENDING: PLAY VIDEO + GO TO LIMINAL SCENE", labelStyle))
        {
            CloseMenu();
            StartCoroutine(RunLiminalEndingTest());
            return;
        }
        float listTop = y + 154f;
        float listHeight = height - 174f;
        float rowHeight = 46f;
        scroll = GUI.BeginScrollView(new Rect(x + 18f, listTop, width - 36f, listHeight), scroll,
            new Rect(0f, 0f, width - 58f, checkpoints.Count * rowHeight + 8f));
        for (int i = 0; i < checkpoints.Count; i++)
        {
            var item = checkpoints[i];
            string caption = (i < 9 ? "[" + (i + 1) + "]   " : "      ") +
                item.Label + (item.WasNight ? "     • NIGHT" : "     • EVENING");
            if (GUI.Button(new Rect(0f, i * rowHeight, width - 62f, rowHeight - 5f), caption, labelStyle))
            {
                JumpTo(i);
                CloseMenu();
            }
        }
        if (checkpoints.Count == 0)
            GUI.Label(new Rect(16f, 12f, width - 90f, 28f), "No unfinished objectives are available.");
        GUI.EndScrollView();
    }
}
