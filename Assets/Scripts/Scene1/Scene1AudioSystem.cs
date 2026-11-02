using UnityEngine;

public sealed class Scene1AudioSystem : MonoBehaviour
{
    private const int SampleRate = 22050;
    private const int ClipSeconds = 6;

    private void Awake()
    {
        AssignClip("Room Ambience", 0, true, 0.08f);
        AssignClip("Electrical Hum", 1, true, 0.055f);
        AssignClip("Monitor Noise", 2, true, 0.025f);
        AssignClip("Light Buzz", 3, true, 0.025f);
        AssignClip("Distant Building Noise", 4, false, 0.08f);
        AssignClip("Air Movement", 5, true, 0.025f);
    }

    private void AssignClip(string objectName, int soundKind, bool loop, float volume)
    {
        var sourceObject = transform.Find(objectName);
        if (sourceObject == null)
        {
            return;
        }

        var source = sourceObject.GetComponent<AudioSource>();
        if (source == null || source.clip != null)
        {
            return;
        }

        source.clip = CreateClip(objectName, soundKind, loop);
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
    }

    private static AudioClip CreateClip(string clipName, int soundKind, bool loop)
    {
        var sampleCount = SampleRate * ClipSeconds;
        var samples = new float[sampleCount];
        var random = new System.Random(2701 + soundKind);
        var phase = 0f;

        for (var i = 0; i < sampleCount; i++)
        {
            var time = (float)i / SampleRate;
            var noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float sample;

            switch (soundKind)
            {
                case 0:
                    var swell = 0.7f + 0.3f * Mathf.Sin(time * 0.72f);
                    sample = swell * (Mathf.Sin(time * 271f) * 0.18f + Mathf.Sin(time * 563f) * 0.045f) + noise * 0.012f;
                    break;
                case 1:
                    sample = Mathf.Sin(time * 376.99f) * 0.11f + Mathf.Sin(time * 753.98f) * 0.035f;
                    break;
                case 2:
                    phase += noise * 0.0004f;
                    sample = noise * (i % 11025 < 170 ? 0.12f : 0.012f) + Mathf.Sin(time * 91f + phase) * 0.025f;
                    break;
                case 3:
                    sample = Mathf.Sin(time * 753.98f) * 0.055f + noise * 0.008f;
                    break;
                case 4:
                    var envelope = Mathf.Sin(Mathf.PI * time / ClipSeconds);
                    sample = envelope * (Mathf.Sin(time * 94f) * 0.22f + Mathf.Sin(time * 151f) * 0.1f + noise * 0.06f);
                    break;
                default:
                    sample = Mathf.Sin(time * 69f) * 0.04f + Mathf.Sin(time * 37f) * 0.025f + noise * 0.012f;
                    break;
            }

            samples[i] = Mathf.Clamp(sample, -0.35f, 0.35f);
        }

        var clip = AudioClip.Create("Scene1 " + clipName + " Placeholder", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}