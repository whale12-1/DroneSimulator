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
        if (chairCollider == null) chairCollider = GetComponent<Collider>();
    }

    public string GetInteractionPrompt() => isSitting ? "Встать [Пробел]" : "Сесть [E]";
    public bool CanInteract(GameObject interactor) => true;

    public void Interact(GameObject interactor)
    {
        if (isSitting) StandUp();
        else SitDown(interactor);
    }

    public void Interact(GameObject interactor, bool forceSitDown)
    {
        if (forceSitDown)
        {
            if (!isSitting) SitDown(interactor);
        }
        else
        {
            Interact(interactor);
        }
    }

    private void Update()
    {
        if (!isSitting) return;
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            StandUp();
    }

    private void SitDown(GameObject interactor)
    {
        Debug.Log($"[ChairInteractable] SitDown() called: isSitting = {isSitting}, time = {Time.unscaledTime:F3}");
        ResolvePlayer(interactor);
        if (playerController == null || sitPosition == null) return;

        isSitting = true;
        playerController.isSitting = true;
        playerController.SetSitting(true);

        if (characterController != null) characterController.enabled = false;
        if (chairCollider != null) chairCollider.enabled = false;

        Transform player = playerController.transform;
        player.position = sitPosition.position;
        player.rotation = sitPosition.rotation;

        if (uiController != null)
        {
            uiController.HideHint();
            uiController.ShowSittingHint("Встать [Пробел]");
        }

        Debug.Log($"[ChairInteractable] SitDown() done: player position = {player.position}, isSitting = {isSitting}");
    }

    private void StandUp()
    {
        Debug.Log($"[ChairInteractable] StandUp() called: isSitting = {isSitting}, time = {Time.unscaledTime:F3}");
        if (playerController == null) return;

        Debug.Log($"[ChairInteractable] StandUp(): player position before = {playerController.transform.position}");

        if (characterController != null) characterController.enabled = false;
        if (standPosition != null)
        {
            Transform player = playerController.transform;
            player.position = standPosition.position;
            player.rotation = standPosition.rotation;
        }
        if (chairCollider != null) chairCollider.enabled = true;
        if (characterController != null) characterController.enabled = true;

        isSitting = false;
        playerController.isSitting = false;
        playerController.SetSitting(false);

        if (uiController != null) uiController.HideSittingHint();

        Debug.Log($"[ChairInteractable] StandUp() done: player position = {playerController.transform.position}, isSitting = {isSitting}");
    }

    private void ResolvePlayer(GameObject interactor)
    {
        if (playerController == null && interactor != null) playerController = interactor.GetComponent<PlayerController>();
        if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
        if (characterController == null && playerController != null) characterController = playerController.GetComponent<CharacterController>();
        if (uiController == null) uiController = FindAnyObjectByType<InteractionUIController>();
    }
}
