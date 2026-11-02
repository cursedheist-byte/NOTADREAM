using UnityEngine;
using UnityEngine.SceneManagement;

// Applies one render distance to every camera in the game. Geometry farther than this is
// never submitted, which is the cheapest way to raise FPS without touching textures, lights,
// shadows or post-processing quality.
public sealed class RenderDistanceSettings : MonoBehaviour
{
    private const string PrefsKey = "Nyxo.RenderDistance";
    private const int DefaultIndex = 2;

    public static readonly int[] Distances = { 150, 250, 400, 700, 1000 };

    private static RenderDistanceSettings instance;
    private static int currentIndex = -1;
    private static Camera[] cameraBuffer = new Camera[32];
    private float nextApplyTime;

    public static int StoredIndex
    {
        get
        {
            if (currentIndex < 0)
            {
                int saved = PlayerPrefs.GetInt(PrefsKey, Distances[DefaultIndex]);
                int best = 0;
                for (int i = 1; i < Distances.Length; i++)
                {
                    if (Mathf.Abs(Distances[i] - saved) < Mathf.Abs(Distances[best] - saved))
                    {
                        best = i;
                    }
                }

                currentIndex = best;
            }

            return currentIndex;
        }
    }

    public static int Distance => Distances[StoredIndex];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        var host = new GameObject("RenderDistanceSettings");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<RenderDistanceSettings>();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyToAllCameras();
    }

    public static void SetIndex(int index)
    {
        int wrapped = ((index % Distances.Length) + Distances.Length) % Distances.Length;
        currentIndex = wrapped;
        PlayerPrefs.SetInt(PrefsKey, Distances[wrapped]);
        PlayerPrefs.Save();
        ApplyToAllCameras();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextApplyTime)
        {
            return;
        }

        // Cameras can be enabled or created mid-scene (cutscenes, coma ending, endings), so the
        // distance is re-applied instead of only on scene load.
        nextApplyTime = Time.unscaledTime + 1f;
        ApplyToAllCameras();
    }

    private static void ApplyToAllCameras()
    {
        float distance = Distance;
        int count = Camera.GetAllCameras(cameraBuffer);
        if (count >= cameraBuffer.Length)
        {
            cameraBuffer = new Camera[cameraBuffer.Length * 2];
            count = Camera.GetAllCameras(cameraBuffer);
        }

        for (int i = 0; i < count; i++)
        {
            var camera = cameraBuffer[i];
            if (camera != null && !Mathf.Approximately(camera.farClipPlane, distance))
            {
                camera.farClipPlane = distance;
            }
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToAllCameras();
    }
}
