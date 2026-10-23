using UnityEngine;

public interface IInteractable
{
    string GetInteractionPrompt();
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}
