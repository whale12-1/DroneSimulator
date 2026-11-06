using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SliderReleaseNotifier : MonoBehaviour, IEndDragHandler, IPointerUpHandler
{
    public Action OnRelease;

    private const float MinInterval = 0.05f;

    private float lastReleaseTime = -1f;

    public void OnEndDrag(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    private void Release()
    {
        if (Time.unscaledTime - lastReleaseTime < MinInterval) return;

        lastReleaseTime = Time.unscaledTime;
        if (OnRelease != null) OnRelease();
    }
}
