using UnityEngine;

public class ComputerInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameObject computerUIPanel;

    private void Start()
    {
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
    }

    public string GetInteractionPrompt()
    {
        return "Войти в систему [E]";
    }

    public bool CanInteract(GameObject interactor)
    {
        if (playerController == null) return false;
        return playerController.isSitting;
    }

    public void Interact(GameObject interactor)
    {
        if (computerUIPanel == null)
        {
            Debug.Log("Компьютер включен, но UI пока не назначен.");
            return;
        }

        ComputerUIController controller = computerUIPanel.GetComponent<ComputerUIController>();
        if (controller == null)
        {
            Debug.Log("На объекте ComputerOS отсутствует компонент ComputerUIController.");
            return;
        }

        controller.ToggleDesktop();
    }
}