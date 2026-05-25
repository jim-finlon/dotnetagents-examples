using System.Globalization;
using System.Text;

namespace SalesArena.Assets.AudioCueSynth;

/// <summary>
/// One-off DNA-authored synthesizer for SA-06-04's four audio cues
/// (bell, drumroll, sad-trombone, cold-open). Pure System.IO + Math —
/// zero third-party dependencies. The output WAVs are CC0 by virtue of
/// being entirely DNA-synthesized.
///
/// <para>Run: <c>dotnet run --project samples/sales-arena/assets/audio/_synth/AudioCueSynth</c>.
/// Output lands at <c>samples/sales-arena/assets/audio/{bell,drumroll,sad-trombone,cold-open}.wav</c>.
/// Determinism: every sample is derived from a fixed-seed pseudo-RNG +
/// closed-form math; runs are bit-exact.</para>
/// </summary>
internal static class Program
{
    private const int SampleRate = 22050;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    private static int Main(string[] args)
    {
        var outDir = args.Length > 0
            ? args[0]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        Directory.CreateDirectory(outDir);

        Write(outDir, "bell.wav",          SynthBell());
        Write(outDir, "drumroll.wav",      SynthDrumroll());
        Write(outDir, "sad-trombone.wav",  SynthSadTrombone());
        Write(outDir, "cold-open.wav",     SynthColdOpen());
        return 0;
    }

    private static void Write(string outDir, string name, short[] samples)
    {
        var path = Path.Combine(outDir, name);
        var bytes = WriteWav(samples);
        File.WriteAllBytes(path, bytes);
        Console.WriteLine($"wrote {path} ({bytes.Length} bytes)");
    }

    // ---------- bell.wav ----------
    // Single-strike trading-floor bell: 3 harmonics with exponential decay.

