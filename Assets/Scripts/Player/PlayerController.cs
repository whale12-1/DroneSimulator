using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    private const float LegacyReferenceFrameTime = 1f / 60f;

    public float speed = 3f;
    public float gravity = -9.81f;
    public float mouseSensitivity = 100f;
    public Transform cameraTransform;
    [SerializeField] private Animator animator;
    public bool isSitting = false;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    private float LookSensitivity
    {
        get
        {
            SettingsService service = SettingsService.Instance;
            if (service != null)
                return Mathf.Max(0.001f, service.Data.controls.fpsMouseSensitivity);

            return Mathf.Max(0.001f, mouseSensitivity * LegacyReferenceFrameTime);
        }
    }

    private bool InvertLookY
    {
        get
        {
            SettingsService service = SettingsService.Instance;
            return service != null && service.Data.controls.fpsInvertY;
        }
    }

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogError("[PlayerController] Animator не назначен и не найден у дочерних объектов. Анимации сидения и печати не будут работать.");
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null)
            Debug.LogWarning("[PlayerController] cameraTransform is not assigned.");
        Cursor.lockState = CursorLockMode.Locked;
    }

    // При потере фокуса Unity сбрасывает Cursor.lockState в None. Возвращаем захват курсора,
    // иначе строгая проверка в Update() навсегда остановит обзор после Alt+Tab.
    // При открытом UI (пауза, ComputerOS, панели миссии) Time.timeScale == 0 — курсор не трогаем.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;
        if (Time.timeScale <= 0f) return;
        if (Cursor.lockState == CursorLockMode.Locked) return;

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // На паузе (Time.timeScale == 0) и при разблокированном курсоре (открыт pause/ComputerOS)
        // управление отключается целиком. mouse.delta от Time.timeScale не зависит, поэтому без
        // этой проверки камера продолжала вращаться мышью при открытом меню паузы/настроек.
        if (Time.timeScale == 0f || Cursor.lockState != CursorLockMode.Locked)
            return;

        var mouse = Mouse.current;
        var keyboard = Keyboard.current;

        float animSpeed = 0f;
        float direction = 0f;
        float strafe = 0f;

        if (mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            float sensitivity = LookSensitivity;
            float mouseX = mouseDelta.x * sensitivity;
            float mouseY = mouseDelta.y * sensitivity;

            if (InvertLookY) mouseY = -mouseY;

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
        if (animator == null) return;
        animator.SetBool("IsSitting", isSitting);
    }

    public void ForceSitIdle()
    {
        if (animator == null) return;
        animator.SetBool("IsSitting", true);
        animator.Play("SitIdle", 0, 1f);
    }

    public void SetTyping(bool isTyping)
    {
        if (animator == null) return;
        animator.SetBool("IsTyping", isTyping);
    }
}
