using UnityEngine;

public abstract class UIScreen : MonoBehaviour
{
    [SerializeField] protected CanvasGroup group;

    public virtual void Show()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) { Debug.LogError($"[UIScreen] У '{name}' нет компонента CanvasGroup — Show() не сработает.", this); return; }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    public virtual void Hide()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) { Debug.LogError($"[UIScreen] У '{name}' нет компонента CanvasGroup — Hide() не сработает.", this); return; }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
