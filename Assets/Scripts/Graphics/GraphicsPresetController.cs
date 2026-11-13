using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public enum GraphicsPreset
{
    Potato = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Max = 4
}

// Single source of truth for the graphics presets. The menu only reports a choice; every
// render and quality setting is derived here so the presets keep working across scene loads
// and cannot be silently overridden by a scene manager (Scene 1's blur and Scene 2's liminal
// volume both switch camera post-processing back on when they start).
public sealed class GraphicsPresetController : MonoBehaviour
{
    public const string PrefsKey = "Nyxo.GraphicsPreset";

    private const int OverrideVolumePriority = 1000;

    private static GraphicsPresetController instance;
    private static GraphicsPreset current = GraphicsPreset.High;
    private static Camera[] cameraBuffer = new Camera[32];
    private static int forcedRenderingMode = -1;

    private Volume overrideVolume;
    private VolumeProfile overrideProfile;

    public static GraphicsPreset Current => current;

    // Lower presets also shorten the draw distance. Distant geometry is the cheapest thing to
    // remove and it is what makes the open jungle scenes expensive on old GPUs.
    public static float MaxRenderDistance
    {
        get
        {
            switch (current)
            {
                case GraphicsPreset.Potato: return 150f;
                case GraphicsPreset.Low: return 250f;
                case GraphicsPreset.Medium: return 400f;
                default: return float.MaxValue;
            }
        }
    }

    // Vegetation is the single biggest cost in the world (~21k renderers under one root, and
    // hiding it takes Potato from ~110 FPS to ~215 FPS on a 5070). The lower presets stop
    // drawing it past this distance while still drawing the village itself.
    public static float MaxVegetationDistance
    {
        get
        {
            switch (current)
            {
                case GraphicsPreset.Potato: return 65f;
                case GraphicsPreset.Low: return 110f;
                case GraphicsPreset.Medium: return 170f;
                default: return float.MaxValue;
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        var host = new GameObject("GraphicsPresetController");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<GraphicsPresetController>();
        instance.CreateOverrideVolume();
        ParseModeOverride();

        GraphicsPreset preset;
        if (!TryParseCommandLine(out preset))
            preset = (GraphicsPreset)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, (int)GraphicsPreset.High), 0, 4);

        current = preset;
        instance.ApplyInternal(preset);
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
        instance.StartCoroutine(instance.EnforceCameraSettings());
    }

    public static void Apply(GraphicsPreset preset)
    {
        current = preset;
        PlayerPrefs.SetInt(PrefsKey, (int)preset);
        PlayerPrefs.Save();

        if (instance != null)
            instance.ApplyInternal(preset);
    }

    public static string DisplayName(GraphicsPreset preset)
    {
        switch (preset)
        {
            case GraphicsPreset.Potato:
                return "Potato  •  minimum: 45% render / 150 m draw / 65 m foliage / no shadows";
            case GraphicsPreset.Low:
                return "Low  •  light: 60% render / 250 m draw / 110 m foliage / hard shadows";
            case GraphicsPreset.Medium:
                return "Medium  •  balanced: 80% render / 400 m draw / 170 m foliage / 1K shadows";
            case GraphicsPreset.High:
                return "High  •  current look: 150% render / 4x MSAA / 4K shadows / post FX";
            default:
                return "MAX  •  enhanced: 175% render / ultra shadows / SSAO / cinematic grade";
        }
    }

