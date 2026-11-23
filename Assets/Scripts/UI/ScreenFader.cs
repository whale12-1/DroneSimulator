using UnityEngine;
using System.Collections;
using TMPro;

public class ScreenFader : MonoBehaviour
{
    [Header("�������� UI")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private TMP_Text fadeText;

    [Header("���������")]
    [SerializeField] private float fadeSpeed = 1.5f;
    [SerializeField] private float holdSeconds = 2f;

    private void Awake()
    {
        // �� ������ ����� ����������
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
    }

    public IEnumerator FadeOut(string message)
    {
        if (fadeGroup == null) yield break;

        fadeGroup.blocksRaycasts = true;

        if (fadeText != null)
            fadeText.text = message;

        // Unscaled: переход между главами не должен зависать при Time.timeScale == 0.
        while (fadeGroup.alpha < 1f)
        {
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, 1f, Time.unscaledDeltaTime * fadeSpeed);
            yield return null;
        }

        fadeGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdSeconds));
    }

    public IEnumerator FadeIn()
    {
        if (fadeGroup == null) yield break;

        while (fadeGroup.alpha > 0f)
        {
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, 0f, Time.unscaledDeltaTime * fadeSpeed);
            yield return null;
        }

        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
    }
}