using UnityEngine;

// Дополнительное действие при наведении на объект (R / колесо мыши).
// Базовый IInteractable отвечает только за клавишу E и одну строку подсказки;
// этот контракт позволяет радио переключать волну, не создавая в сцене второй
// raycast-детектор и не дублируя логику наведения.
public interface ISecondaryInteractable
{
    bool CanInteractSecondary(GameObject interactor);

    // direction: 0 — нажата R, +1 / -1 — прокрутка колеса вперёд / назад.
    void OnSecondaryInteract(GameObject interactor, float direction);
}
