using UnityEngine;

public static class WorldPromptUI
{
    private static Texture2D circleTexture;
    private static Texture2D roundedTexture;
    private static GUIStyle keyStyle;
    private static GUIStyle slashStyle;

    public static void DrawInteractionHint()
    {
        EnsureStyles();

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f + 34f;
        Rect keyCircle = new Rect(centerX - 59f, centerY - 20f, 40f, 40f);
        Rect slash = new Rect(centerX - 12f, centerY - 19f, 20f, 38f);
        Rect mouse = new Rect(centerX + 13f, centerY - 21f, 30f, 42f);

        DrawCircle(keyCircle, new Color(0.04f, 0.09f, 0.15f, 0.94f));
        GUI.Label(keyCircle, WorldMasterKeyBindings.GetKey("Interact").ToString(), keyStyle);
        GUI.Label(slash, "/", slashStyle);
        DrawMouse(mouse);
    }

    private static void DrawCircle(Rect rect, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, circleTexture);
        GUI.color = oldColor;
    }

    private static void DrawMouse(Rect rect)
    {
        Color oldColor = GUI.color;
        Rect outline = new Rect(rect.x, rect.y, rect.width, rect.height);
        Rect body = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
        GUI.color = new Color(0.85f, 0.91f, 0.97f, 1f);
        GUI.DrawTexture(outline, roundedTexture);
        GUI.color = new Color(0.055f, 0.085f, 0.12f, 1f);
        GUI.DrawTexture(body, roundedTexture);

        Rect leftClick = new Rect(body.x + 2f, body.y + 2f, body.width * 0.43f, body.height * 0.43f);
        GUI.color = new Color(0.25f, 0.72f, 1f, 1f);
        GUI.DrawTexture(leftClick, roundedTexture);
        GUI.color = new Color(0.85f, 0.91f, 0.97f, 1f);
        GUI.DrawTexture(new Rect(body.center.x - 0.5f, body.y + 2f, 1f, body.height * 0.43f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(body.center.x - 1f, body.y + 7f, 2f, 5f), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private static void EnsureStyles()
    {
        if (circleTexture == null)
        {
            circleTexture = CreateShapeTexture(64, true);
            roundedTexture = CreateShapeTexture(64, false);
        }

        if (keyStyle != null)
            return;

        keyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
            fontStyle = FontStyle.Bold
        };
        keyStyle.normal.textColor = Color.white;

        slashStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 25,
            fontStyle = FontStyle.Normal
        };
        slashStyle.normal.textColor = Color.white;
    }

    private static Texture2D CreateShapeTexture(int size, bool circle)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = circle ? "World Prompt Circle" : "World Prompt Rounded Shape",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        float radius = circle ? size * 0.5f : size * 0.22f;
        float center = (size - 1f) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - center);
                float dy = Mathf.Abs(y - center);
                float distance;

                if (circle)
                {
                    distance = Mathf.Sqrt(dx * dx + dy * dy);
                }
                else
                {
                    float cornerX = Mathf.Max(dx - (center - radius), 0f);
                    float cornerY = Mathf.Max(dy - (center - radius), 0f);
                    distance = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);
                }

                byte alpha = distance <= radius ? (byte)255 : (byte)0;
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
