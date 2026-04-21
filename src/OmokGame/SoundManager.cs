using System;
using Microsoft.Xna.Framework.Audio;

namespace OmokGame
{
    /// <summary>
    /// 외부 WAV 파일 없이 PCM 데이터를 즉시 합성해 SoundEffect를 만든다.
    /// 착수 클릭(짧은 노이즈 + 감쇠), 승리 차임(상승 톤).
    /// </summary>
    internal class SoundManager : IDisposable
    {
        private readonly SoundEffect? _click;
        private readonly SoundEffect? _win;
        public bool Enabled { get; set; } = true;

        public SoundManager()
        {
            // 오디오 디바이스가 없거나 SoundEffect 생성이 실패해도 게임은 계속 동작해야 한다.
            try { _click = MakeClick(); }     catch { _click = null; }
            try { _win   = MakeWinChime(); }  catch { _win = null; }
        }

        public void PlayClick() { if (Enabled && _click != null) try { _click.Play(0.5f, 0f, 0f); } catch { } }
        public void PlayWin()   { if (Enabled && _win   != null) try { _win.Play(0.7f, 0f, 0f);   } catch { } }

        // 짧은 노이즈 + 빠른 감쇠 (50ms)
        private static SoundEffect MakeClick()
        {
            const int sampleRate = 22050;
            int samples = sampleRate / 20;   // 50ms
            byte[] buffer = new byte[samples * 2];
            var rng = new Random(7);
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)sampleRate;
                double env = Math.Exp(-t * 80);              // 빠른 감쇠
                double noise = (rng.NextDouble() * 2 - 1);
                double tone  = Math.Sin(2 * Math.PI * 1200 * t) * 0.3;
                short v = (short)(short.MaxValue * 0.6 * env * (noise * 0.7 + tone));
                buffer[i * 2]     = (byte)(v & 0xff);
                buffer[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }
            return new SoundEffect(buffer, sampleRate, AudioChannels.Mono);
        }

        // 상승 두 음 차임 (300ms)
        private static SoundEffect MakeWinChime()
        {
            const int sampleRate = 22050;
            int samples = sampleRate * 3 / 10;   // 300ms
            byte[] buffer = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)sampleRate;
                double env = Math.Exp(-t * 5);
                double f = t < 0.15 ? 660 : 990;
                double s = Math.Sin(2 * Math.PI * f * t);
                short v = (short)(short.MaxValue * 0.4 * env * s);
                buffer[i * 2]     = (byte)(v & 0xff);
                buffer[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }
            return new SoundEffect(buffer, sampleRate, AudioChannels.Mono);
        }

        public void Dispose()
        {
            _click?.Dispose();
            _win?.Dispose();
        }
    }
}
