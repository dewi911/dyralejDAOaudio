using System;
using System.Windows.Documents;
using NAudio.Wave;
using System.IO;
using System.Collections.Generic;


namespace dyralejDAOaudio
{
    internal class EffectStream : WaveStream
    {
        public WaveStream SourceStream { get; set; }
        public List<IEffect> Effects {  get;  private set; }
        public bool ApplyEffects { get; set; } = true;
        public EffectStream(WaveStream stream)
        {
            this.SourceStream = stream;
            this.Effects = new List<IEffect>();
        }

        public override long Length
        {
            get { return SourceStream.Length; }
        }

        public override long Position
        {
            get
            {
                return SourceStream.Position;
            }
            set
            {
                SourceStream.Position = value;
            }
        }

        public override WaveFormat WaveFormat
        {
            get
            {
                return SourceStream.WaveFormat;
            }
        }
        private int channel = 0;
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = SourceStream.Read(buffer, offset, count);

            if (ApplyEffects)
            {
                for (int i = 0; i < read / 4; i++)
                {
                    float sample = BitConverter.ToSingle(buffer, i * 4);
                    if (Effects.Count == WaveFormat.Channels)
                    {
                        sample = Effects[channel].ApplyEffect(sample);
                        channel = (channel + 1) % WaveFormat.Channels;
                    }
                    byte[] bytes = BitConverter.GetBytes(sample);
                    buffer[i * 4 + 0] = bytes[0];
                    buffer[i * 4 + 1] = bytes[1];
                    buffer[i * 4 + 2] = bytes[2];
                    buffer[i * 4 + 3] = bytes[3];
                }
            }
            

            return read;
        }
        public void ClearEffects()
        {
            Effects.Clear();
        }


    }
}
