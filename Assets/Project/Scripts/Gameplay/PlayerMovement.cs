using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovement : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private Renderer bodyRenderer;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference lookAction;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float lookSensitivity = 0.12f;

    private CharacterController controller;
    private float verticalSpeed;
    private float pitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        controller.enabled = false;
        playerCamera.enabled = false;
        audioListener.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        controller.enabled = true;
        playerCamera.enabled = true;
        audioListener.enabled = true;
        bodyRenderer.enabled = false;

        moveAction.action.Enable();
        lookAction.action.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || ConnectionService.Instance?.CanPlay != true)
            return;

        Vector2 movement = Vector2.zero;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            UpdateLook();
            movement = moveAction.action.ReadValue<Vector2>();
        }

        UpdateMovement(movement);
    }

    private void UpdateLook()
    {
        Vector2 look = lookAction.action.ReadValue<Vector2>();

        transform.Rotate(Vector3.up, look.x * lookSensitivity);

        pitch -= look.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -85f, 85f);

        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void UpdateMovement(Vector2 input)
    {
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 direction =
            transform.right * input.x +
            transform.forward * input.y;

        if (controller.isGrounded && verticalSpeed < 0f)
            verticalSpeed = -2f;

        verticalSpeed += gravity * Time.deltaTime;

        Vector3 velocity = direction * moveSpeed;
        velocity.y = verticalSpeed;

        controller.Move(velocity * Time.deltaTime);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
            return;

        moveAction.action.Disable();
        lookAction.action.Disable();

        controller.enabled = false;
        playerCamera.enabled = false;
        audioListener.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
