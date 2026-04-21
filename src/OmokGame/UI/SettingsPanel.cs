using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using GomokuEngine.AI;
using GomokuEngine.Core;

namespace OmokGame.UI
{
    internal class SettingsPanel
    {
        // PvA에서는 _blackSlider 하나가 'AI 실력'을 표현 (BlackLevel/WhiteLevel 둘 다 같은 값 반환).
        // AvA에서는 _blackSlider=흑 AI, _whiteSlider=백 AI.
        public int      BlackLevel       => _blackSlider.Value;
        public int      WhiteLevel       => Mode == GameMode.AiVsAi ? _whiteSlider.Value : _blackSlider.Value;
        public Stone    PlayerStone      => _playerColorIdx == 0 ? Stone.Black : Stone.White;
        public bool     UseRenju         => _renjuCheck.Checked;
        public bool     ShowMoveNumbers  => _moveNumCheck.Checked;
        public bool     SoundEnabled     => _soundCheck.Checked;
        public GameMode Mode             { get; private set; } = GameMode.PlayerVsAi;

        // 게임 진행 중에는 모드/색상/렌주 변경 잠금
        public bool     CanChangeSettings { get; set; } = true;

        public event Action?  OnNewGame;
        public event Action?  OnUndo;
        public event Action?  OnHint;
        public event Action?  OnSurrender;
        public event Action?  OnSave;
        public event Action?  OnLoad;
        public event Action?  OnThemeToggle;
        public event Action?  OnModeToggle;
        public event Action<bool>? OnSoundToggle;

        private readonly int _x;
        private readonly int _px;

        private readonly Button   _modeBtn;
        private readonly Slider   _blackSlider;
        private readonly Slider   _whiteSlider;
        private readonly Checkbox _renjuCheck;
        private readonly Checkbox _moveNumCheck;
        private readonly Checkbox _soundCheck;
        private readonly Button   _newGameBtn;
        private readonly Button   _surrenderBtn;
        private readonly Button   _hintBtn;
        private readonly Button   _undoBtn;
        private readonly Button   _saveBtn;
        private readonly Button   _loadBtn;
        private readonly Button   _themeBtn;

        private int _playerColorIdx = 0;
        private readonly Rectangle _radioBlack;
        private readonly Rectangle _radioWhite;
        private bool _radioPrevRawPressed;
        private int  _radioPressStartedIdx = -1;   // 이번 press가 어느 라디오에서 시작했나 (0/1, -1=없음)

        public string StatusLine    { get; set; } = "새 게임을 시작하세요";
        public string TurnLine      { get; set; } = "";
        public string AiTimeLine    { get; set; } = "";
        public int    MoveCount     { get; set; } = 0;

        // 외부에서 주입할 컨트롤 활성화 정보
        public bool CanUndo      { get; set; } = false;
        public bool CanHint      { get; set; } = false;
        public bool CanSurrender { get; set; } = false;
        public bool CanSave      { get; set; } = false;

        // 수순 기록 (외부 주입)
        public IReadOnlyList<Position> History { get; set; } = Array.Empty<Position>();

        private string _toastMsg = "";
        private double _toastTimer;
        private const double ToastDur = 2.5;

