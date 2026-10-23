using UnityEngine;
using UnityEngine.InputSystem; // РСЃРїРѕР»СЊР·СѓРµРј РЅРѕРІС‹Р№ Input System

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
            Debug.LogWarning("[PlayerController] cameraTransform РЅРµ РЅР°Р·РЅР°С‡РµРЅ вЂ” РІСЂР°С‰РµРЅРёРµ РєР°РјРµСЂС‹ РѕС‚РєР»СЋС‡РµРЅРѕ.");
        // Р‘Р»РѕРєРёСЂСѓРµРј РєСѓСЂСЃРѕСЂ РІ С†РµРЅС‚СЂРµ СЌРєСЂР°РЅР°
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;

        // 1. Р’СЂР°С‰РµРЅРёРµ РєР°РјРµСЂРѕР№ (РњС‹С€СЊ)
        if (mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            float mouseX = mouseDelta.x * mouseSensitivity * Time.deltaTime;
            float mouseY = mouseDelta.y * mouseSensitivity * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -70f, 90f); // СЃС‚СЂР°С…РѕРІРєР°: РІРЅРёР· РЅРµ РіР»СѓР±Р¶Рµ -70В°
            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        // 2. Р”РІРёР¶РµРЅРёРµ (WASD) + Р±РµРі (Left Shift)
        float animSpeed = 0f;   // СЃРєРѕСЂРѕСЃС‚СЊ РґР»СЏ Animator (0 = Idle, 3 = Walk, 6 = Run)
        float direction = 0f;
        if (keyboard != null)
        {
            Vector2 moveInput = new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)
            );

            // Р‘Р°Р·РѕРІР°СЏ СЃРєРѕСЂРѕСЃС‚СЊ: С…РѕРґСЊР±Р° РёР»Рё Р±РµРі
            float moveSpeed = keyboard.leftShiftKey.isPressed ? runSpeed : speed;

            // РќР°РїСЂР°РІР»РµРЅРёРµ РґРІРёР¶РµРЅРёСЏ РІ РјРёСЂРѕРІС‹С… РєРѕРѕСЂРґРёРЅР°С‚Р°С…
            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

            // Р¤РРљРЎ: РЅРѕСЂРјР°Р»РёР·СѓРµРј, С‡С‚РѕР±С‹ РїРѕ РґРёР°РіРѕРЅР°Р»Рё СЃРєРѕСЂРѕСЃС‚СЊ РЅРµ Р±С‹Р»Р° РІС‹С€Рµ, С‡РµРј РїРѕ РїСЂСЏРјРѕР№
            // (W+D РґР°С‘С‚ magnitude в‰€ 1.414 РІРјРµСЃС‚Рѕ 1.0 вЂ” РёР·-Р·Р° СЌС‚РѕРіРѕ Animator РґСѓРјР°Р», С‡С‚Рѕ РјС‹ Р±РµР¶РёРј)
            if (move.magnitude > 1f)
                move.Normalize();

            // Р”РІРёРіР°РµРј РїРµСЂСЃРѕРЅР°Р¶Р°
            controller.Move(move * moveSpeed * Time.deltaTime);

            // Р”Р»СЏ Animator: РµСЃР»Рё СЃС‚РѕРёРј РЅР° РјРµСЃС‚Рµ вЂ” 0, РёРЅР°С‡Рµ РїРµСЂРµРґР°С‘Рј С†РµР»РµРІСѓСЋ СЃРєРѕСЂРѕСЃС‚СЊ (3 РёР»Рё 6),
            // Р° РќР• СЂРµР°Р»СЊРЅСѓСЋ (move.magnitude * moveSpeed), С‡С‚РѕР±С‹ Р°РЅРёРјР°С†РёСЏ РЅРµ РґС‘СЂРіР°Р»Р°СЃСЊ
            animSpeed = moveInput.magnitude > 0.01f ? moveSpeed : 0f;
            direction = moveInput.y;
        }

        // 3. Р“СЂР°РІРёС‚Р°С†РёСЏ (С‡С‚РѕР±С‹ РЅРµ Р»РµС‚Р°Р»).
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // РќРµР±РѕР»СЊС€РѕРµ РїСЂРёР¶Р°С‚РёРµ Рє Р·РµРјР»Рµ
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        // 4. РџР°СЂР°РјРµС‚СЂС‹ Animator
        if (animator != null)
        {
            animator.SetFloat("Speed", animSpeed);
            animator.SetFloat("Direction", direction);
            animator.SetBool("IsGrounded", controller.isGrounded);
        }
    }
}