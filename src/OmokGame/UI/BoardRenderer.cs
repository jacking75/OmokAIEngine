using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using GomokuEngine.Core;

namespace OmokGame.UI
{
    internal class BoardRenderer
    {
        private readonly Texture2D _pixel;
        private readonly Texture2D _stoneBlack;
        private readonly Texture2D _stoneWhite;
        private readonly Texture2D _lastMoveMarker;
        private readonly Texture2D _starPoint;
        private readonly SpriteFontBase _font;

        public int HoverRow { get; set; } = -1;
        public int HoverCol { get; set; } = -1;
        public Stone HoverStone { get; set; } = Stone.Black;

        public List<(int r, int c)> ForbiddenCells { get; } = new();
        public (int r1, int c1, int r2, int c2)? WinLine { get; set; }
        public bool ShowMoveNumbers { get; set; } = true;

        // 힌트 마커 (null이면 표시 안 함)
        public Position? HintPosition { get; set; }

        // 페이드 인용 시각 정보 (Game1에서 매 프레임 갱신)
        public Dictionary<(int r, int c), double>? PlaceTimes { get; set; }
        public double Now { get; set; }
        public const double FadeDurationSec = 0.18;

        // 힌트 펄스 애니메이션
        public double PulsePhase { get; set; }

        public BoardRenderer(Texture2D pixel, Texture2D stoneBlack, Texture2D stoneWhite,
                             Texture2D lastMoveMarker, Texture2D starPoint, SpriteFontBase font)
        {
            _pixel = pixel;
            _stoneBlack = stoneBlack;
            _stoneWhite = stoneWhite;
            _lastMoveMarker = lastMoveMarker;
            _starPoint = starPoint;
            _font = font;
        }

        public void Draw(SpriteBatch sb, Stone[,] board, List<Position> history)
        {
            sb.Draw(_pixel, new Rectangle(0, 0, Theme.BoardAreaW, Theme.WindowH), Theme.BoardBg);

            DrawGrid(sb);
            DrawStarPoints(sb);
            DrawCoordinates(sb);
            DrawStones(sb, board, history);
            DrawHover(sb, board);
            DrawForbidden(sb);
            DrawHint(sb);
            DrawWinLine(sb);
        }

        private void DrawGrid(SpriteBatch sb)
        {
            int size = Theme.BoardSize;
            for (int i = 0; i < size; i++)
            {
                int y = Theme.RowToY(i);
                sb.Draw(_pixel, new Rectangle(Theme.ColToX(0), y, (size - 1) * Theme.CellSize, 1), Theme.GridLine);
                int x = Theme.ColToX(i);
                sb.Draw(_pixel, new Rectangle(x, Theme.RowToY(0), 1, (size - 1) * Theme.CellSize), Theme.GridLine);
            }
        }

        private void DrawStarPoints(SpriteBatch sb)
        {
            foreach (var (r, c) in Theme.StarPoints)
            {
                int x = Theme.ColToX(c) - _starPoint.Width / 2;
                int y = Theme.RowToY(r) - _starPoint.Height / 2;
                sb.Draw(_starPoint, new Rectangle(x, y, _starPoint.Width, _starPoint.Height), Color.White);
            }
        }

        private void DrawCoordinates(SpriteBatch sb)
        {
            for (int i = 0; i < Theme.BoardSize; i++)
            {
                string colLabel = ((char)('A' + i)).ToString();
                var colSize = _font.MeasureString(colLabel);
                float cx = Theme.ColToX(i) - colSize.X / 2f;
                _font.DrawText(sb, colLabel, new Vector2(cx, Theme.BoardTop - 22), Theme.TextSecond);
                _font.DrawText(sb, colLabel, new Vector2(cx, Theme.BoardTop + Theme.BoardPixelSize + 6), Theme.TextSecond);

                string rowLabel = (i + 1).ToString();
                var rowSize = _font.MeasureString(rowLabel);
                float ry = Theme.RowToY(i) - rowSize.Y / 2f;
                _font.DrawText(sb, rowLabel, new Vector2(Theme.BoardOffset - 22 - rowSize.X / 2f, ry), Theme.TextSecond);
                _font.DrawText(sb, rowLabel, new Vector2(Theme.BoardOffset + Theme.BoardPixelSize + 6, ry), Theme.TextSecond);
            }
        }

