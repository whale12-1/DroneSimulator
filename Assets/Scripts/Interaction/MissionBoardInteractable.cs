using UnityEngine;

public class MissionBoardInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private InteractionUIController uiController;

    public string GetInteractionPrompt() => "Карта миссий [E]";

    public bool CanInteract(GameObject interactor) => true;

    public void Interact(GameObject interactor)
    {
        InteractionUIController controller = uiController != null
            ? uiController
            : FindAnyObjectByType<InteractionUIController>();
        if (controller != null)
            controller.OpenMissionBoard();
    }
}
