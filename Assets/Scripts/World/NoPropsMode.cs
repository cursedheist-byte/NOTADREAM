using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Single switch for low-end machines. The lower presets only distance-cull vegetation, so
// the near field still submits millions of triangles and every instance stays in the scene.
// This mode deactivates every foliage root (the big Vegetation root plus the roadside tree,
// shrub, fern and rock roots) so those renderers leave camera culling, the shadow passes and
// draw submission entirely. Toggled from the graphics page of the pause menu.
public sealed class NoPropsMode : MonoBehaviour
{
    public const string PrefsKey = "Nyxo.NoProps";

    // World_Master keeps the bulk of the foliage inside "Vegetation", but the roadside trees,
    // shrubs and ferns were authored as their own scene roots. "f" is how the unnamed Oak
    // instances ended up in the scene file; it is still an Oak_Tree_Bark renderer.
    private static readonly string[] PropRootPrefixes =
    {
        "Vegetation", "Oak_Tree", "Pine_Tree", "PF_TreeIt", "Shrub", "PF_Fern", "PF_RedBush", "Rock_3"
    };

    private static NoPropsMode instance;
    private static bool active;
    private readonly List<GameObject> hiddenRoots = new List<GameObject>();

    public static bool Active => active;
    public static bool StoredValue => PlayerPrefs.GetInt(PrefsKey, 0) == 1;

    private static bool IsPropRoot(GameObject root)
    {
        string name = root.name;
        if (name == "f" || name.StartsWith("f (", System.StringComparison.Ordinal))
            return true;

        for (int i = 0; i < PropRootPrefixes.Length; i++)
        {
            if (name.StartsWith(PropRootPrefixes[i], System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        var host = new GameObject("NoPropsMode");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<NoPropsMode>();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        active = StoredValue;
        instance.Apply();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != null)
            instance.Apply();
    }

    public static void SetActive(bool value, bool persist = true)
    {
        active = value;
        if (persist)
        {
            PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (instance != null)
            instance.Apply();
    }

    private void Apply()
    {
        if (!active)
        {
            Restore();
            return;
        }

        VegetationCuller.SetPropsHidden(true);

        for (int i = hiddenRoots.Count - 1; i >= 0; i--)
        {
            if (hiddenRoots[i] == null)
                hiddenRoots.RemoveAt(i);
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var root = roots[r];
                if (root == null || hiddenRoots.Contains(root) || !IsPropRoot(root))
                    continue;

                if (root.activeSelf)
                    root.SetActive(false);
                hiddenRoots.Add(root);
            }
        }
    }

    private void Restore()
    {
        bool restored = false;
        for (int i = 0; i < hiddenRoots.Count; i++)
        {
            var root = hiddenRoots[i];
            if (root == null)
                continue;
            root.SetActive(true);
            restored = true;
        }

        hiddenRoots.Clear();
        VegetationCuller.NotifyPropsRestored();
        if (restored)
            Debug.Log("[NyxoProps] vegetation restored to the scene");
    }
}
