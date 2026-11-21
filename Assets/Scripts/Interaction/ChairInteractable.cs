using UnityEngine;
using UnityEngine.InputSystem;

public class ChairInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform sitPosition;
    [SerializeField] private Transform standPosition;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Collider chairCollider;
    [SerializeField] private InteractionUIController uiController;
    [SerializeField] private float maxInteractDistance = 0.8f;
    // Рабочий стол компьютера: пока он открыт, Пробел не должен поднимать игрока со стула.
    [SerializeField] private ComputerUIController computerUI;

    private CharacterController characterController;
    private bool isSitting;

    private void Awake()
    {
        if (chairCollider == null) chairCollider = GetComponent<Collider>();

        // ComputerOS в сцене выключен (m_IsActive: 0), поэтому без Include ссылка не найдётся.
        if (computerUI == null)
            computerUI = FindAnyObjectByType<ComputerUIController>(FindObjectsInactive.Include);
    }

    public string GetInteractionPrompt() => isSitting ? "Встать [Пробел]" : "Сесть [E]";

    public bool CanInteract(GameObject interactor)
    {
        if (isSitting) return true;
        if (interactor == null) return false;

        if (chairCollider == null) chairCollider = GetComponent<Collider>();

        Vector3 playerPosition = interactor.transform.position;
        Vector3 nearestPoint = chairCollider != null
            ? chairCollider.ClosestPoint(playerPosition)
            : transform.position;

        // Стул ниже уровня глаз, поэтому сравниваем только горизонтальную дистанцию:
        // 3D-расстояние от корня капсулы искажалось бы из-за высоты камеры.
        Vector2 offset = new Vector2(
            playerPosition.x - nearestPoint.x,
            playerPosition.z - nearestPoint.z);

        return offset.sqrMagnitude <= maxInteractDistance * maxInteractDistance;
    }

    // Встать нельзя, пока открыт рабочий стол компьютера: иначе игрок поднимается
    // со стула, а окно ОС остаётся открытым (Time.timeScale при этом уже 0).
    public bool IsStandUpBlocked => isSitting && computerUI != null && computerUI.IsOpen;

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
        if (IsStandUpBlocked) return; // открыт рабочий стол — Пробел игнорируется
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

        if (characterController != null) characterController.enabled = false;
        if (chairCollider != null) chairCollider.enabled = false;

        Transform player = playerController.transform;
        player.position = sitPosition.position;
        player.rotation = sitPosition.rotation;

        // Переход SitDown не проигрываем: сразу конечная поза SitIdle в той же позе.
        playerController.ForceSitIdle();

        // Процедурный SFX через пул EffectService (шина SFX) — настройка "Эффекты"
        // влияет и на взаимодействия в бункере.
        EffectService.PlaySound(ProceduralClips.Thud(), transform.position, 0.7f);

        if (uiController != null)
        {
            uiController.HideHint();
            uiController.ShowSittingHint("Встать [Пробел]");
        }

        Debug.Log($"[ChairInteractable] SitDown() done: player position = {player.position}, isSitting = {isSitting}");
    }

    public void StandUp()
    {
        if (!isSitting) return;

        if (IsStandUpBlocked)
        {
            Debug.Log("[ChairInteractable] StandUp() ignored: ComputerOS is open.");
            return;
        }

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
        // Мгновенно прерываем анимацию посадки/сидения: иначе клип SitDown доигрывает
        // до перехода в SitIdle, и анимация «накладывается» на уже вставшего игрока.
        playerController.ForceIdle();

        EffectService.PlaySound(ProceduralClips.Thud(), transform.position, 0.6f);

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
