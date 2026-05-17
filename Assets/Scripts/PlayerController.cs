using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Move Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float crouchSpeed = 1.5f;
    public float gravity = -9.81f;

    [Header("Crouch Settings")]
    public float standHeight = 2f;
    public float crouchHeight = 1f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isCrouching;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        controller.height = standHeight;
    }

    void Update()
    {
        HandleCrouch();
        HandleMovement();
    }

    void HandleMovement()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0) velocity.y = -2f;

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        float speed = isCrouching ? crouchSpeed : (kb.leftShiftKey.isPressed ? runSpeed : walkSpeed);

        Vector3 move = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);
        controller.Move(move * speed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleCrouch()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.cKey.wasPressedThisFrame)
        {
            isCrouching = !isCrouching;
            controller.height = isCrouching ? crouchHeight : standHeight;
        }
    }
}
