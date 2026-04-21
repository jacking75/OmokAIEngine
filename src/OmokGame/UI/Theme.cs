using Microsoft.Xna.Framework;

namespace OmokGame.UI
{
    public enum ThemeMode { LightWood, DarkWood }

    internal static class Theme
    {
        // ── 배경 (테마 전환 대상) ────────────────────────────────────────
        public static Color BoardBg      = new Color(205, 170, 110);
        public static Color GridLine     = new Color(90, 60, 20);
        public static Color PanelBg      = new Color(40, 40, 50);
        public static Color PanelBorder  = new Color(80, 80, 100);
        public static Color WindowBg     = new Color(30, 30, 40);

        // ── 텍스트 ───────────────────────────────────────────────────────
        public static readonly Color TextPrimary  = new Color(230, 230, 240);
        public static readonly Color TextSecond   = new Color(170, 170, 185);
        public static readonly Color TextDisabled = new Color(100, 100, 115);
        public static readonly Color TextAccent   = new Color(120, 200, 255);

        // ── 버튼/슬라이더/체크 ───────────────────────────────────────────
        public static readonly Color BtnNormal    = new Color(60, 100, 160);
        public static readonly Color BtnHover     = new Color(80, 130, 200);
        public static readonly Color BtnPressed   = new Color(40, 70, 130);
        public static readonly Color SliderTrack  = new Color(60, 60, 75);
        public static readonly Color SliderFill   = new Color(80, 160, 220);
        public static readonly Color SliderThumb  = new Color(140, 210, 255);
        public static readonly Color CheckBorder  = new Color(150, 150, 165);
        public static readonly Color CheckFill    = new Color(80, 160, 220);

        // ── 게임 표시 ───────────────────────────────────────────────────
        public static readonly Color WinLine      = new Color(230, 50, 50);
        public static readonly Color WinBg        = new Color(0, 0, 0, 140);
        public static readonly Color HoverBlack   = new Color(0, 0, 0, 100);
        public static readonly Color HoverWhite   = new Color(255, 255, 255, 100);
        public static readonly Color Forbidden    = new Color(255, 80, 80, 160);
        public static readonly Color HintMarker   = new Color(50, 220, 120);

        public static ThemeMode CurrentMode { get; private set; } = ThemeMode.LightWood;

        public static void Apply(ThemeMode mode)
        {
            CurrentMode = mode;
            if (mode == ThemeMode.LightWood)
            {
                BoardBg     = new Color(205, 170, 110);
                GridLine    = new Color(90, 60, 20);
                PanelBg     = new Color(40, 40, 50);
                PanelBorder = new Color(80, 80, 100);
                WindowBg    = new Color(30, 30, 40);
            }
            else
            {
                BoardBg     = new Color(120, 80, 40);
                GridLine    = new Color(20, 10, 0);
                PanelBg     = new Color(20, 20, 28);
                PanelBorder = new Color(60, 60, 80);
                WindowBg    = new Color(10, 10, 18);
            }
        }

        // ── 레이아웃 ────────────────────────────────────────────────────
        public const int WindowW = 1024;
        public const int WindowH = 800;
        public const int BoardAreaW = 716;
        public const int PanelAreaW = 308;

        public const int BoardSize = 15;
        public const int CellSize = 42;
        public const int BoardOffset = 28;
        public const int BoardTop  = 30;

        public static int BoardPixelSize => (BoardSize - 1) * CellSize;

        public static int ColToX(int col) => BoardOffset + col * CellSize;
        public static int RowToY(int row) => BoardTop    + row * CellSize;

        public static int XToCol(int x) => (int)System.Math.Round((x - BoardOffset) / (float)CellSize);
        public static int YToRow(int y) => (int)System.Math.Round((y - BoardTop)    / (float)CellSize);

        public static readonly (int r, int c)[] StarPoints =
        {
            (3,3),(3,11),(7,7),(11,3),(11,11)
        };
    }
}
