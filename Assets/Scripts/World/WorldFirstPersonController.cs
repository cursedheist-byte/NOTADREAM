using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class WorldFirstPersonController : MonoBehaviour
{
    [SerializeField] private Transform cameraPivot;
    [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField, Min(0f)] private float gravity = 20f;
    [SerializeField, Range(1f, 2f)] private float eyeHeight = 1.65f;
    [SerializeField, Range(0f, 89f)] private float pitchLimit = 85f;

    private CharacterController characterController;
    private float verticalSpeed;
    
    private bool ignoreNextMouseDelta;
private float pitch;

private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (cameraPivot == null)
        {
            Transform cameraTransform = transform.Find("FirstPersonCamera");
            if (cameraTransform != null)
                cameraPivot = cameraTransform;
        }

        if (cameraPivot == null)
            Debug.LogError("WorldPlayer requires a child transform named FirstPersonCamera.", this);
        else
        {
            cameraPivot.localPosition = new Vector3(0f, eyeHeight, 0f);
            cameraPivot.localRotation = Quaternion.identity;
        }

        pitch = 0f;
    }

private void OnEnable()
    {
        SetCursorLocked(true);
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || characterController == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
            SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

        if (Cursor.lockState == CursorLockMode.Locked && mouse != null && cameraPivot != null)
        {
            if (ignoreNextMouseDelta)
                ignoreNextMouseDelta = false;
            else
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, look.x, 0f, Space.Self);
                pitch = Mathf.Clamp(pitch - look.y, -pitchLimit, pitchLimit);
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        Vector2 input = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        if (characterController.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;
        verticalSpeed -= gravity * Time.deltaTime;

        Vector3 movement = transform.right * input.x + transform.forward * input.y;
        movement *= walkSpeed;
        movement.y = verticalSpeed;
        characterController.Move(movement * Time.deltaTime);
    }

private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isActiveAndEnabled)
            SetCursorLocked(true);
    }

private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
        ignoreNextMouseDelta = true;
    }
}
