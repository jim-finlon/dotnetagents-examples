using System.Text;

namespace SalesArena.Assets.WalkOnSynth;

/// <summary>
/// DNA-authored synthesizer for the SA-08-10 per-persona walk-on themes.
/// Each clip is an 8-second character motif at 11025 Hz mono 16-bit
/// (≈ 172 KB) — well under the 200 KB-per-clip cap. Pure System.IO + Math,
/// zero third-party deps, bit-exact regeneration.
///
/// <para>Run: <c>dotnet run --project examples/sales-arena/assets/audio/walk-ons/_synth/WalkOnSynth</c>.
/// Output lands at <c>examples/sales-arena/assets/audio/walk-ons/{persona}.wav</c>.</para>
/// </summary>
internal static class Program
{
    private const int SampleRate = 11025;
    private const int Channels = 1;
    private const int BitsPerSample = 16;
    private const double DurationSec = 8.0;

    private static int Main(string[] args)
    {
        var outDir = args.Length > 0
            ? args[0]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        Directory.CreateDirectory(outDir);

        Write(outDir, "roma.wav",              SynthRoma());
        Write(outDir, "levene.wav",            SynthLevene());
        Write(outDir, "moss.wav",              SynthMoss());
        Write(outDir, "aaronow.wav",           SynthAaronow());
        Write(outDir, "williamson.wav",        SynthWilliamson());
        Write(outDir, "mitch-and-murray.wav",  SynthMitchAndMurray());
        return 0;
    }

    private static void Write(string outDir, string name, short[] samples)
    {
        var path = Path.Combine(outDir, name);
        var bytes = WriteWav(samples);
        File.WriteAllBytes(path, bytes);
        Console.WriteLine($"wrote {path} ({bytes.Length} bytes)");
    }

    // ---------- roma: elegant brass fanfare ----------
    // Roma is the consultative closer — smooth, deliberate, theatrical.