        private void DrawStones(SpriteBatch sb, Stone[,] board, List<Position> history)
        {
            int stoneR = Theme.CellSize / 2 - 2;

            var moveNums = new Dictionary<(int, int), int>();
            if (ShowMoveNumbers)
                for (int i = 0; i < history.Count; i++)
                    moveNums[(history[i].Row, history[i].Col)] = i + 1;

            Position? last = history.Count > 0 ? history[history.Count - 1] : null;

            for (int r = 0; r < Theme.BoardSize; r++)
            {
                for (int c = 0; c < Theme.BoardSize; c++)
                {
                    Stone s = board[r, c];
                    if (s == Stone.Empty) continue;

                    int cx = Theme.ColToX(c);
                    int cy = Theme.RowToY(r);

                    // 페이드 인 알파
                    float alpha = 1f;
                    if (PlaceTimes != null && PlaceTimes.TryGetValue((r, c), out double t0))
                    {
                        double dt = Now - t0;
                        if (dt < FadeDurationSec)
                            alpha = (float)(dt / FadeDurationSec);
                    }

                    Texture2D tex = s == Stone.Black ? _stoneBlack : _stoneWhite;
                    sb.Draw(tex,
                        new Rectangle(cx - stoneR, cy - stoneR, stoneR * 2, stoneR * 2),
                        Color.White * alpha);

                    if (last.HasValue && last.Value.Row == r && last.Value.Col == c && alpha >= 0.99f)
                    {
                        int mw = _lastMoveMarker.Width;
                        int mh = _lastMoveMarker.Height;
                        sb.Draw(_lastMoveMarker,
                            new Rectangle(cx - mw / 2, cy - mh / 2, mw, mh),
                            Color.White);
                    }

                    if (ShowMoveNumbers && moveNums.TryGetValue((r, c), out int num) && alpha >= 0.99f)
                    {
                        string ns = num.ToString();
                        var nsz = _font.MeasureString(ns);
                        Color numColor = s == Stone.Black ? Color.White : Color.Black;
                        _font.DrawText(sb, ns,
                            new Vector2(cx - nsz.X / 2f, cy - nsz.Y / 2f),
                            numColor * 0.85f);
                    }
                }
            }
        }

        private void DrawHover(SpriteBatch sb, Stone[,] board)
        {
            if (HoverRow < 0 || HoverCol < 0) return;
            if (HoverRow >= Theme.BoardSize || HoverCol >= Theme.BoardSize) return;
            if (board[HoverRow, HoverCol] != Stone.Empty) return;

            int cx = Theme.ColToX(HoverCol);
            int cy = Theme.RowToY(HoverRow);
            int r = Theme.CellSize / 2 - 2;
            Texture2D hoverTex = HoverStone == Stone.White ? _stoneWhite : _stoneBlack;
            Color hoverColor = HoverStone == Stone.White ? Theme.HoverWhite : Theme.HoverBlack;
            sb.Draw(hoverTex, new Rectangle(cx - r, cy - r, r * 2, r * 2), hoverColor);
        }

        private void DrawForbidden(SpriteBatch sb)
        {
            foreach (var (r, c) in ForbiddenCells)
            {
                int cx = Theme.ColToX(c);
                int cy = Theme.RowToY(r);
                int half = 10;
                sb.Draw(_pixel, new Rectangle(cx - half, cy - half, half * 2, half * 2), Theme.Forbidden);
                sb.Draw(_pixel, new Rectangle(cx - half, cy - 1, half * 2, 2), Theme.WinLine);
                sb.Draw(_pixel, new Rectangle(cx - 1, cy - half, 2, half * 2), Theme.WinLine);
            }
        }

        private void DrawHint(SpriteBatch sb)
        {
            if (!HintPosition.HasValue) return;
            var p = HintPosition.Value;
            int cx = Theme.ColToX(p.Col);
            int cy = Theme.RowToY(p.Row);

            // 펄스 효과: 14~20px 사이 변동
            float pulse = (float)(0.5 + 0.5 * Math.Sin(PulsePhase));
            int half = (int)(14 + pulse * 6);

            // 외곽 사각 테두리 (얇은 4면)
            sb.Draw(_pixel, new Rectangle(cx - half, cy - half, half * 2, 2), Theme.HintMarker);
            sb.Draw(_pixel, new Rectangle(cx - half, cy + half - 2, half * 2, 2), Theme.HintMarker);
            sb.Draw(_pixel, new Rectangle(cx - half, cy - half, 2, half * 2), Theme.HintMarker);
            sb.Draw(_pixel, new Rectangle(cx + half - 2, cy - half, 2, half * 2), Theme.HintMarker);
        }

        private void DrawWinLine(SpriteBatch sb)
        {
            if (!WinLine.HasValue) return;
            var (r1, c1, r2, c2) = WinLine.Value;
            int x1 = Theme.ColToX(c1);
            int y1 = Theme.RowToY(r1);
            int x2 = Theme.ColToX(c2);
            int y2 = Theme.RowToY(r2);

            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;
            int x = x1, y = y1;

            while (true)
            {
                sb.Draw(_pixel, new Rectangle(x - 2, y - 2, 4, 4), Theme.WinLine);
                if (x == x2 && y == y2) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx)  { err += dx; y += sy; }
            }
        }
    }
}