        public SettingsPanel(int panelX)
        {
            _x  = panelX;
            _px = panelX + 16;
            int w  = Theme.PanelAreaW - 32;
            int hw = (w - 8) / 2;   // 절반 폭
            int y  = 56;

            // 모드 토글 + 테마 토글 (한 행, 절반씩)
            _modeBtn   = new Button(new Rectangle(_px,             y, hw, 30), "모드: 사람 vs AI");
            _themeBtn  = new Button(new Rectangle(_px + hw + 8,    y, hw, 30), "테마: 밝은 나무");
            y += 38;

            // 흑 AI 슬라이더 (PvA에선 'AI 실력'으로 사용; AvA에선 흑 AI)
            // 라벨은 모드/플레이어 색상에 따라 매 프레임 갱신.
            _blackSlider = new Slider(new Rectangle(_px, y, w, 40), "AI 실력", 1, 20, 10);
            y += 64;

            // 백 AI 슬라이더 (AvA 모드 전용 시각적 표시. PvA에서는 색상 라디오 영역으로 사용)
            _whiteSlider = new Slider(new Rectangle(_px, y, w, 40), "AI 실력 (백)", 1, 20, 10);

            // 색상 라디오 (PvA에서 _whiteSlider 자리 위에 겹쳐 표시)
            _radioBlack  = new Rectangle(_px,           y + 22, 90, 28);
            _radioWhite  = new Rectangle(_px + 98,      y + 22, 70, 28);
            y += 64;

            _renjuCheck   = new Checkbox(new Rectangle(_px, y, w, 26), "렌주 금수 규칙 (흑)", false); y += 30;
            _moveNumCheck = new Checkbox(new Rectangle(_px, y, w, 26), "수순 번호 표시", true);       y += 30;
            _soundCheck   = new Checkbox(new Rectangle(_px, y, w, 26), "효과음", true);              y += 36;

            // 액션 버튼 3행 × 2열
            _newGameBtn   = new Button(new Rectangle(_px,          y, hw, 30), "새 게임");
            _surrenderBtn = new Button(new Rectangle(_px + hw + 8, y, hw, 30), "항복");
            y += 36;
            _hintBtn      = new Button(new Rectangle(_px,          y, hw, 30), "힌트");
            _undoBtn      = new Button(new Rectangle(_px + hw + 8, y, hw, 30), "무르기");
            y += 36;
            _saveBtn      = new Button(new Rectangle(_px,          y, hw, 30), "저장");
            _loadBtn      = new Button(new Rectangle(_px + hw + 8, y, hw, 30), "불러오기");
        }

        public void ShowToast(string msg) { _toastMsg = msg; _toastTimer = ToastDur; }

        public void Update(MouseState ms, double dt)
        {
            _toastTimer = Math.Max(0, _toastTimer - dt);

            // B6: 게임 진행 중에는 모드/색상/렌주 변경 불가
            _modeBtn.IsEnabled    = CanChangeSettings;
            _renjuCheck.IsEnabled = CanChangeSettings;

            if (_modeBtn.Update(ms))
            {
                Mode = Mode == GameMode.PlayerVsAi ? GameMode.AiVsAi : GameMode.PlayerVsAi;
                _modeBtn.Text = Mode == GameMode.PlayerVsAi ? "모드: 사람 vs AI" : "모드: AI vs AI";
                OnModeToggle?.Invoke();
            }
            if (_themeBtn.Update(ms))
            {
                OnThemeToggle?.Invoke();
                _themeBtn.Text = Theme.CurrentMode == ThemeMode.LightWood ? "테마: 밝은 나무" : "테마: 짙은 나무";
            }

            // B5: 슬라이더 라벨 동적 갱신 (B12: 모드 토글 후에 실행해 같은 프레임에 반영)
            if (Mode == GameMode.AiVsAi)
            {
                _blackSlider.Label = "흑 AI 실력";
                _whiteSlider.Label = "백 AI 실력";
            }
            else
            {
                _blackSlider.Label = "AI 실력";
            }

            _blackSlider.Update(ms);
            if (Mode == GameMode.AiVsAi) _whiteSlider.Update(ms);

            _renjuCheck.Update(ms);
            _moveNumCheck.Update(ms);
            if (_soundCheck.Update(ms)) OnSoundToggle?.Invoke(_soundCheck.Checked);

            // 라디오 (PvA에서만, 그리고 설정 변경 가능 시에만)
            // edge-detection: press가 enabled+라디오 영역 내에서 시작했고, 같은 영역에서 release되어야 토글
            {
                bool rawPressed = ms.LeftButton == ButtonState.Pressed;
                bool trackable  = Mode == GameMode.PlayerVsAi && CanChangeSettings;

                if (rawPressed && !_radioPrevRawPressed && trackable)
                {
                    if      (_radioBlack.Contains(ms.X, ms.Y)) _radioPressStartedIdx = 0;
                    else if (_radioWhite.Contains(ms.X, ms.Y)) _radioPressStartedIdx = 1;
                }

                if (!rawPressed && _radioPrevRawPressed && trackable && _radioPressStartedIdx >= 0)
                {
                    var rect = _radioPressStartedIdx == 0 ? _radioBlack : _radioWhite;
                    if (rect.Contains(ms.X, ms.Y)) _playerColorIdx = _radioPressStartedIdx;
                }

                if (!rawPressed) _radioPressStartedIdx = -1;
                _radioPrevRawPressed = rawPressed;
            }

            // 활성/비활성
            _surrenderBtn.IsEnabled = CanSurrender;
            _hintBtn.IsEnabled      = CanHint;
            _undoBtn.IsEnabled      = CanUndo;
            _saveBtn.IsEnabled      = CanSave;

            if (_newGameBtn.Update(ms))   OnNewGame?.Invoke();
            if (_surrenderBtn.Update(ms)) OnSurrender?.Invoke();
            if (_hintBtn.Update(ms))      OnHint?.Invoke();
            if (_undoBtn.Update(ms))      OnUndo?.Invoke();
            if (_saveBtn.Update(ms))      OnSave?.Invoke();
            if (_loadBtn.Update(ms))      OnLoad?.Invoke();
        }