    private static short[] SynthRoma()
    {
        // C major triad arpeggio then sustained.
        double[] arpeggio = { 261.63, 329.63, 392.00, 523.25 };
        var n = (int)(DurationSec * SampleRate);
        var s = new short[n];
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)SampleRate;
            double sample = 0;
            // Sustained pad on the first three triad tones.
            sample += 0.4 * Math.Sin(2 * Math.PI * arpeggio[0] * t);
            sample += 0.3 * Math.Sin(2 * Math.PI * arpeggio[1] * t);
            sample += 0.25 * Math.Sin(2 * Math.PI * arpeggio[2] * t);
            // Arpeggio melody: each note for 0.8s in the first 3.2s, then hold the high octave.
            var noteSlot = (int)Math.Min(3, t / 0.8);
            var melodyFreq = arpeggio[noteSlot];
            sample += 0.45 * Math.Sin(2 * Math.PI * melodyFreq * t);
            // Envelope: 0.4s attack, smooth release at the end.
            var envelope = SwellEnvelope(t, attack: 0.4, release: 0.8, total: DurationSec);
            sample *= envelope * 0.30;
            s[i] = Clamp16((int)(sample * 0.8 * short.MaxValue));
        }
        return Normalize(s, targetPeak: 0.82);
    }

    // ---------- levene: aggressive hunt theme ----------
    // Levene is the high-volume hunter — fast, urgent, predatory.

    private static short[] SynthLevene()
    {
        var n = (int)(DurationSec * SampleRate);
        var s = new short[n];
        var rng = new SeededRng(0xBEEF1234);
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)SampleRate;
            // Hi-hat-style noise pulse @ 8 Hz with rapid decay envelope.
            var beatPhase = t * 8.0;
            var beatPos = beatPhase - Math.Floor(beatPhase);
            var hatEnv = Math.Exp(-beatPos * 35.0);
            var hat = (rng.NextDouble() * 2 - 1) * 0.55 * hatEnv;
            // Ascending D-minor arpeggio motif played every 2 seconds.
            double[] motif = { 146.83, 174.61, 220.00, 261.63 };  // D3, F3, A3, C4
            var motifSlot = (int)Math.Min(3, (t % 2.0) / 0.5);
            var melody = 0.40 * Math.Sin(2 * Math.PI * motif[motifSlot] * t);
            // Bass thump on the downbeat (every second).
            var bassPhase = t * 1.0;
            var bassPos = bassPhase - Math.Floor(bassPhase);
            var bassEnv = Math.Exp(-bassPos * 12.0);
            var bass = 0.6 * Math.Sin(2 * Math.PI * 73.42 * t) * bassEnv;
            var sample = (hat + melody + bass) * 0.32;
            var envelope = SwellEnvelope(t, attack: 0.2, release: 0.5, total: DurationSec);
            sample *= envelope;
            s[i] = Clamp16((int)(sample * 0.85 * short.MaxValue));
        }
        return Normalize(s, targetPeak: 0.85);
    }

    // ---------- moss: jazzy minor-key piano ----------
    // Moss is the skeptical surgeon — careful, deliberate, blue-note.

    private static short[] SynthMoss()
    {
        // A minor pentatonic motif: A3, C4, D4, E4, G4, A4
        double[] scale = { 220.00, 261.63, 293.66, 329.63, 392.00, 440.00 };
        // Riff sequence indices over 8 beats (one beat = 1.0s).
        int[] riff = { 0, 3, 2, 5, 4, 2, 1, 0 };

        var n = (int)(DurationSec * SampleRate);
        var s = new short[n];
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)SampleRate;
            var noteSlot = (int)Math.Min(7, t / 1.0);
            var freq = scale[riff[noteSlot]];
            // Piano-ish envelope: sharp attack, exponential decay.
            var noteT = t - noteSlot;
            var noteEnv = Math.Exp(-noteT * 2.5);
            // Slight detuning for jazz colour.
            var sample = 0.45 * Math.Sin(2 * Math.PI * freq * t);
            sample += 0.20 * Math.Sin(2 * Math.PI * freq * 2.01 * t);  // octave shimmer
            sample += 0.10 * Math.Sin(2 * Math.PI * freq * 1.498 * t); // perfect-fifth ghost
            sample *= noteEnv * 0.6;
            // Subtle continuous low pad on the tonic (A2).
            sample += 0.10 * Math.Sin(2 * Math.PI * 110 * t);
            var envelope = SwellEnvelope(t, attack: 0.15, release: 0.8, total: DurationSec);
            sample *= envelope;
            s[i] = Clamp16((int)(sample * 0.75 * short.MaxValue));
        }
        return Normalize(s, targetPeak: 0.83);
    }

    // ---------- aaronow: steady-rhythm reliable theme ----------
    // Aaronow is the booker — dependable, measured, low-drama.

    private static short[] SynthAaronow()
    {
        var n = (int)(DurationSec * SampleRate);
        var s = new short[n];
        // March-tempo bass drum on every beat (1 Hz) + descending tone every 2s.
        double[] descent = { 246.94, 220.00, 196.00, 174.61 };  // B3, A3, G3, F3
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)SampleRate;
            // Bass kick.
            var kickPhase = t - Math.Floor(t);
            var kickEnv = Math.Exp(-kickPhase * 18.0);
            var kick = 0.55 * Math.Sin(2 * Math.PI * 65 * t) * kickEnv;
            // Melody descent — each note 2 seconds, repeating once over the 8s.
            var melodyIdx = (int)Math.Min(3, (t % 4.0) / 1.0);
            var melodyFreq = descent[melodyIdx];
            var melody = 0.40 * Math.Sin(2 * Math.PI * melodyFreq * t);
            // Steady mid-frequency hum to ground it.
            var hum = 0.10 * Math.Sin(2 * Math.PI * 110 * t);
            var sample = (kick + melody + hum) * 0.36;
            var envelope = SwellEnvelope(t, attack: 0.3, release: 0.5, total: DurationSec);
            sample *= envelope;
            s[i] = Clamp16((int)(sample * 0.8 * short.MaxValue));
        }
        return Normalize(s, targetPeak: 0.84);
    }

    // ---------- williamson: stage-bell announcement ----------
    // Williamson is the manager handing out leads — a stage bell + held note.

    private static short[] SynthWilliamson()
    {
        var n = (int)(DurationSec * SampleRate);
        var s = new short[n];
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)SampleRate;
            double sample = 0;
            // Hand-bell: short bright ring at t=0 with second strike at t=4.0s.
            if (t < 2.0)
            {
                var decay = Math.Exp(-t / 0.45);
                sample += decay * 0.5 * Math.Sin(2 * Math.PI * 880 * t);   // A5
                sample += decay * 0.3 * Math.Sin(2 * Math.PI * 1760 * t);  // A6
            }
            if (t >= 4.0 && t < 6.0)
            {
                var u = t - 4.0;
                var decay = Math.Exp(-u / 0.45);
                sample += decay * 0.5 * Math.Sin(2 * Math.PI * 880 * t);
                sample += decay * 0.3 * Math.Sin(2 * Math.PI * 1760 * t);
            }
            // Sustained A4 announcement tone underneath the whole clip.
            sample += 0.20 * Math.Sin(2 * Math.PI * 440 * t);
            var envelope = SwellEnvelope(t, attack: 0.1, release: 0.6, total: DurationSec);
            sample *= envelope * 0.4;
            s[i] = Clamp16((int)(sample * 0.8 * short.MaxValue));
        }
        return Normalize(s, targetPeak: 0.85);
    }

    // ---------- mitch-and-murray: low brass fanfare ----------
    // Mitch & Murray is the off-stage manager — stentorian, intimidating.

    private static short[] SynthMitchAndMurray()
    {
        // E minor low brass: E2, G2, B2, E3.
        double[] partials = { 82.41, 98.00, 123.47, 164.81 };
        double[] partialAmps = { 1.0, 0.7, 0.55, 0.45 };
        var n = (int)(DurationSec * SampleRate);
        var s = new short[n];
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)SampleRate;
            double sample = 0;
            // Sustained low brass triad.
            for (var k = 0; k < partials.Length; k++)
            {
                sample += partialAmps[k] * Math.Sin(2 * Math.PI * partials[k] * t);
            }
            // Rising stinger on the second half (t in [4, 8]).
            if (t >= 4.0)
            {
                var u = t - 4.0;
                var stingerFreq = 110 + u * 30;
                sample += 0.5 * Math.Sin(2 * Math.PI * stingerFreq * t) * (u / 4.0);
            }
            var envelope = SwellEnvelope(t, attack: 0.5, release: 1.0, total: DurationSec);
            sample *= envelope * 0.18;
            s[i] = Clamp16((int)(sample * 0.85 * short.MaxValue));
        }
        return Normalize(s, targetPeak: 0.85);
    }

    // ---------- helpers ----------

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
