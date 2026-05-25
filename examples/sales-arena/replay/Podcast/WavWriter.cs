using System.Text;

namespace SalesArena.Replay.Podcast;

/// <summary>
/// Pure RIFF/WAVE writer. Takes raw 16-bit signed PCM samples and produces
/// a valid .wav file. No third-party dependency — voice intelligibility is
/// the TTS adapter's job, file format integrity is ours.
/// </summary>
public static class WavWriter
{
    public static byte[] Write(byte[] pcmBytes, PodcastOptions options)
    {
        ArgumentNullException.ThrowIfNull(pcmBytes);
        ArgumentNullException.ThrowIfNull(options);

        var byteRate = options.SampleRate * options.Channels * options.BitsPerSample / 8;
        var blockAlign = (short)(options.Channels * options.BitsPerSample / 8);
        var dataChunkSize = pcmBytes.Length;
        var riffSize = 36 + dataChunkSize;

        using var ms = new MemoryStream(44 + dataChunkSize);
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        // RIFF header.
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(riffSize);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt subchunk.
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);                                            // subchunk size (PCM = 16)
        w.Write((short)1);                                       // audio format = PCM
        w.Write((short)options.Channels);
        w.Write(options.SampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write((short)options.BitsPerSample);

        // data subchunk.
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataChunkSize);
        w.Write(pcmBytes);

        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Returns true if <paramref name="bytes"/> begins with a valid RIFF/WAVE header.
    /// Tests use this to verify the writer output round-trips into any wav player.
    /// </summary>
    public static bool IsValidWav(byte[] bytes)
    {
        if (bytes is null || bytes.Length < 44) return false;
        if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F') return false;
        if (bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E') return false;
        if (bytes[12] != 'f' || bytes[13] != 'm' || bytes[14] != 't' || bytes[15] != ' ') return false;
        return true;
    }
}