        public void Draw(SpriteBatch sb, SpriteFontBase font, SpriteFontBase small, Texture2D px)
        {
            sb.Draw(px, new Rectangle(_x, 0, Theme.PanelAreaW, Theme.WindowH), Theme.PanelBg);
            sb.Draw(px, new Rectangle(_x, 0, 1, Theme.WindowH), Theme.PanelBorder);

            int y = 10;
            font.DrawText(sb, "오목 AI 설정", new Vector2(_px, y), Theme.TextAccent); y += 28;
            HLine(sb, px, y); y += 8;

            // 모드 / 테마
            _modeBtn.Draw(sb, small, px);
            _themeBtn.Draw(sb, small, px);

            // 슬라이더
            _blackSlider.Draw(sb, font, px);

            // 흑 슬라이더 단계 이름 (PvA: AI 프로파일 / AvA: 흑 AI 프로파일)
            int blackLv = Math.Clamp(_blackSlider.Value, 1, 20);
            string blackProfileName = LevelProfile.Profiles[blackLv - 1].Name;
            small.DrawText(sb, blackProfileName,
                new Vector2(_px + 4, _blackSlider.Bounds.Bottom + 16),
                Theme.TextSecond);

            if (Mode == GameMode.AiVsAi)
            {
                _whiteSlider.Draw(sb, font, px);

                // 백 AI 프로파일 (B10)
                int whiteLv = Math.Clamp(_whiteSlider.Value, 1, 20);
                string whiteProfileName = LevelProfile.Profiles[whiteLv - 1].Name;
                small.DrawText(sb, whiteProfileName,
                    new Vector2(_px + 4, _whiteSlider.Bounds.Bottom + 16),
                    Theme.TextSecond);
            }
            else
            {
                // 색상 라디오 (B6: 게임 중에는 흐리게 + 클릭 무시)
                Color labelCol = CanChangeSettings ? Theme.TextPrimary : Theme.TextDisabled;
                font.DrawText(sb, "플레이어 색상", new Vector2(_px, _whiteSlider.Bounds.Y), labelCol);
                DrawRadio(sb, font, px, _radioBlack, "흑 (선공)", _playerColorIdx == 0, CanChangeSettings);
                DrawRadio(sb, font, px, _radioWhite, "백",        _playerColorIdx == 1, CanChangeSettings);
            }

            _renjuCheck.Draw(sb, font, px);
            _moveNumCheck.Draw(sb, font, px);
            _soundCheck.Draw(sb, font, px);

            HLine(sb, px, _newGameBtn.Bounds.Y - 6);

            _newGameBtn.Draw(sb, small, px);
            _surrenderBtn.Draw(sb, small, px);
            _hintBtn.Draw(sb, small, px);
            _undoBtn.Draw(sb, small, px);
            _saveBtn.Draw(sb, small, px);
            _loadBtn.Draw(sb, small, px);

            int sectionY = _loadBtn.Bounds.Bottom + 10;
            HLine(sb, px, sectionY); sectionY += 8;

            // 상태
            font.DrawText(sb, "게임 상태", new Vector2(_px, sectionY), Theme.TextAccent); sectionY += 24;
            if (TurnLine.Length > 0)   { small.DrawText(sb, TurnLine,   new Vector2(_px, sectionY), Theme.TextPrimary); sectionY += 18; }
            small.DrawText(sb, $"수순: {MoveCount}수", new Vector2(_px, sectionY), Theme.TextSecond); sectionY += 18;
            if (AiTimeLine.Length > 0) { small.DrawText(sb, AiTimeLine, new Vector2(_px, sectionY), Theme.TextSecond); sectionY += 18; }
            small.DrawText(sb, StatusLine, new Vector2(_px, sectionY), Theme.TextPrimary); sectionY += 24;

            HLine(sb, px, sectionY); sectionY += 8;

            // 수순 기록 (최근 18수, 2열로)
            font.DrawText(sb, "수순 기록", new Vector2(_px, sectionY), Theme.TextAccent); sectionY += 22;
            DrawHistory(sb, small, sectionY);

            // 토스트 (가장 위에)
            if (_toastTimer > 0)
            {
                float a = (float)Math.Min(1.0, _toastTimer);
                var sz = small.MeasureString(_toastMsg);
                int ty = Theme.WindowH - 40;
                sb.Draw(px, new Rectangle(_px - 4, ty - 4, (int)sz.X + 8, (int)sz.Y + 8), Theme.Forbidden * a);
                small.DrawText(sb, _toastMsg, new Vector2(_px, ty), Color.White * a);
            }
        }

