using TMPro;
using UnityEngine;

public sealed class Scene1TitleCardController : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private CanvasGroup canvasGroup;

    public CanvasGroup Group => canvasGroup;

    private void Awake()
    {
        if (titleText != null)
        {
            titleText.text = "YESTERDAY";
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }
}