using UnityEngine;

public class HangarInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private InteractionUIController uiController;

    public string GetInteractionPrompt() => "Магазин [E]";

    public bool CanInteract(GameObject interactor) => true;

    public void Interact(GameObject interactor)
    {
        InteractionUIController controller = uiController != null
            ? uiController
            : FindAnyObjectByType<InteractionUIController>();
        if (controller != null)
            controller.OpenMissionMap();
    }
}
