using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Procedural retro sound effects and a short original ambient loop, written as 16-bit WAV files.
public static class SfxSynth
{
    public const string Folder = "Assets/Audio";
    const int Rate = 44100;

    public static Dictionary<SfxId, AudioClip> GenerateEffects()
    {
        Directory.CreateDirectory(Folder);
        var rng = new System.Random(42);
        return new Dictionary<SfxId, AudioClip>
        {
            [SfxId.Throw] = Save("sfx_throw", Mix(Noise(0.09f, rng, 0.35f, 30f), Sweep(0.09f, 1400f, 500f, Wave.Square, 0.12f, 25f))),
            [SfxId.Hit] = Save("sfx_hit", Mix(Sweep(0.08f, 260f, 110f, Wave.Square, 0.45f, 30f), Noise(0.04f, rng, 0.3f, 60f))),
            [SfxId.Recall] = Save("sfx_recall", Sweep(0.28f, 280f, 1300f, Wave.Triangle, 0.5f, 6f)),
            [SfxId.EnemyDie] = Save("sfx_enemy_die", Mix(Noise(0.28f, rng, 0.45f, 12f), Sweep(0.22f, 180f, 50f, Wave.Square, 0.35f, 10f))),
            [SfxId.PlayerHurt] = Save("sfx_player_hurt", Mix(Sweep(0.22f, 150f, 70f, Wave.Square, 0.5f, 9f), Noise(0.1f, rng, 0.25f, 30f))),
            [SfxId.Explosion] = Save("sfx_explosion", LowPass(Noise(0.75f, rng, 0.9f, 4.5f), 0.12f)),
            [SfxId.Coin] = Save("sfx_coin", Concat(Tone(0.05f, 988f, Wave.Square, 0.25f), Tone(0.1f, 1319f, Wave.Square, 0.25f))),
            [SfxId.Purchase] = Save("sfx_purchase", Concat(Tone(0.06f, 523f, Wave.Square, 0.25f), Tone(0.06f, 659f, Wave.Square, 0.25f), Tone(0.06f, 784f, Wave.Square, 0.25f), Tone(0.12f, 1047f, Wave.Square, 0.25f))),
            [SfxId.UIClick] = Save("sfx_ui_click", Tone(0.03f, 1200f, Wave.Square, 0.2f)),
        };
    }

    // A slow, original minor-key bass and arpeggio loop: 8 bars at 100 BPM.
    public static AudioClip GenerateMusic()
    {
        Directory.CreateDirectory(Folder);
        float beat = 60f / 100f;
        float[] roots = { 110f, 87.31f, 130.81f, 98f };              // A2, F2, C3, G2
        int[][] chord = { new[] { 0, 3, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 } };
        int bars = 8;
        float length = bars * 4 * beat;
        var data = new float[Mathf.CeilToInt(length * Rate)];

        for (int bar = 0; bar < bars; bar++)
        {
            float root = roots[bar % roots.Length];
            int[] intervals = chord[bar % chord.Length];
            float barStart = bar * 4 * beat;
            for (int b = 0; b < 4; b++)
                AddNote(data, barStart + b * beat, beat * 0.9f, root, Wave.Triangle, 0.22f, 3f);
            for (int s = 0; s < 8; s++)
            {
                int step = intervals[s % intervals.Length] + (s >= 4 ? 12 : 0);
                float freq = root * 4f * Mathf.Pow(2f, step / 12f);
                AddNote(data, barStart + s * beat * 0.5f, beat * 0.4f, freq, Wave.Square, 0.05f, 8f);
            }
        }
        return Save("music_night_loop", data);
    }

    enum Wave { Square, Triangle, Sine }

    static float Osc(Wave wave, float phase)
    {
        float p = phase - Mathf.Floor(phase);
        switch (wave)
        {
            case Wave.Square: return p < 0.5f ? 1f : -1f;
            case Wave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
            default: return Mathf.Sin(p * Mathf.PI * 2f);
        }
    }

    static float[] Tone(float seconds, float freq, Wave wave, float volume) => Sweep(seconds, freq, freq, wave, volume, 4f);

    static float[] Sweep(float seconds, float from, float to, Wave wave, float volume, float decay)
    {
        var data = new float[Mathf.CeilToInt(seconds * Rate)];
        float phase = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / Rate;
            float freq = Mathf.Lerp(from, to, t / seconds);
            phase += freq / Rate;
            data[i] = Osc(wave, phase) * volume * Mathf.Exp(-decay * t) * Fade(i, data.Length);
        }
        return data;
    }

    static float[] Noise(float seconds, System.Random rng, float volume, float decay)
    {
        var data = new float[Mathf.CeilToInt(seconds * Rate)];
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / Rate;
            data[i] = ((float)rng.NextDouble() * 2f - 1f) * volume * Mathf.Exp(-decay * t) * Fade(i, data.Length);
        }
        return data;
    }

    static float[] LowPass(float[] data, float amount)
    {
        float y = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            y += amount * (data[i] - y);
            data[i] = y * 3f;
        }
        return data;
    }

    static float[] Mix(params float[][] parts)
    {
        int length = 0;
        foreach (float[] p in parts) length = Mathf.Max(length, p.Length);
        var data = new float[length];
        foreach (float[] p in parts)
            for (int i = 0; i < p.Length; i++) data[i] += p[i];
        return data;
    }

    static float[] Concat(params float[][] parts)
    {
        var list = new List<float>();
        foreach (float[] p in parts) list.AddRange(p);
        return list.ToArray();
    }

    static void AddNote(float[] data, float start, float duration, float freq, Wave wave, float volume, float decay)
    {
        int begin = Mathf.FloorToInt(start * Rate);
        int count = Mathf.CeilToInt(duration * Rate);
        float phase = 0f;
        for (int i = 0; i < count && begin + i < data.Length; i++)
        {
            float t = (float)i / Rate;
            phase += freq / Rate;
            data[begin + i] += Osc(wave, phase) * volume * Mathf.Exp(-decay * t) * Fade(i, count);
        }
    }

    // Short fades on both ends prevent clicks.
    static float Fade(int i, int length)
    {
        const int edge = 120;
        return Mathf.Clamp01(Mathf.Min(i, length - 1 - i) / (float)edge);
    }

    static AudioClip Save(string name, float[] data)
    {
        string path = $"{Folder}/{name}.wav";
        using (var stream = new FileStream(path, FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            int byteCount = data.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + byteCount);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(Rate);
            writer.Write(Rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(byteCount);
            foreach (float sample in data)
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
