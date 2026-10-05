using UnityEngine;

public abstract class UIScreen : MonoBehaviour
{
    [SerializeField] protected CanvasGroup group;

    public virtual void Show()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) { Debug.LogWarning($"[UIScreen] {name}: нет CanvasGroup", this); return; }

        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    public virtual void Hide()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) return;

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}