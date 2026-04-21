using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;

namespace OmokGame.UI
{
    internal class Checkbox
    {
        public Rectangle Bounds { get; set; }  // 전체 클릭 영역
        public string Label { get; set; }
        public bool Checked { get; set; }
        public bool IsEnabled { get; set; } = true;

        private bool _prevRawPressed;
        private bool _pressStartedHere;   // 이번 press가 enabled+bounds 내에서 시작했나

        public Checkbox(Rectangle bounds, string label, bool initialValue = false)
        {
            Bounds = bounds;
            Label = label;
            Checked = initialValue;
        }

        public bool Update(MouseState ms)
        {
            bool rawPressed = ms.LeftButton == ButtonState.Pressed;
            bool inBounds   = Bounds.Contains(ms.X, ms.Y);

            // rising edge: enabled & bounds 내에서만 트래킹 시작
            if (rawPressed && !_prevRawPressed && IsEnabled && inBounds)
                _pressStartedHere = true;

            bool toggled = false;
            if (!rawPressed && _prevRawPressed && _pressStartedHere && IsEnabled && inBounds)
            {
                Checked = !Checked;
                toggled = true;
            }

            if (!rawPressed) _pressStartedHere = false;
            _prevRawPressed = rawPressed;
            return toggled;
        }

        public void Draw(SpriteBatch sb, SpriteFontBase font, Texture2D pixel)
        {
            int boxSize = 16;
            var box = new Rectangle(Bounds.X, Bounds.Y + (Bounds.Height - boxSize) / 2, boxSize, boxSize);

            Color labelColor = IsEnabled ? Theme.TextPrimary : Theme.TextDisabled;

            // 외곽선
            sb.Draw(pixel, box, IsEnabled ? Theme.CheckBorder : Theme.TextDisabled);
            sb.Draw(pixel, new Rectangle(box.X + 1, box.Y + 1, box.Width - 2, box.Height - 2),
                    Checked ? (IsEnabled ? Theme.CheckFill : Theme.TextDisabled) : Theme.PanelBg);

            // 체크 마크
            if (Checked)
            {
                sb.Draw(pixel, new Rectangle(box.X + 3, box.Y + 7, 4, 2), labelColor);
                sb.Draw(pixel, new Rectangle(box.X + 6, box.Y + 4, 2, 6), labelColor);
            }

            // 라벨
            font.DrawText(sb, Label,
                new Vector2(Bounds.X + boxSize + 6, Bounds.Y + (Bounds.Height - 18) / 2f),
                labelColor);
        }
    }
}
