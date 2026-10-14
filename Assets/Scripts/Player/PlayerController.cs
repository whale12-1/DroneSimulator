using UnityEngine;
using UnityEngine.InputSystem; // Используем новый Input System

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float speed = 3f;
    public float runSpeed = 6f;
    public float gravity = -9.81f;
    public float mouseSensitivity = 100f;
    public Transform cameraTransform;
    public Animator animator;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (cameraTransform == null)
            Debug.LogWarning("[PlayerController] cameraTransform не назначен — вращение камеры отключено.");
        // Блокируем курсор в центре экрана
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;

        // 1. Вращение камерой (Мышь)
        if (mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity * Time.deltaTime;
            float mouseY = mouseDelta.y * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -70f, 90f); // страховка: вниз не глубже -70°
            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        // 2. Движение (WASD) + бег (Left Shift)
        float animSpeed = 0f;   // скорость для Animator (0 = Idle, 3 = Walk, 6 = Run)
        float direction = 0f;
        if (keyboard != null)
        {
            Vector2 moveInput = new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)
            );

            // Базовая скорость: ходьба или бег
            float moveSpeed = keyboard.leftShiftKey.isPressed ? runSpeed : speed;

            // Направление движения в мировых координатах
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

            // ФИКС: нормализуем, чтобы по диагонали скорость не была выше, чем по прямой
            // (W+D даёт magnitude ≈ 1.414 вместо 1.0 — из-за этого Animator думал, что мы бежим)
            if (move.magnitude > 1f)
                move.Normalize();

            // Двигаем персонажа
            controller.Move(move * moveSpeed * Time.deltaTime);

            // Для Animator: если стоим на месте — 0, иначе передаём целевую скорость (3 или 6),
            // а НЕ реальную (move.magnitude * moveSpeed), чтобы анимация не дёргалась
            animSpeed = moveInput.magnitude > 0.01f ? moveSpeed : 0f;
            direction = moveInput.y;
        }

        // 3. Гравитация (чтобы не летал).
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Небольшое прижатие к земле
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // 4. Параметры Animator
        if (animator != null)
        {
            animator.SetFloat("Speed", animSpeed);
            animator.SetFloat("Direction", direction);
            animator.SetBool("IsGrounded", controller.isGrounded);
        }
    }
}