    private static bool TryParseCommandLine(out GraphicsPreset preset)
    {
        preset = GraphicsPreset.High;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "-nyxoQuality", StringComparison.OrdinalIgnoreCase))
                continue;

            switch (args[i + 1].ToLowerInvariant())
            {
                case "potato": preset = GraphicsPreset.Potato; return true;
                case "low": preset = GraphicsPreset.Low; return true;
                case "medium": preset = GraphicsPreset.Medium; return true;
                case "high": preset = GraphicsPreset.High; return true;
                case "max": preset = GraphicsPreset.Max; return true;
            }
        }

        return false;
    }

    private static void ParseModeOverride()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "-nyxoMode", StringComparison.OrdinalIgnoreCase))
                continue;

            switch (args[i + 1].ToLowerInvariant())
            {
                case "forward": forcedRenderingMode = (int)RenderingMode.Forward; break;
                case "plus": forcedRenderingMode = (int)RenderingMode.ForwardPlus; break;
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyInternal(current);
    }

    private IEnumerator EnforceCameraSettings()
    {
        var wait = new WaitForSecondsRealtime(1f);
        while (true)
        {
            ApplyCameraSettings(current);
            yield return wait;
        }
    }

    private void ApplyInternal(GraphicsPreset preset)
    {
        int pcLevel = FindQualityLevel("PC");
        if (pcLevel >= 0 && QualitySettings.GetQualityLevel() != pcLevel)
            QualitySettings.SetQualityLevel(pcLevel, false);

        ApplyQualitySettings(preset);

        UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null)
        {
            ApplyPipelineSettings(pipeline, preset);
            ApplyRendererSettings(pipeline, preset);
        }

        ApplyVolumeOverrides(preset);
        ApplyCameraSettings(preset);

        Debug.Log("[NyxoGraphics] preset=" + preset + " renderScale=" + (pipeline != null ? pipeline.renderScale : -1f)
            + " msaa=" + (pipeline != null ? pipeline.msaaSampleCount : -1)
            + " renderer=" + DescribeRenderer(pipeline)
            + " postFX=" + UsesPostProcessing(preset) + " shadows=" + QualitySettings.shadows
            + " draw=" + MaxRenderDistance + " gpu=" + SystemInfo.graphicsDeviceName
            + " api=" + SystemInfo.graphicsDeviceType);
    }

    private static string DescribeRenderer(UniversalRenderPipelineAsset pipeline)
    {
        if (pipeline == null || pipeline.rendererDataList.Length == 0)
            return "none";

        var data = pipeline.rendererDataList[0] as UniversalRendererData;
        return data != null ? data.renderingMode.ToString() : "none";
    }

    private static bool UsesPostProcessing(GraphicsPreset preset)
    {
        // Every preset keeps post-processing on so Scene 1's authored vision blur is present
        // everywhere; the lower presets simply switch off every other effect in the override
        // volume below.
        return true;
    }

    private static int FindQualityLevel(string name)
    {
        string[] names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static void ApplyQualitySettings(GraphicsPreset preset)
    {
        bool usesShadows = preset > GraphicsPreset.Potato;
        int msaa = preset >= GraphicsPreset.High ? 4 : 0;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        QualitySettings.antiAliasing = msaa;
        QualitySettings.shadows = !usesShadows
            ? UnityEngine.ShadowQuality.Disable
            : preset >= GraphicsPreset.Medium
                ? UnityEngine.ShadowQuality.All
                : UnityEngine.ShadowQuality.HardOnly;
        QualitySettings.shadowResolution = preset >= GraphicsPreset.High
            ? UnityEngine.ShadowResolution.VeryHigh
            : preset == GraphicsPreset.Medium
                ? UnityEngine.ShadowResolution.Medium
                : UnityEngine.ShadowResolution.Low;
        QualitySettings.shadowProjection = preset == GraphicsPreset.Max
            ? UnityEngine.ShadowProjection.CloseFit
            : UnityEngine.ShadowProjection.StableFit;
        QualitySettings.shadowNearPlaneOffset = preset == GraphicsPreset.Max ? 1f : 3f;
        QualitySettings.shadowDistance = preset == GraphicsPreset.Max ? 150f
            : preset == GraphicsPreset.High ? 120f
            : preset == GraphicsPreset.Medium ? 40f
            : preset == GraphicsPreset.Low ? 20f : 0f;
        QualitySettings.shadowCascades = preset >= GraphicsPreset.High ? 4
            : preset == GraphicsPreset.Medium ? 2 : 1;

        QualitySettings.lodBias = preset == GraphicsPreset.Max ? 4f
            : preset == GraphicsPreset.High ? 3f
            : preset == GraphicsPreset.Medium ? 1.5f
            : preset == GraphicsPreset.Low ? 0.8f : 0.4f;
        QualitySettings.maximumLODLevel = preset == GraphicsPreset.Potato ? 1 : 0;
        QualitySettings.globalTextureMipmapLimit = preset == GraphicsPreset.Potato ? 2
            : preset == GraphicsPreset.Low ? 1 : 0;
        QualitySettings.anisotropicFiltering = preset == GraphicsPreset.Max
            ? AnisotropicFiltering.ForceEnable
            : preset >= GraphicsPreset.Medium
                ? AnisotropicFiltering.Enable
                : AnisotropicFiltering.Disable;

        QualitySettings.pixelLightCount = preset == GraphicsPreset.Max ? 8
            : preset == GraphicsPreset.High ? 4
            : preset == GraphicsPreset.Medium ? 3
            : preset == GraphicsPreset.Low ? 1 : 0;
        QualitySettings.particleRaycastBudget = preset == GraphicsPreset.Max ? 2048
            : preset == GraphicsPreset.High ? 256
            : preset == GraphicsPreset.Medium ? 128
            : preset == GraphicsPreset.Low ? 32 : 8;
        QualitySettings.softParticles = preset >= GraphicsPreset.High;
        QualitySettings.realtimeReflectionProbes = preset >= GraphicsPreset.High;
        QualitySettings.billboardsFaceCameraPosition = preset == GraphicsPreset.Max;
        QualitySettings.skinWeights = preset == GraphicsPreset.Potato
            ? SkinWeights.OneBone
            : preset == GraphicsPreset.Low
                ? SkinWeights.TwoBones
                : SkinWeights.FourBones;
    }

    private static void ApplyPipelineSettings(UniversalRenderPipelineAsset pipeline, GraphicsPreset preset)
    {
        bool highLook = preset >= GraphicsPreset.High;

        pipeline.renderScale = preset == GraphicsPreset.Max ? 1.75f
            : preset == GraphicsPreset.High ? 1.5f
            : preset == GraphicsPreset.Medium ? 0.8f
            : preset == GraphicsPreset.Low ? 0.6f : 0.45f;
        pipeline.msaaSampleCount = highLook ? 4 : 1;
        pipeline.supportsHDR = preset >= GraphicsPreset.Medium;
        pipeline.supportsCameraDepthTexture = true;
        pipeline.supportsCameraOpaqueTexture = false;

        pipeline.shadowDistance = preset == GraphicsPreset.Max ? 150f
            : preset == GraphicsPreset.High ? 120f
            : preset == GraphicsPreset.Medium ? 40f
            : preset == GraphicsPreset.Low ? 20f : 0f;
        pipeline.shadowCascadeCount = preset >= GraphicsPreset.High ? 4
            : preset == GraphicsPreset.Medium ? 2 : 1;
        pipeline.mainLightShadowmapResolution = preset >= GraphicsPreset.High ? 4096
            : preset == GraphicsPreset.Medium ? 1024 : 512;
        pipeline.additionalLightsShadowmapResolution = preset >= GraphicsPreset.High ? 2048
            : preset == GraphicsPreset.Medium ? 512 : 256;
        pipeline.maxAdditionalLightsCount = preset == GraphicsPreset.Max ? 8
            : preset == GraphicsPreset.High ? 8
            : preset == GraphicsPreset.Medium ? 4
            : preset == GraphicsPreset.Low ? 2 : 0;
        pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras = highLook;
    }

    private static void ApplyRendererSettings(UniversalRenderPipelineAsset pipeline, GraphicsPreset preset)
    {
        if (pipeline.rendererDataList.Length == 0)
            return;

        var data = pipeline.rendererDataList[0] as UniversalRendererData;
        if (data == null)
            return;

        // Forward+ is used at every preset. The world holds ~22k renderers and ~100 point
        // lights; plain Forward culls those lights per renderer on the CPU and also forces
        // GPU Resident Drawer off (it only runs under Forward+/Deferred+), which made the
        // lower presets slower than High. The preset still scales lights, shadows, resolution
        // and draw distance, so the cheap modes stay cheap.
        RenderingMode mode = forcedRenderingMode >= 0
            ? (RenderingMode)forcedRenderingMode
            : RenderingMode.ForwardPlus;
        if (data.renderingMode != mode)
            data.renderingMode = mode;

        bool ambientOcclusion = preset >= GraphicsPreset.High;
        for (int i = 0; i < data.rendererFeatures.Count; i++)
        {
            if (data.rendererFeatures[i] is ScreenSpaceAmbientOcclusion ssao)
                ssao.SetActive(ambientOcclusion);
        }
    }

    private void CreateOverrideVolume()
    {
        overrideVolume = gameObject.AddComponent<Volume>();
        overrideVolume.isGlobal = true;
        overrideVolume.priority = OverrideVolumePriority;
        overrideVolume.weight = 1f;
        overrideVolume.gameObject.layer = 0;
    }

    private void ApplyVolumeOverrides(GraphicsPreset preset)
    {
        if (overrideVolume == null)
            return;

        if (preset == GraphicsPreset.High)
        {
            // High leaves the authored scene volumes exactly as they are.
            overrideVolume.enabled = false;
            return;
        }

        overrideVolume.sharedProfile = null;
        if (overrideProfile != null)
            Destroy(overrideProfile);

        overrideProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        overrideProfile.name = "Graphics Preset Override (" + preset + ")";

        if (preset == GraphicsPreset.Medium)
        {
            // Keep Scene 1's vision blur (just cheaper sampling) and a light grade, but drop
            // the two other expensive full-screen effects (bloom and heavy film grain).
            ApplyVisionBlur(overrideProfile, false);

            var bloom = overrideProfile.Add<Bloom>(true);
            bloom.intensity.value = 0f;

            var filmGrain = overrideProfile.Add<FilmGrain>(true);
            filmGrain.intensity.value = 0f;

            var chromaticAberration = overrideProfile.Add<ChromaticAberration>(true);
            chromaticAberration.intensity.value = 0f;

            var colorAdjustments = overrideProfile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.value = 0.06f;
            colorAdjustments.contrast.value = 8f;
            colorAdjustments.saturation.value = 4f;

            var vignette = overrideProfile.Add<Vignette>(true);
            vignette.intensity.value = 0.12f;

            var tonemapping = overrideProfile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;
        }
        else if (preset == GraphicsPreset.Max)
        {
            // MAX adds to the authored look without touching the Scene 1 vision blur or the
            // scene's own depth of field, so those volumes still win.
            var bloom = overrideProfile.Add<Bloom>(true);
            bloom.intensity.value = 0.4f;
            bloom.threshold.value = 0.95f;
            bloom.scatter.value = 0.72f;
            bloom.highQualityFiltering.value = true;

            var colorAdjustments = overrideProfile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.value = 0.16f;
            colorAdjustments.contrast.value = 16f;
            colorAdjustments.saturation.value = 12f;
            colorAdjustments.colorFilter.value = new Color(1f, 0.94f, 0.86f, 1f);

            var vignette = overrideProfile.Add<Vignette>(true);
            vignette.intensity.value = 0.2f;
            vignette.smoothness.value = 0.5f;

            var filmGrain = overrideProfile.Add<FilmGrain>(true);
            filmGrain.intensity.value = 0.08f;
            filmGrain.response.value = 0.8f;

            var chromaticAberration = overrideProfile.Add<ChromaticAberration>(true);
            chromaticAberration.intensity.value = 0.03f;

            var tonemapping = overrideProfile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.ACES;
        }
        else
        {
            // Low and Potato strip the authored grade and bloom entirely but keep the vision
            // blur, because it is a story beat and not eye candy.
            ApplyVisionBlur(overrideProfile, false);

            var bloom = overrideProfile.Add<Bloom>(true);
            bloom.intensity.value = 0f;

            var filmGrain = overrideProfile.Add<FilmGrain>(true);
            filmGrain.intensity.value = 0f;

            var chromaticAberration = overrideProfile.Add<ChromaticAberration>(true);
            chromaticAberration.intensity.value = 0f;

            var colorAdjustments = overrideProfile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.value = 0f;
            colorAdjustments.contrast.value = 0f;
            colorAdjustments.saturation.value = 0f;
            colorAdjustments.hueShift.value = 0f;
            colorAdjustments.colorFilter.value = Color.white;

            var vignette = overrideProfile.Add<Vignette>(true);
            vignette.intensity.value = 0f;

            var tonemapping = overrideProfile.Add<Tonemapping>(true);
            tonemapping.mode.value = TonemappingMode.Neutral;
        }

        overrideVolume.sharedProfile = overrideProfile;
        overrideVolume.enabled = true;
    }

    // Overrides the scene's depth of field so the same blur stays on screen at a cheaper
    // sampling cost. Bokeh mode is kept because Gaussian mode cannot reach the heavy blur
    // Scene 1 authors (its radius is clamped to 1.5). The component is added with
    // SetAllOverridesTo(false): overriding every parameter would write this profile's
    // defaults (mode = Off) over the scene's authored blur and erase it.
    private static void ApplyVisionBlur(VolumeProfile profile, bool highQuality)
    {
        var depthOfField = profile.Add<DepthOfField>(false);
        depthOfField.highQualitySampling.Override(highQuality);
    }

    private static void ApplyCameraSettings(GraphicsPreset preset)
    {
        bool postProcessing = UsesPostProcessing(preset);
        bool hdr = preset >= GraphicsPreset.Medium;
        bool msaa = preset >= GraphicsPreset.High;
        bool shadows = preset > GraphicsPreset.Potato;
        AntialiasingMode antialiasing = preset == GraphicsPreset.Max
            ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
            : preset == GraphicsPreset.Medium
                ? AntialiasingMode.FastApproximateAntialiasing
                : AntialiasingMode.None;

        int count = Camera.GetAllCameras(cameraBuffer);
        if (count >= cameraBuffer.Length)
        {
            cameraBuffer = new Camera[cameraBuffer.Length * 2];
            count = Camera.GetAllCameras(cameraBuffer);
        }

        float farClip = Mathf.Min(RenderDistanceSettings.Distance, MaxRenderDistance);

        for (int i = 0; i < count; i++)
        {
            Camera camera = cameraBuffer[i];
            if (camera == null)
                continue;

            camera.allowHDR = hdr;
            if (camera.farClipPlane > farClip)
                camera.farClipPlane = farClip;

            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                continue;

            camera.allowMSAA = msaa;
            cameraData.renderPostProcessing = postProcessing;
            cameraData.renderShadows = shadows;
            cameraData.antialiasing = antialiasing;
            cameraData.antialiasingQuality = preset == GraphicsPreset.Max
                ? AntialiasingQuality.High
                : AntialiasingQuality.Medium;
            cameraData.dithering = preset == GraphicsPreset.Max;
        }
    }
}
