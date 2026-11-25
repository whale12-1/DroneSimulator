using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractionDetector : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private InteractionUIController uiController;

    private void Update()
    {
        if (playerCamera == null) return;

        // Пока открыто любое модальное окно бункера (рабочий стол, панель миссий,
        // пауза, настройки), подсказка не нужна: раньше она оставалась включённой
        // поверх терминала, а рейкаст продолжал выполняться при Time.timeScale = 0.
        if (BunkerModalStack.AnyOpen)
        {
            if (uiController != null) uiController.HideHint();
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactableLayer, QueryTriggerInteraction.Ignore))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null && interactable.CanInteract(gameObject))
            {
                if (uiController != null)
                    uiController.ShowHint(interactable.GetInteractionPrompt());

                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    interactable.Interact(gameObject);
                }

                HandleSecondaryAction(interactable);
                return;
            }
        }

        if (uiController != null)
            uiController.HideHint();
    }

    // R и колесо мыши — дополнительное действие при взгляде на объект (радио).
    // Работает только если объект в фокусе и сам подтверждает готовность через
    // ISecondaryInteractable, поэтому остальные интеракторы не затронуты.
    private void HandleSecondaryAction(IInteractable interactable)
    {
        if (!(interactable is ISecondaryInteractable secondary)) return;
        if (!secondary.CanInteractSecondary(gameObject)) return;

        float direction = 0f;
        bool triggered = false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            triggered = true;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;

            if (scroll > 0.01f)
            {
                direction = 1f;
                triggered = true;
            }
            else if (scroll < -0.01f)
            {
                direction = -1f;
                triggered = true;
            }
        }

        if (triggered) secondary.OnSecondaryInteract(gameObject, direction);
    }
}