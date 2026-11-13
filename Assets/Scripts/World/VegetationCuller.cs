using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// World_Master's Vegetation root holds ~21,400 authored instances (grass, ferns, trees).
// Measured in Scene 2: capping vegetation shadow casting at 45 m removed 82M of the
// 108M triangles submitted per frame and cut GPU time by 26% without a visible change.
// Distant grass is still drawn; only its shadow contribution is dropped, so nothing
// disappears from view. Work is time-sliced so no frame takes the whole 21k-renderer
// sweep at once.
public sealed class VegetationCuller : MonoBehaviour
{
    private const string VegetationRootName = "Vegetation";
    private const float ShadowCutoff = 45f;
    private const float Hysteresis = 6f;
    private const float RescanDelay = 0.75f;
    private const int FrameBudget = 2500;

    private static VegetationCuller instance;
    private static bool suppressed;
    // Rescans run on every additive scene load, and a culled renderer would otherwise
    // report ShadowCastingMode.Off as its authored mode and never cast again.
    private static readonly System.Collections.Generic.Dictionary<Renderer, ShadowCastingMode> authoredShadowModes =
        new System.Collections.Generic.Dictionary<Renderer, ShadowCastingMode>();
    // Distance culling disables renderers; a rescan must not read that back as "authored off".
    private static readonly System.Collections.Generic.Dictionary<Renderer, bool> authoredEnabled =
        new System.Collections.Generic.Dictionary<Renderer, bool>();

