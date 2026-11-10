using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene 1's fade overlay is destroyed together with its scene, so the Scene 1 -> Scene 2
// handoff used to reveal gameplay exactly while the opening scene was being torn down.
// This cover is created in the last black frame, survives the unload, and only fades out
// after Scene 2 has rendered a few frames.
public sealed class SceneHandoffCover : MonoBehaviour
{
    private float alpha = 1f;
    private float fadeDuration = 0.45f;
    private bool revealing;

    public static SceneHandoffCover Show()
    {
        var host = new GameObject("SceneHandoffCover");
        DontDestroyOnLoad(host);
        return host.AddComponent<SceneHandoffCover>();
    }

    public void RevealWhenHandoffSettles(Scene openingScene)
    {
        if (revealing)
        {
            return;
        }

        revealing = true;
        StartCoroutine(Reveal(openingScene));
    }

    private IEnumerator Reveal(Scene openingScene)
    {
        // Timeout so a failed unload cannot leave the player staring at black.
        float deadline = Time.realtimeSinceStartup + 10f;
        while (openingScene.IsValid() && openingScene.isLoaded && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        for (int i = 0; i < 4; i++)
        {
            yield return null;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        alpha = 0f;
        Destroy(gameObject);
    }

    private void OnGUI()
    {
        if (alpha <= 0f)
        {
            return;
        }

        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
