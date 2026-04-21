using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;

namespace OmokGame.UI
{
    internal class Button
    {
        public Rectangle Bounds { get; set; }
        public string Text { get; set; }
        public bool IsEnabled { get; set; } = true;

        private bool _hovered;
        private bool _pressed;

        public Button(Rectangle bounds, string text)
        {
            Bounds = bounds;
            Text = text;
        }

        public bool Update(MouseState ms)
        {
            bool wasPressed = _pressed;
            _hovered = IsEnabled && Bounds.Contains(ms.X, ms.Y);
            _pressed = _hovered && ms.LeftButton == ButtonState.Pressed;
            return !_pressed && wasPressed && _hovered && IsEnabled;  // click
        }

        public void Draw(SpriteBatch sb, SpriteFontBase font, Texture2D pixel)
        {
            Color bg = !IsEnabled ? Theme.TextDisabled
                     : _pressed   ? Theme.BtnPressed
                     : _hovered   ? Theme.BtnHover
                                  : Theme.BtnNormal;

            DrawRect(sb, pixel, Bounds, bg);
            DrawRect(sb, pixel, new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, 1), Color.White * 0.3f);
            DrawRect(sb, pixel, new Rectangle(Bounds.X, Bounds.Bottom - 1, Bounds.Width, 1), Color.Black * 0.3f);

            var size = font.MeasureString(Text);
            var textPos = new Vector2(
                Bounds.X + (Bounds.Width - size.X) / 2f,
                Bounds.Y + (Bounds.Height - size.Y) / 2f);
            font.DrawText(sb, Text, textPos, IsEnabled ? Theme.TextPrimary : Theme.TextDisabled);
        }

        private static void DrawRect(SpriteBatch sb, Texture2D pixel, Rectangle r, Color c)
        {
            sb.Draw(pixel, r, c);
        }
    }
}