        private void DrawHistory(SpriteBatch sb, SpriteFontBase small, int startY)
        {
            int total = History.Count;
            const int maxRows = 9;
            int firstShown = Math.Max(0, total - maxRows * 2);
            int colW = (Theme.PanelAreaW - 32) / 2;

            for (int i = firstShown; i < total; i++)
            {
                int idx = i - firstShown;
                int row = idx % maxRows;
                int col = idx / maxRows;
                var p = History[i];
                string s = $"{i + 1,3}. {(char)('A' + p.Col)}{p.Row + 1}";
                Color c = (i % 2 == 0) ? Theme.TextPrimary : Theme.TextSecond;
                small.DrawText(sb, s, new Vector2(_px + col * colW, startY + row * 16), c);
            }
        }

        private void HLine(SpriteBatch sb, Texture2D px, int y) =>
            sb.Draw(px, new Rectangle(_px, y, Theme.PanelAreaW - 32, 1), Theme.PanelBorder);

        private static void DrawRadio(SpriteBatch sb, SpriteFontBase font, Texture2D px,
                                      Rectangle bounds, string text, bool selected, bool enabled = true)
        {
            int r = 8, cx = bounds.X + r, cy = bounds.Y + bounds.Height / 2;
            Color border = enabled ? Theme.CheckBorder : Theme.TextDisabled;
            Color fill   = enabled ? Theme.CheckFill   : Theme.TextDisabled;
            Color label  = enabled ? Theme.TextPrimary : Theme.TextDisabled;
            FillCircle(sb, px, cx, cy, r,     border);
            FillCircle(sb, px, cx, cy, r - 1, Theme.PanelBg);
            if (selected) FillCircle(sb, px, cx, cy, r - 3, fill);
            font.DrawText(sb, text, new Vector2(cx + r + 4, cy - 9f), label);
        }

        private static void FillCircle(SpriteBatch sb, Texture2D px, int cx, int cy, int r, Color c)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r)
                        sb.Draw(px, new Rectangle(cx + dx, cy + dy, 1, 1), c);
        }
    }
}
