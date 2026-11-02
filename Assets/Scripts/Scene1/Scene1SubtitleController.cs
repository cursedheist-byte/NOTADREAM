using TMPro;
using UnityEngine;

public sealed class Scene1SubtitleController : MonoBehaviour
{
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private CanvasGroup canvasGroup;

    public CanvasGroup Group => canvasGroup;

    public void Show(string line)
    {
        if (subtitleText != null)
        {
            subtitleText.text = line;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }

    public void Clear()
    {
        if (subtitleText != null)
        {
            subtitleText.text = string.Empty;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
}