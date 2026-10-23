using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractionDetector : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private InteractionUIController uiController;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (uiController != null && uiController.IsWindowOpen)
        {
            uiController.HideHint();
            return;
        }

        if (playerCamera == null || uiController == null)
            return;

        IInteractable interactable = null;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactableLayer, QueryTriggerInteraction.Ignore))
            interactable = hit.collider.GetComponentInParent<IInteractable>();

        if (interactable == null || !interactable.CanInteract(gameObject))
        {
            uiController.HideHint();
            return;
        }

        uiController.ShowHint(interactable.GetInteractionPrompt());
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            interactable.Interact(gameObject);
    }
}
