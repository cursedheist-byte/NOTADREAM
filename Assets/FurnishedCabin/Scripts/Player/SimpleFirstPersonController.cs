using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class SimpleFirstPersonController : MonoBehaviour
{
    [SerializeField] private Transform playerCamera;
    
    [SerializeField] private float sprintMultiplier = 2.5f;
[SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float mouseSensitivity = 0.05f;
    [SerializeField] private float interactionRange = 5f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickSound;
    private AudioSource movementAudioSource;
    private AudioClip walkingMovementClip;
    private AudioClip jumpMovementClip;


    private CharacterController characterController;
    private float verticalVelocity;
    private float cameraPitch;
    private InputActionMap playerActions;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    
    private InputAction sprintAction;
private InputAction interactAction;private InputAction mouseInteractAction;
    private Interactable currentInteractable;
    private readonly RaycastHit[] interactionHits = new RaycastHit[32];
    private GUIStyle crosshairStyle;

private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (playerCamera == null)
            throw new System.InvalidOperationException("Assign the player camera before entering Play mode.");

        playerActions = new InputActionMap("Player");
        moveAction = playerActions.AddAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", WorldMasterKeyBindings.GetPath("Forward"))
            .With("Down", WorldMasterKeyBindings.GetPath("Backward"))
            .With("Left", WorldMasterKeyBindings.GetPath("Left"))
            .With("Right", WorldMasterKeyBindings.GetPath("Right"));

        lookAction = playerActions.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
        jumpAction = playerActions.AddAction("Jump", InputActionType.Button, WorldMasterKeyBindings.GetPath("Jump"));
        interactAction = playerActions.AddAction("Interact", InputActionType.Button, WorldMasterKeyBindings.GetPath("Interact"));
        interactAction.performed += OnInteractPerformed;
        sprintAction = playerActions.AddAction("Sprint", InputActionType.Button, WorldMasterKeyBindings.GetPath("Sprint"));
        mouseInteractAction = playerActions.AddAction("MouseInteract", InputActionType.Button, "<Mouse>/leftButton");
        mouseInteractAction.performed += OnMouseInteractPerformed;
        WorldMasterKeyBindings.BindingChanged += OnKeyBindingChanged;
    }

private void OnEnable()
    {
        if (playerActions != null)
            playerActions.Enable();
    }

    private void OnDisable()
    {
        if (playerActions != null)
            playerActions.Disable();
        if (movementAudioSource != null)
        {
            movementAudioSource.Stop();
            movementAudioSource.volume = 0f;
        }
    }

    public void ConfigureMovementAudio(AudioClip walkingClip, AudioClip jumpClip)
    {
        walkingMovementClip = walkingClip;
        jumpMovementClip = jumpClip;
        if (walkingMovementClip == null && jumpMovementClip == null)
            return;

        if (movementAudioSource == null)
        {
            movementAudioSource = gameObject.AddComponent<AudioSource>();
            movementAudioSource.playOnAwake = false;
            movementAudioSource.spatialBlend = 0f;
            movementAudioSource.dopplerLevel = 0f;
            movementAudioSource.loop = true;
            movementAudioSource.volume = 0f;
        }

        movementAudioSource.clip = walkingMovementClip;
    }

    private void OnDestroy()
    {
        WorldMasterKeyBindings.BindingChanged -= OnKeyBindingChanged;
        if (playerActions != null)
            playerActions.Dispose();
    }

    private void OnKeyBindingChanged(string bindingName, Key key)
    {
        if (playerActions == null)
            return;

        bool wasEnabled = playerActions.enabled;
        playerActions.Disable();
        string path = "<Keyboard>/" + key.ToString().ToLowerInvariant();

        switch (bindingName)
        {
            case "Forward": ApplyCompositeBinding("Up", path); break;
            case "Backward": ApplyCompositeBinding("Down", path); break;
            case "Left": ApplyCompositeBinding("Left", path); break;
            case "Right": ApplyCompositeBinding("Right", path); break;
            case "Jump": jumpAction.ApplyBindingOverride(0, path); break;
            case "Sprint": sprintAction.ApplyBindingOverride(0, path); break;
            case "Interact": interactAction.ApplyBindingOverride(0, path); break;
        }

        if (wasEnabled)
            playerActions.Enable();
    }

    private void ApplyCompositeBinding(string partName, string path)
    {
        for (int i = 0; i < moveAction.bindings.Count; i++)
        {
            InputBinding binding = moveAction.bindings[i];
            if (binding.isPartOfComposite && binding.name == partName)
            {
                moveAction.ApplyBindingOverride(i, path);
                return;
            }
        }
    }


    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

private void Update()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
            Look();

        Move();
        UpdateInteractionTarget();
    }