    private static short[] SynthBell()
    {
        const double durationSec = 2.5;
        const double decayTau = 0.55;            // ~1.5s perceptual decay
        double[] partials = { 1318.51, 2637.02, 3956.0, 5274.0 };
        double[] partialAmps = { 1.00, 0.55, 0.30, 0.15 };

        var n = (int)(durationSec * SampleRate);
        var s = new short[n];
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)SampleRate;
            var envelope = Math.Exp(-t / decayTau);
            double sample = 0;
            for (var k = 0; k < partials.Length; k++)
            {
                sample += partialAmps[k] * Math.Sin(2 * Math.PI * partials[k] * t);
            }
            // Strike-attack micro-noise in the first 8 ms.
            if (t < 0.008)
            {
                sample += (Math.Sin(i * 12.91) - 0.5) * 0.4 * (1.0 - t / 0.008);
            }
            sample *= envelope;
            s[i] = Clamp16((int)(sample * 0.6 * short.MaxValue));
        }
        return Normalize(s, targetPeak: 0.85);
    }

    // ---------- drumroll.wav ----------
    // Filtered noise with rising amplitude envelope, single snare hit at the end.

    private static short[] SynthDrumroll()
    {
        const double durationSec = 4.0;
        const double rollEndSec = 3.55;
        const double hitDurationSec = 0.35;

        var n = (int)(durationSec * SampleRate);
        var s = new short[n];
        var rng = new SeededRng(seed: 0xC0FFEE);

        // Two-pole low-pass state (snare timbre).
        double y1 = 0, y2 = 0;

        for (var i = 0; i < n; i++)
        {
            var t = i / (double)SampleRate;
            double sample;
            if (t < rollEndSec)
            {
                // Roll: filtered noise modulated at 38 Hz rapid amplitude pulses.
                var noise = rng.NextDouble() * 2 - 1;
                var lp = 0.50 * noise + 0.30 * y1 + 0.18 * y2;
                y2 = y1;
                y1 = lp;
                var rollAmpEnv = Math.Pow(t / rollEndSec, 1.4);                 // rising
                var beatModulation = 0.65 + 0.35 * Math.Sin(2 * Math.PI * 38 * t);
                sample = lp * rollAmpEnv * beatModulation;
            }
            else
            {
                // Single hit: percussive thump + bright snare ring.
                var u = t - rollEndSec;
                if (u > hitDurationSec)
                {
                    sample = 0;
                }
                else
                {
                    var hitEnv = Math.Exp(-u / 0.07);
                    var thump = Math.Sin(2 * Math.PI * 180 * u) * 0.7;
                    var ring  = (rng.NextDouble() * 2 - 1) * 0.6;
                    sample = (thump + ring) * hitEnv;
                }
            }
            s[i] = Clamp16((int)(sample * 0.55 * short.MaxValue));
        }
        return Normalize(s, targetPeak: 0.85);
    }

    // ---------- sad-trombone.wav ----------
    // Four descending notes; sawtooth approximation with light vibrato.

    private static short[] SynthSadTrombone()
    {
        // Bb3 → A3 → Ab3 → G3 — the classic "wah wah waaaah" descent.
        double[] notes = { 233.08, 220.00, 207.65, 196.00 };
        double[] durations = { 0.40, 0.40, 0.40, 0.80 };          // last note holds

        var totalSec = 0.0;
        foreach (var d in durations) totalSec += d;

        var n = (int)(totalSec * SampleRate);
        var s = new short[n];
        var cursor = 0;
        for (var ni = 0; ni < notes.Length; ni++)
        {
            var freq = notes[ni];
            var noteSamples = (int)(durations[ni] * SampleRate);
            for (var i = 0; i < noteSamples && (cursor + i) < n; i++)
            {
                var t = i / (double)SampleRate;
                // Light vibrato 4 Hz at ±0.6%.
                var vibrato = 1.0 + 0.006 * Math.Sin(2 * Math.PI * 4.0 * t);
                var phase = (2 * Math.PI * freq * vibrato * t) % (2 * Math.PI);
                // Sawtooth via Fourier series (5 harmonics) — brass-ish without DC offset.
                double sample = 0;
                for (var k = 1; k <= 5; k++)
                {
                    sample += Math.Sin(k * phase) / k;
                }
                sample *= 0.45;
                // ADSR-like envelope per note: 8 ms attack, smooth decay.
                var envelope = AttackDecay(t, attack: 0.020, decay: durations[ni]);
                sample *= envelope;
                s[cursor + i] = Clamp16((int)(sample * 0.7 * short.MaxValue));
            }
            cursor += noteSamples;
        }
        return Normalize(s, targetPeak: 0.85);
    }

    // ---------- cold-open.wav ----------
    // Low brass swell suitable as a voice-over bed.

    private static short[] SynthColdOpen()
    {
        const double durationSec = 5.0;
        // A minor triad rooted on A2; brass swell, no melody, broad envelope.
        double[] partials = { 110.00, 164.81, 220.00, 329.63, 440.00 };
        double[] partialAmps = { 1.00, 0.65, 0.55, 0.35, 0.22 };

        var n = (int)(durationSec * SampleRate);
        var s = new short[n];
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)SampleRate;
            double sample = 0;
            for (var k = 0; k < partials.Length; k++)
            {
                sample += partialAmps[k] * Math.Sin(2 * Math.PI * partials[k] * t);
            }
            // Slow attack 1.2s → sustain ~2.6s → slow release 1.2s.
            var envelope = SwellEnvelope(t, attack: 1.2, release: 1.2, total: durationSec);
            sample *= envelope * 0.35;
            s[i] = Clamp16((int)(sample * 0.85 * short.MaxValue));
        }
        return Normalize(s, targetPeak: 0.80);
    }

    // ---------- helpers ----------

    private static double AttackDecay(double t, double attack, double decay)
    {
        if (t < attack) return t / attack;
        if (t >= decay) return 0;
        return Math.Max(0, 1.0 - (t - attack) / Math.Max(1e-6, (decay - attack)));
    }

    private static double SwellEnvelope(double t, double attack, double release, double total)
    {
        if (t < attack) return Math.Sin((t / attack) * Math.PI * 0.5);
        if (t > total - release) return Math.Sin(((total - t) / release) * Math.PI * 0.5);
        return 1.0;
    }

    private static short Clamp16(int v) =>
        v > short.MaxValue ? short.MaxValue :
        v < short.MinValue ? short.MinValue : (short)v;

    private static short[] Normalize(short[] samples, double targetPeak)
    {
        var peak = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            var abs = samples[i] < 0 ? -samples[i] : samples[i];
            if (abs > peak) peak = abs;
        }
        if (peak == 0) return samples;
        var scale = (targetPeak * short.MaxValue) / peak;
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = Clamp16((int)(samples[i] * scale));
        }
        return samples;
    }

    private static byte[] WriteWav(short[] samples)
    {
        var byteRate = SampleRate * Channels * BitsPerSample / 8;
        var blockAlign = (short)(Channels * BitsPerSample / 8);
        var dataSize = samples.Length * 2;
        var riffSize = 36 + dataSize;
        using var ms = new MemoryStream(44 + dataSize);
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(riffSize);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);
        w.Write((short)Channels);
        w.Write(SampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write((short)BitsPerSample);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);
        for (var i = 0; i < samples.Length; i++)
        {
            w.Write(samples[i]);
        }
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Mulberry32-style seeded PRNG. Bit-exact across .NET versions so a
    /// regenerated drumroll.wav matches byte-for-byte. Pure integer math
    /// — no dependency on System.Random's implementation details.
    /// </summary>
    private sealed class SeededRng
    {
        private uint _state;
        public SeededRng(uint seed) { _state = seed; }

        public double NextDouble()
        {
            _state += 0x6D2B79F5;
            var t = _state;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + ((t ^ (t >> 7)) * (t | 61));
            return ((t ^ (t >> 14)) >>> 0) / 4294967296.0;
        }
    }
}
