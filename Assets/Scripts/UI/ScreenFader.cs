using UnityEngine;
using System.Collections;
using TMPro;

public class ScreenFader : MonoBehaviour
{
    [Header("Элементы UI")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private TMP_Text fadeText;

    [Header("Настройки")]
    [SerializeField] private float fadeSpeed = 1.5f;

    private void Awake()
    {
        // На старте экран прозрачный
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
    }

    public IEnumerator FadeOut(string message)
    {
        fadeGroup.blocksRaycasts = true; // Блокируем клики, пока идет загрузка

        if (fadeText != null)
            fadeText.text = message;

        // Плавно затемняем
        while (fadeGroup.alpha < 1f)
        {
            fadeGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        // Пауза, чтобы игрок прочитал заголовок главы
        yield return new WaitForSeconds(2f);
    }

    public IEnumerator FadeIn()
    {
        // Плавно убираем затемнение
        while (fadeGroup.alpha > 0f)
        {
            fadeGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        fadeGroup.blocksRaycasts = false;
    }
}