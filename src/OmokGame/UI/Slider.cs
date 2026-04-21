using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;

namespace OmokGame.UI
{
    internal class Slider
    {
        public Rectangle Bounds { get; set; }
        public string Label { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public int Value { get; set; }

        private bool _dragging;

        public Slider(Rectangle bounds, string label, int min, int max, int value)
        {
            Bounds = bounds;
            Label = label;
            Min = min;
            Max = max;
            Value = Math.Clamp(value, min, max);
        }

        public bool Update(MouseState ms)
        {
            int old = Value;

            if (_dragging && ms.LeftButton == ButtonState.Released)
                _dragging = false;

            var trackRect = GetTrackRect();
            if (ms.LeftButton == ButtonState.Pressed)
            {
                if (!_dragging && trackRect.Contains(ms.X, ms.Y))
                    _dragging = true;

                if (_dragging)
                {
                    float ratio = (float)(ms.X - trackRect.X) / trackRect.Width;
                    Value = Math.Clamp((int)Math.Round(Min + ratio * (Max - Min)), Min, Max);
                }
            }

            return Value != old;
        }

        public void Draw(SpriteBatch sb, SpriteFontBase font, Texture2D pixel)
        {
            var track = GetTrackRect();

            // 라벨 + 값
            string text = $"{Label}: {Value}";
            font.DrawText(sb, text, new Vector2(Bounds.X, Bounds.Y), Theme.TextPrimary);

            // 트랙 배경
            sb.Draw(pixel, track, Theme.SliderTrack);

            // 채워진 부분
            float ratio = (Max > Min) ? (float)(Value - Min) / (Max - Min) : 0f;
            var filled = new Rectangle(track.X, track.Y, (int)(track.Width * ratio), track.Height);
            sb.Draw(pixel, filled, Theme.SliderFill);

            // 썸
            int thumbX = track.X + (int)(track.Width * ratio) - 5;
            var thumb = new Rectangle(thumbX, track.Y - 4, 10, track.Height + 8);
            sb.Draw(pixel, thumb, Theme.SliderThumb);
        }

        private Rectangle GetTrackRect()
        {
            int labelH = 22;
            return new Rectangle(Bounds.X, Bounds.Y + labelH + 4, Bounds.Width, 8);
        }
    }
}
