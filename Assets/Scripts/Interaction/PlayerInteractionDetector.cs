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
                return;
            }
        }

        if (uiController != null)
            uiController.HideHint();
    }
}