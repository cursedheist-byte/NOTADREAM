using UnityEngine;

public sealed class FpsCounter : MonoBehaviour
{
    private const string PrefsKey = "Nyxo.ShowFps";
    private static FpsCounter instance;
    private static bool visible;

    private int frames;
    private float elapsed;
    private int displayFps = -1;
    private GUIStyle labelStyle;
    private GUIStyle shadowStyle;

    public static bool StoredValue => PlayerPrefs.GetInt(PrefsKey, 0) == 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        visible = StoredValue;
        var host = new GameObject("FpsCounter");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<FpsCounter>();
        if (!visible)
            instance.enabled = false;
    }

    public static void SetVisible(bool show)
    {
        visible = show;
        PlayerPrefs.SetInt(PrefsKey, show ? 1 : 0);
        PlayerPrefs.Save();

        if (instance == null)
        {
            Bootstrap();
            return;
        }

        instance.enabled = show;
    }

    private void Update()
    {
        frames++;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < 0.25f)
            return;

        displayFps = Mathf.RoundToInt(frames / elapsed);
        frames = 0;
        elapsed = 0f;
    }

    private void OnGUI()
    {
        if (!visible || displayFps < 0)
            return;

        EnsureStyles();
        float offset = Mathf.Max(1f, labelStyle.fontSize / 12f);
        var baseRect = new Rect(10f, 9f, 220f, labelStyle.fontSize + 8f);
        string text = displayFps + " FPS";

        int previousDepth = GUI.depth;
        GUI.depth = -100;
        GUI.Label(new Rect(baseRect.x + offset, baseRect.y + offset, baseRect.width, baseRect.height), text, shadowStyle);
        GUI.Label(baseRect, text, labelStyle);
        GUI.depth = previousDepth;
    }

    private void EnsureStyles()
    {
        int fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 72f), 12, 20);
        if (labelStyle != null && labelStyle.fontSize == fontSize)
            return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(0, 0, 0, 0)
        };
        labelStyle.normal.textColor = Color.white;

        shadowStyle = new GUIStyle(labelStyle);
        shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
    }
}
