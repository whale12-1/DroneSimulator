using System.Collections;
using TMPro;
using UnityEngine;

// Затемнение экрана с текстом: титры глав обучения и сон в бункере.
// Вся анимация идёт на unscaled-времени, поэтому переход не зависает при
// Time.timeScale = 0 и не ускоряется от перемоток времени.
public class ScreenFader : MonoBehaviour
{
    [Header("Ссылки UI")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private TMP_Text fadeText;

    [Header("Настройки")]
    [SerializeField] private float fadeSpeed = 1.5f;
    [SerializeField] private float holdSeconds = 2f;

    private void Awake()
    {
        if (fadeGroup == null) fadeGroup = GetComponent<CanvasGroup>();
        if (fadeGroup == null) return;

        // На старте экран прозрачный.
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
    }

    // Обновляет текст на уже затемнённом экране без перезапуска корутины:
    // сон в бункере перематывает часы каждую игровую минуту.
    public void SetMessage(string text)
    {
        if (fadeText != null) fadeText.text = text;
    }

    // holdOverride < 0 — используется holdSeconds из инспектора;
    // 0 — управление возвращается сразу после полного затемнения.
    public IEnumerator FadeOut(string message, float holdOverride = -1f)
    {
        if (fadeGroup == null) yield break;

        fadeGroup.blocksRaycasts = true;
        SetMessage(message);

        while (fadeGroup.alpha < 1f)
        {
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, 1f, Time.unscaledDeltaTime * fadeSpeed);
            yield return null;
        }

        fadeGroup.alpha = 1f;

        float hold = holdOverride >= 0f ? holdOverride : Mathf.Max(0f, holdSeconds);
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
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