private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        Interact();
    }

private void OnMouseInteractPerformed(InputAction.CallbackContext context)
    {
        if (Cursor.lockState == CursorLockMode.Locked && currentInteractable != null)
        {
            PlaySound(clickSound);
            currentInteractable.Interact();
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }


    private void UpdateInteractionTarget()
    {
        currentInteractable = FindNearestInteractable();
    }

    private Interactable FindNearestInteractable()
    {
        int hitCount = Physics.RaycastNonAlloc(
            playerCamera.position,
            playerCamera.forward,
            interactionHits,
            interactionRange,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        Interactable nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = interactionHits[i].collider;
            if (collider == null || collider.GetComponentInParent<SimpleFirstPersonController>() != null)
                continue;

            Interactable interactable = collider.GetComponentInParent<Interactable>();
            if (interactable != null && interactionHits[i].distance < nearestDistance)
            {
                nearestDistance = interactionHits[i].distance;
                nearest = interactable;
            }
        }

        return nearest;
    }



private void OnGUI()
    {
        if (crosshairStyle == null)
        {
            crosshairStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            crosshairStyle.normal.textColor = Color.white;
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        GUI.Label(new Rect(centerX - 10f, centerY - 12f, 20f, 24f), "+", crosshairStyle);

        if (currentInteractable != null)
            WorldPromptUI.DrawInteractionHint();
    }



private void Look()
    {
        Vector2 lookDelta = lookAction.ReadValue<Vector2>() * mouseSensitivity;
        transform.Rotate(Vector3.up * lookDelta.x);
        cameraPitch = Mathf.Clamp(cameraPitch - lookDelta.y, -85f, 85f);
        playerCamera.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

private void Move()
    {
        Vector2 movementInput = moveAction.ReadValue<Vector2>();
        bool groundedBeforeMove = characterController.isGrounded;
        float speed = moveSpeed * (sprintAction.IsPressed() ? sprintMultiplier : 1f);
        Vector3 move = (transform.right * movementInput.x + transform.forward * movementInput.y).normalized * speed;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        bool jumpedThisFrame = groundedBeforeMove && jumpAction.WasPressedThisFrame();
        if (jumpedThisFrame)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (jumpMovementClip != null && movementAudioSource != null)
                movementAudioSource.PlayOneShot(jumpMovementClip, 0.55f);
        }

        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;
        characterController.Move(move * Time.deltaTime);
        UpdateMovementAudio(groundedBeforeMove && !jumpedThisFrame && movementInput.sqrMagnitude > 0.015f);
    }

    private void UpdateMovementAudio(bool shouldPlayFootsteps)
    {
        if (movementAudioSource == null || walkingMovementClip == null)
            return;

        if (shouldPlayFootsteps && !movementAudioSource.isPlaying)
            movementAudioSource.Play();

        movementAudioSource.pitch = sprintAction != null && sprintAction.IsPressed() ? 1.08f : 1f;
        movementAudioSource.volume = Mathf.MoveTowards(
            movementAudioSource.volume,
            shouldPlayFootsteps ? 0.18f : 0f,
            Time.deltaTime * 0.65f);

        if (!shouldPlayFootsteps && movementAudioSource.volume <= 0.005f && movementAudioSource.isPlaying)
        {
            movementAudioSource.Stop();
            movementAudioSource.volume = 0f;
        }
    }

private void Interact()
    {
        Interactable interactable = FindNearestInteractable();
        if (interactable == null)
            return;

        interactable.Interact();
    }
}
