using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float speed = 3f;
    public float gravity = -9.81f;
    public float mouseSensitivity = 100f;
    public Transform cameraTransform;
    public Animator animator;
    public bool isSitting = false;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (cameraTransform == null)
            Debug.LogWarning("[PlayerController] cameraTransform is not assigned.");
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;

        float animSpeed = 0f;
        float direction = 0f;
        float strafe = 0f;

        if (mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity * Time.deltaTime;
            float mouseY = mouseDelta.y * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -70f, 90f);
            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        if (!isSitting)
        {
            if (keyboard != null)
            {
                Vector2 moveInput = new Vector2(
                    (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)
                );

                float moveSpeed = speed;

                Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

                if (move.magnitude > 1f)
                    move.Normalize();

                controller.Move(move * moveSpeed * Time.deltaTime);

                animSpeed = moveInput.magnitude > 0.01f ? moveSpeed : 0f;
                direction = moveInput.y;
                strafe = Mathf.Abs(moveInput.y) < 0.1f ? moveInput.x : 0f;
            }
        }

        if (!isSitting)
        {
            if (controller.isGrounded && velocity.y < 0)
                velocity.y = -2f;

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", animSpeed);
            animator.SetFloat("Direction", direction);
            animator.SetFloat("Strafe", strafe);
            animator.SetBool("IsGrounded", controller.isGrounded);
        }
    }

    public void SetSitting(bool isSitting)
    {
        animator.SetBool("IsSitting", isSitting);
    }

    public void SetTyping(bool isTyping)
    {
        animator.SetBool("IsTyping", isTyping);
    }
}
