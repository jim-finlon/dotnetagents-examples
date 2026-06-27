using System;
using System.IO;

namespace SalesArena.Meeting.AudioSynth;

/// <summary>
/// One-off DNA-authored synthesizer that produces a copyright-clean WAV
/// fixture for the SA-01-05 Meeting Agent integration test. The output
/// is pure tone + silence -- there is no human speech, no IP claim, and
/// the bytes are CC0 by virtue of being entirely DNA-synthesized.
///
/// <para>Run: <c>dotnet run --project examples/sales-arena/agents/calendar/test-audio/_synth/MeetingAudioSynth</c>.
/// Output lands at <c>examples/sales-arena/agents/calendar/test-audio/meeting-demo.wav</c>.</para>
///
/// <para>The fixture is intentionally short (about 4 seconds) and shaped so
/// the bit-pattern is stable across runs: four 0.5s tones at A4/C5/E5/G5
/// separated by 0.5s silences, 22.05 kHz mono PCM16. Total ~177 KB.
/// Used together with a stubbed <c>IVoiceTranscriptionService</c> in the
/// integration test: the file must exist and parse as WAV; the canned
/// transcription content is provided by the test harness.</para>
/// </summary>
internal static class Program
{
    private const int SampleRate = 22050;
    private const int BitsPerSample = 16;
    private const int Channels = 1;

    private static int Main(string[] args)
    {
        // bin/Debug/net10.0 -> ../../../ MeetingAudioSynth -> ../ _synth -> ../ test-audio (the canonical output target).
        var outDir = args.Length > 0
            ? args[0]
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        Directory.CreateDirectory(outDir);

        var samples = Synthesize();
        var bytes = WriteWav(samples);
        var outPath = Path.Combine(outDir, "meeting-demo.wav");
        File.WriteAllBytes(outPath, bytes);
        Console.WriteLine($"wrote {outPath} ({bytes.Length} bytes, {samples.Length} samples, {samples.Length / (double)SampleRate:F2}s)");
        return 0;
    }

    private static short[] Synthesize()
    {
        // 4 tones (A4=440, C5=523.25, E5=659.25, G5=783.99) interleaved with silence.
        double[] tones = { 440.0, 523.25, 659.25, 783.99 };
        const double toneSeconds = 0.5;
        const double silenceSeconds = 0.5;

        int toneSamples = (int)(toneSeconds * SampleRate);
        int silenceSamples = (int)(silenceSeconds * SampleRate);
        int totalSamples = (toneSamples + silenceSamples) * tones.Length;

        var s = new short[totalSamples];
        int write = 0;
        foreach (var freq in tones)
        {
            for (int i = 0; i < toneSamples; i++)
            {
                double t = i / (double)SampleRate;
                // Half-amplitude sine + 50ms attack + 50ms release envelope to avoid clicks.
                double envelope = AttackRelease(t, toneSeconds, 0.05);
                double v = 0.45 * envelope * Math.Sin(2.0 * Math.PI * freq * t);
                s[write++] = (short)(v * short.MaxValue);
            }
            for (int i = 0; i < silenceSamples; i++)
                s[write++] = 0;
        }
        return s;
    }

    private static double AttackRelease(double t, double total, double tail)
    {
        if (t < tail) return t / tail;
        if (t > total - tail) return Math.Max(0.0, (total - t) / tail);
        return 1.0;
    }

    private static byte[] WriteWav(short[] samples)
    {
        int byteRate = SampleRate * Channels * BitsPerSample / 8;
        int dataSize = samples.Length * BitsPerSample / 8;
        int chunkSize = 36 + dataSize;
        using var ms = new MemoryStream(chunkSize + 8);
        using var w = new BinaryWriter(ms);
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(chunkSize);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16); // PCM fmt-chunk size
        w.Write((short)1); // PCM format
        w.Write((short)Channels);
        w.Write(SampleRate);
        w.Write(byteRate);
        w.Write((short)(Channels * BitsPerSample / 8)); // block align
        w.Write((short)BitsPerSample);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);
        foreach (var sample in samples)
            w.Write(sample);
        return ms.ToArray();
    }
}