    private Renderer[] renderers = new Renderer[0];
    private ShadowCastingMode[] baseShadowModes = new ShadowCastingMode[0];
    private Vector3[] centers = new Vector3[0];
    private bool[] managed = new bool[0];
    private bool[] renderShadow = new bool[0];
    private bool[] renderVisible = new bool[0];
    private int cursor;
    private bool rescanPending;
    private float nextRescanTime;
    private Camera cachedCamera;
    private float nextCameraLookup;
    private Vector3 cycleOrigin;
    private bool hasSwept;
    private bool forceSweep;
    private GraphicsPreset lastPreset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        var host = new GameObject("VegetationCuller");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<VegetationCuller>();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance == null)
        {
            return;
        }

        instance.rescanPending = true;
        instance.nextRescanTime = Time.unscaledTime + RescanDelay;
        instance.cachedCamera = null;
    }

    // NoPropsMode reactivates the root outside a scene load; the cached arrays are empty by
    // then, so the distance culling has to re-acquire its renderers immediately.
    public static void NotifyPropsRestored()
    {
        suppressed = false;
        if (instance == null)
        {
            return;
        }

        instance.rescanPending = true;
        instance.nextRescanTime = Time.unscaledTime;
        instance.cachedCamera = null;
        instance.forceSweep = true;
    }

    // With the whole root deactivated there is nothing left to cull, and sweeping 21k cached
    // renderers every frame would keep burning main-thread time for no visual result.
    public static void SetPropsHidden(bool hidden)
    {
        suppressed = hidden;
        if (instance == null || !hidden)
        {
            return;
        }

        instance.renderers = new Renderer[0];
        instance.baseShadowModes = new ShadowCastingMode[0];
        instance.centers = new Vector3[0];
        instance.managed = new bool[0];
        instance.renderShadow = new bool[0];
        instance.renderVisible = new bool[0];
        instance.cursor = 0;
        instance.rescanPending = false;
        instance.cachedCamera = null;
    }

    private void Update()
    {
        if (suppressed)
        {
            return;
        }

        if (rescanPending && Time.unscaledTime >= nextRescanTime)
        {
            rescanPending = false;
            Rescan();
        }

        if (renderers.Length == 0)
        {
            return;
        }

        var camera = ResolveCamera();
        if (camera == null)
        {
            return;
        }

        Vector3 cameraPosition = camera.transform.position;

        // A distance sweep can only change what it does when the camera moves, and the sweep
        // itself costs around 1.5 ms per frame with 21k renderers. Whole cycles are skipped
        // while the camera is effectively still; leaving that state re-evaluates immediately.
        if (cursor == 0)
        {
            if (lastPreset != GraphicsPresetController.Current)
            {
                lastPreset = GraphicsPresetController.Current;
                forceSweep = true;
            }

            if (hasSwept && !forceSweep && (cameraPosition - cycleOrigin).sqrMagnitude < 4f)
            {
                return;
            }

            cycleOrigin = cameraPosition;
            hasSwept = true;
            forceSweep = false;
        }

        int budget = Mathf.Min(FrameBudget, renderers.Length);
        for (int step = 0; step < budget; step++)
        {
            int index = cursor;
            cursor++;
            if (cursor >= renderers.Length)
            {
                cursor = 0;
            }

            var renderer = renderers[index];
            if (renderer == null || !managed[index])
            {
                continue;
            }

            // Vegetation is static, so the cached bounds center stays valid and the sweep
            // never has to touch renderer.bounds, which is costly with GPU Resident Drawer.
            float sqrDistance = (centers[index] - cameraPosition).sqrMagnitude;

            bool casts = renderShadow[index];
            float shadowLimit = casts ? ShadowCutoff + Hysteresis : ShadowCutoff;
            bool shouldCast = sqrDistance <= shadowLimit * shadowLimit;
            if (shouldCast != casts)
            {
                renderShadow[index] = shouldCast;
                renderer.shadowCastingMode = shouldCast ? baseShadowModes[index] : ShadowCastingMode.Off;
            }

            float visibleLimit = GraphicsPresetController.MaxVegetationDistance;
            bool visible = renderVisible[index];
            float renderLimit = visible ? visibleLimit + Hysteresis : visibleLimit;
            bool shouldRender = sqrDistance <= renderLimit * renderLimit;
            if (shouldRender != visible)
            {
                renderVisible[index] = shouldRender;
                renderer.enabled = shouldRender;
            }
        }
    }

    private Camera ResolveCamera()
    {
        if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
        {
            return cachedCamera;
        }

        if (Time.unscaledTime < nextCameraLookup && cachedCamera != null)
        {
            return cachedCamera;
        }

        nextCameraLookup = Time.unscaledTime + 1f;
        cachedCamera = Camera.main;
        return cachedCamera;
    }

    private void Rescan()
    {
        Transform root = null;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r] != null && roots[r].name == VegetationRootName)
                {
                    root = roots[r].transform;
                    break;
                }
            }

            if (root != null)
            {
                break;
            }
        }

        if (root == null)
        {
            renderers = new Renderer[0];
            baseShadowModes = new ShadowCastingMode[0];
            centers = new Vector3[0];
            managed = new bool[0];
            renderShadow = new bool[0];
            renderVisible = new bool[0];
            cursor = 0;
            return;
        }

        var found = root.GetComponentsInChildren<Renderer>(false);
        var newRenderers = new Renderer[found.Length];
        var newBaseModes = new ShadowCastingMode[found.Length];
        var newCenters = new Vector3[found.Length];
        var newManaged = new bool[found.Length];
        var newShadow = new bool[found.Length];
        var newVisible = new bool[found.Length];
        int write = 0;
        for (int i = 0; i < found.Length; i++)
        {
            var renderer = found[i];
            if (renderer == null)
            {
                continue;
            }

            newRenderers[write] = renderer;
            if (!authoredShadowModes.TryGetValue(renderer, out var authoredMode))
            {
                authoredMode = renderer.shadowCastingMode;
                authoredShadowModes.Add(renderer, authoredMode);
            }

            if (!authoredEnabled.TryGetValue(renderer, out bool authoredEnabledState))
            {
                authoredEnabledState = renderer.enabled;
                authoredEnabled.Add(renderer, authoredEnabledState);
            }

            newBaseModes[write] = authoredMode;
            newCenters[write] = renderer.bounds.center;
            newManaged[write] = authoredEnabledState;
            newShadow[write] = authoredMode != ShadowCastingMode.Off;
            newVisible[write] = renderer.enabled;
            write++;
        }

        if (write < newRenderers.Length)
        {
            System.Array.Resize(ref newRenderers, write);
            System.Array.Resize(ref newBaseModes, write);
            System.Array.Resize(ref newCenters, write);
            System.Array.Resize(ref newManaged, write);
            System.Array.Resize(ref newShadow, write);
            System.Array.Resize(ref newVisible, write);
        }

        renderers = newRenderers;
        baseShadowModes = newBaseModes;
        centers = newCenters;
        managed = newManaged;
        renderShadow = newShadow;
        renderVisible = newVisible;
        cursor = 0;
        forceSweep = true;
    }
}
