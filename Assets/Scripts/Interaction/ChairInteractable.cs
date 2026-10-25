using UnityEngine;
using UnityEngine.InputSystem;

public class ChairInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform sitPosition;
    [SerializeField] private Transform standPosition;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Collider chairCollider;
    [SerializeField] private InteractionUIController uiController;

    private CharacterController characterController;
    private bool isSitting;

    private void Awake()
    {
        if (chairCollider == null)
            chairCollider = GetComponent<Collider>();
    }

    public string GetInteractionPrompt()
    {
        return isSitting ? "Встать [Пробел]" : "Сесть [E]";
    }

    public bool CanInteract(GameObject interactor)
    {
        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (isSitting)
            StandUp();
        else
            SitDown(interactor);
    }

    private void Update()
    {
        if (!isSitting)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            StandUp();
    }

    private void SitDown(GameObject interactor)
    {
        ResolvePlayer(interactor);

        if (playerController == null || sitPosition == null)
            return;

        if (characterController != null)
            characterController.enabled = false;

        if (chairCollider != null)
            chairCollider.enabled = false;

        Transform player = playerController.transform;
        player.position = sitPosition.position;
        player.rotation = sitPosition.rotation;

        isSitting = true;
        playerController.isSitting = true;
        playerController.SetSitting(true);

        if (uiController != null)
            uiController.ShowHint("Встать [Пробел]");
    }

    private void StandUp()
    {
        if (playerController == null)
            return;

        if (characterController != null)
            characterController.enabled = false;

        if (standPosition != null)
        {
            Transform player = playerController.transform;
            player.position = standPosition.position;
            player.rotation = standPosition.rotation;
        }

        if (chairCollider != null)
            chairCollider.enabled = true;

        if (characterController != null)
            characterController.enabled = true;

        isSitting = false;
        playerController.isSitting = false;
        playerController.SetSitting(false);

        if (uiController != null)
            uiController.HideHint();
    }

    private void ResolvePlayer(GameObject interactor)
    {
        if (playerController == null && interactor != null)
            playerController = interactor.GetComponent<PlayerController>();
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();
        if (characterController == null && playerController != null)
            characterController = playerController.GetComponent<CharacterController>();
        if (uiController == null)
            uiController = FindAnyObjectByType<InteractionUIController>();
    }
}