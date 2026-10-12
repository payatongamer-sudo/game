using System.Collections.Generic;
using System.IO;

namespace BrewingPoint.Minecraft
{
    /// <summary>Decoded PCM of one Minecraft .ogg, ready for AudioClip.Create + SetData.</summary>
    public sealed class OggPcm
    {
        public float[] Samples = System.Array.Empty<float>(); // interleaved
        public int Channels;
        public int SampleRate;
        public int FramesPerChannel => Channels == 0 ? 0 : Samples.Length / Channels;

        /// <summary>Decodes Ogg Vorbis bytes with NVorbis (MIT, bundled in the release).</summary>
        public static OggPcm Decode(byte[] ogg)
        {
            using var reader = new NVorbis.VorbisReader(new MemoryStream(ogg), true);
            var pcm = new OggPcm { Channels = reader.Channels, SampleRate = reader.SampleRate };
            var all = new List<float>(reader.TotalSamples > 0 ? (int)reader.TotalSamples * reader.Channels : 4096);
            var buffer = new float[4096 * reader.Channels];
            int read;
            while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
                for (int i = 0; i < read; i++)
                    all.Add(buffer[i]);
            pcm.Samples = all.ToArray();
            return pcm;
        }
    }
}
