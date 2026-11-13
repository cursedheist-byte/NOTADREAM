# NyxoGame PC FPS Investigation (Superseded)

**Date:** 2026-11-09  
**Scope:** PC rendering profile only. No build, runtime benchmark, or Profiler capture was run.

## Finding

The active quality index was `PC` (`ProjectSettings/QualitySettings.asset`, `m_CurrentQuality: 1`). Its PC Universal Render Pipeline asset was configured for a render scale of 1.5, 4x MSAA, a 4096 main-light shadow map, and 120 m shadow distance. The PC QualitySettings entry also specified 4x anti-aliasing and 120 m shadows. No script under `Assets/Scripts` was found overriding render scale, shadow distance, MSAA, or quality level at runtime.

The 1.5 render scale means 2.25 times as many render pixels as native resolution. This is a strong, game-wide GPU cost candidate, especially combined with MSAA and full-resolution screen effects. It is a static configuration finding; actual GPU/CPU frame time was not measured.

## Temporary change and restoration

1. `Assets/Settings/PC_RPAsset.asset`
   - Render scale: **1.5 → 1.0** (native resolution).
   - MSAA: **4x → 2x**.
   - Main-light shadow map: **4096 → 2048**.
   - Shadow distance: **120 m → 80 m**.
2. `ProjectSettings/QualitySettings.asset`
   - PC shadow distance: **120 m → 80 m**.
   - PC anti-aliasing: **4x → 2x**.

The user clarified that rendering settings are not the suspected cause. Therefore, these temporary values have been restored to their original values: PC render scale 1.5, 4x MSAA, 4096 main-light shadow map, and 120 m shadows; PC QualitySettings anti-aliasing 4x and 120 m shadows. No runtime FPS gain was measured and none is claimed. The Mobile profile and other rendering settings were never changed.

## Validation and limits

- Confirmed the PC profile is the active quality index and references the PC URP asset.
- Confirmed the temporary values, then restored the original PC settings in both YAML files; no runtime quality overrides were found in scripts.
- Unity Editor reported idle, MainMenu active, no asset update or script compilation in progress after edits.
- `git diff --check` reported no whitespace errors; Git emitted only its normal LF/CRLF normalization notices.
- CPU/RAM were checked before edits (66% CPU, about 11.4 GB free RAM). Python processes were left untouched.
- No build, Play Mode launch, stress test, benchmark, or Profiler session was run. The FPS improvement and image quality in gameplay therefore remain unverified. These changes target the Windows PC profile; other platforms use their own quality profile.

## If frame rate is still low

This change addresses a clearly excessive pixel/shadow configuration but cannot prove the whole-game bottleneck without runtime frame-time data. If FPS remains low, the next safe diagnostic should be a brief, ordinary gameplay observation of CPU and GPU frame times and resolution, with training load low. Do not apply further broad quality reductions based on guesswork.
