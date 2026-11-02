using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The house prefab carries eleven soft-shadow point lights and the world instances it eight
// times. Point-light shadows are six shadow faces each, but the duplicated houses are scenery
// far from the player: turning those shadows off early removes most of that cost without
// changing anything visible. Lights near the player keep their authored shadows.
public sealed class LightShadowDistanceCuller : MonoBehaviour
{
    private const float ShadowCutoffDistance = 45f;
    private const float ApplyInterval = 0.5f;
    private const float PostLoadRescanDelay = 0.75f;

    private static LightShadowDistanceCuller instance;

    private readonly Dictionary<Light, LightShadows> originalShadows = new Dictionary<Light, LightShadows>();
    private Light[] trackedLights = new Light[0];
    private bool rescanPending = true;
    private float nextRescanTime;
    private float nextApplyTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        var host = new GameObject("LightShadowDistanceCuller");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<LightShadowDistanceCuller>();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != null)
        {
            instance.rescanPending = true;
            instance.nextRescanTime = Time.unscaledTime + PostLoadRescanDelay;
        }
    }

    private void Update()
    {
        if (rescanPending && Time.unscaledTime >= nextRescanTime)
        {
            // The scene's lights are authored, so one scan after load is enough; a periodic
            // whole-scene FindObjectsByType was only causing a hitch every few seconds.
            rescanPending = false;
            Rescan();
        }

        if (Time.unscaledTime >= nextApplyTime)
        {
            Apply();
            nextApplyTime = Time.unscaledTime + ApplyInterval;
        }
    }

    private void Rescan()
    {
        var lights = FindObjectsByType<Light>(FindObjectsInactive.Include);
        var shadowCasting = new List<Light>(originalShadows.Count);
        for (int i = 0; i < lights.Length; i++)
        {
            var light = lights[i];
            if (light == null || light.type == LightType.Directional)
            {
                continue;
            }

            if (!originalShadows.ContainsKey(light))
            {
                originalShadows[light] = light.shadows;
            }

            if (originalShadows[light] != LightShadows.None)
            {
                shadowCasting.Add(light);
            }
        }

        trackedLights = shadowCasting.ToArray();

        var stale = new List<Light>();
        foreach (var pair in originalShadows)
        {
            if (pair.Key == null)
            {
                stale.Add(pair.Key);
            }
        }

        for (int i = 0; i < stale.Count; i++)
        {
            originalShadows.Remove(stale[i]);
        }
    }

    private void Apply()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        float cutoffSqr = ShadowCutoffDistance * ShadowCutoffDistance;
        Vector3 cameraPosition = camera.transform.position;
        for (int i = 0; i < trackedLights.Length; i++)
        {
            var light = trackedLights[i];
            if (light == null || !originalShadows.TryGetValue(light, out LightShadows original))
            {
                continue;
            }

            bool nearPlayer = (light.transform.position - cameraPosition).sqrMagnitude <= cutoffSqr;
            LightShadows target = nearPlayer ? original : LightShadows.None;
            if (light.shadows != target)
            {
                light.shadows = target;
            }
        }
    }
}
