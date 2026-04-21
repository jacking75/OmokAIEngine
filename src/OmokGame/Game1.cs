using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using OmokGame.UI;
using GomokuEngine.AI;
using GomokuEngine.Core;

namespace OmokGame
{
    public class Game1 : Game
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowTextW")]
        private static extern bool SetWindowTextW(IntPtr hwnd, string text);

        private bool _titleFixed;

        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;

        private Texture2D _pixel = null!;
        private Texture2D _stoneBlack = null!;
        private Texture2D _stoneWhite = null!;
        private Texture2D _lastMoveMarker = null!;
        private Texture2D _starPoint = null!;

        private FontSystem _fontSystem = null!;
        private SpriteFontBase _fontNormal = null!;
        private SpriteFontBase _fontSmall = null!;

        private BoardRenderer _boardRenderer = null!;
        private SettingsPanel _settingsPanel = null!;

        private GameSession _session = new GameSession();
        private SoundManager _sound = null!;

        private MouseState _prevMouse;
        private int _hoverRow = -1, _hoverCol = -1;
        private double _spinnerAngle;
        private double _now;

        private static readonly string SaveDir =
            Path.Combine(AppContext.BaseDirectory, "saves");
        private static readonly string QuickSavePath =
            Path.Combine(SaveDir, "quicksave.json");

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth  = Theme.WindowW,
                PreferredBackBufferHeight = Theme.WindowH,
                IsFullScreen = false,
                SynchronizeWithVerticalRetrace = true
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.Title = "OmokAI";
        }

        protected override void Initialize()
        {
            base.Initialize();
            _session.OnToast      += msg => _settingsPanel?.ShowToast(msg);
            _session.OnStonePlaced += (_, _) => _sound?.PlayClick();
            _session.OnGameWon     += _      => _sound?.PlayWin();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _stoneBlack     = LoadTexture("stone_black.png");
            _stoneWhite     = LoadTexture("stone_white.png");
            _lastMoveMarker = LoadTexture("last_move_marker.png");
            _starPoint      = LoadTexture("star_point.png");

            _fontSystem = new FontSystem();
            _fontSystem.AddFont(File.ReadAllBytes(FindFontPath()));
            _fontNormal = _fontSystem.GetFont(18);
            _fontSmall  = _fontSystem.GetFont(14);

            _boardRenderer = new BoardRenderer(
                _pixel, _stoneBlack, _stoneWhite,
                _lastMoveMarker, _starPoint, _fontSmall);

            _settingsPanel = new SettingsPanel(Theme.BoardAreaW);
            _settingsPanel.OnNewGame     += OnNewGame;
            _settingsPanel.OnUndo        += () => { _session.Undo(); };
            _settingsPanel.OnHint        += () => { if (!_session.RequestHint()) _settingsPanel.ShowToast("힌트 사용 불가"); };
            _settingsPanel.OnSurrender   += () => { _session.Surrender(); _settingsPanel.ShowToast("항복했습니다"); };
            _settingsPanel.OnSave        += OnSave;
            _settingsPanel.OnLoad        += OnLoad;
            _settingsPanel.OnThemeToggle += () => Theme.Apply(Theme.CurrentMode == ThemeMode.LightWood ? ThemeMode.DarkWood : ThemeMode.LightWood);
            _settingsPanel.OnModeToggle  += () => _settingsPanel.ShowToast(_settingsPanel.Mode == GameMode.AiVsAi ? "AI vs AI 모드" : "사람 vs AI 모드");
            _settingsPanel.OnSoundToggle += enabled => _sound.Enabled = enabled;

            _sound = new SoundManager();
        }

        private static string ContentPath(string filename) =>
            Path.Combine(AppContext.BaseDirectory, "Content", filename);

        private Texture2D LoadTexture(string filename)
        {
            using var stream = File.OpenRead(ContentPath(filename));
            return Texture2D.FromStream(GraphicsDevice, stream);
        }

        private static string FindFontPath()
        {
            string bundled = ContentPath("malgun.ttf");
            if (File.Exists(bundled)) return bundled;

            string[] system = {
                @"C:\Windows\Fonts\malgun.ttf",
                @"C:\Windows\Fonts\arial.ttf",
                @"C:\Windows\Fonts\segoeui.ttf",
            };
            foreach (var f in system)
                if (File.Exists(f)) return f;
            throw new FileNotFoundException("폰트 파일을 찾을 수 없습니다.");
        }

        private void OnNewGame()
        {
            _session.StartNewGame(
                _settingsPanel.Mode,
                _settingsPanel.BlackLevel,
                _settingsPanel.WhiteLevel,
                _settingsPanel.PlayerStone,
                _settingsPanel.UseRenju,
                _settingsPanel.ShowMoveNumbers);

            _boardRenderer.WinLine = null;
            _boardRenderer.ForbiddenCells.Clear();
            _boardRenderer.HintPosition = null;
            UpdatePanelStatus();
        }

        private void OnSave()
        {
            try
            {
                _session.Save(QuickSavePath);
                _settingsPanel.ShowToast("저장 완료");
            }
            catch (Exception ex) { _settingsPanel.ShowToast($"저장 실패: {ex.Message}"); }
        }

        private void OnLoad()
        {
            try
            {
                if (_session.Load(QuickSavePath))
                {
                    _settingsPanel.ShowToast("불러오기 완료");
                    _boardRenderer.HintPosition = null;
                }
                else _settingsPanel.ShowToast("저장 파일 없음");
            }
            catch (Exception ex) { _settingsPanel.ShowToast($"불러오기 실패: {ex.Message}"); }
        }

        protected override void Update(GameTime gameTime)
        {
            if (!_titleFixed)
            {
                _titleFixed = true;
                var hwnd = Process.GetCurrentProcess().MainWindowHandle;
                if (hwnd != IntPtr.Zero)
                    SetWindowTextW(hwnd, "오목 AI 시연");
            }

            double dt = gameTime.ElapsedGameTime.TotalSeconds;
            _spinnerAngle += dt * 4.0;
            _now += dt;
            _session.Now = _now;
            _boardRenderer.Now = _now;
            _boardRenderer.PlaceTimes = _session.PlaceTimes;
            _boardRenderer.PulsePhase += dt * 4.0;

            var ms = Mouse.GetState();

            _settingsPanel.CanUndo           = _session.CanUndo();
            _settingsPanel.CanHint           = _session.Phase == GamePhase.PlayerTurn;
            _settingsPanel.CanSurrender      = _session.Phase == GamePhase.PlayerTurn || _session.Phase == GamePhase.AiThinking;
            _settingsPanel.CanSave           = _session.Phase != GamePhase.WaitingNewGame;
            _settingsPanel.CanChangeSettings = _session.Phase == GamePhase.WaitingNewGame || _session.Phase == GamePhase.GameOver;
            _settingsPanel.History           = _session.History;

            _settingsPanel.Update(ms, dt);

            _session.Update();
            SyncBoardRenderer();

            // 마우스 → 교점
            _hoverRow = _hoverCol = -1;
            if (ms.X < Theme.BoardAreaW)
            {
                int r = Theme.YToRow(ms.Y);
                int c = Theme.XToCol(ms.X);
                if (r >= 0 && r < Theme.BoardSize && c >= 0 && c < Theme.BoardSize)
                {
                    _hoverRow = r;
                    _hoverCol = c;
                }
            }
            _boardRenderer.HoverRow   = _hoverRow;
            _boardRenderer.HoverCol   = _hoverCol;
            _boardRenderer.HoverStone = _session.PlayerStone;

            // 클릭 (TryPlayerMove 내부에서 ClearHint 처리됨)
            if (ms.LeftButton == ButtonState.Released &&
                _prevMouse.LeftButton == ButtonState.Pressed &&
                _hoverRow >= 0 && _session.Phase == GamePhase.PlayerTurn)
            {
                _session.TryPlayerMove(_hoverRow, _hoverCol);
                SyncBoardRenderer();
            }

            _prevMouse = ms;
            UpdatePanelStatus();
            base.Update(gameTime);
        }

        private void SyncBoardRenderer()
        {
            _boardRenderer.WinLine = _session.WinLine;
            _boardRenderer.ForbiddenCells.Clear();
            foreach (var fc in _session.ForbiddenCells)
                _boardRenderer.ForbiddenCells.Add(fc);
            _boardRenderer.ShowMoveNumbers = _settingsPanel.ShowMoveNumbers;
            _boardRenderer.HintPosition = _session.HintPosition;
        }

        private void UpdatePanelStatus()
        {
            _settingsPanel.MoveCount = _session.MoveCount;

            if (_session.IsAiThinking)
                _settingsPanel.AiTimeLine = $"AI 사고: {_session.AiElapsedMs / 1000.0:F1}초";
            else
                _settingsPanel.AiTimeLine = "";

            switch (_session.Phase)
            {
                case GamePhase.WaitingNewGame:
                    _settingsPanel.TurnLine = "";
                    _settingsPanel.StatusLine = "새 게임 버튼을 눌러주세요";
                    break;
                case GamePhase.PlayerTurn:
                    _settingsPanel.TurnLine = $"{StoneLabel(_session.PlayerStone)} 차례";
                    _settingsPanel.StatusLine = "클릭하여 착수";
                    break;
                case GamePhase.AiThinking:
                    if (_session.Mode == GameMode.AiVsAi)
                    {
                        _settingsPanel.TurnLine = $"{StoneLabel(_session.CurrentTurn)} AI 사고 중";
                        _settingsPanel.StatusLine = "AI vs AI 관전";
                    }
                    else
                    {
                        _settingsPanel.TurnLine = $"{StoneLabel(_session.AiStone)} 차례 (AI)";
                        _settingsPanel.StatusLine = "AI 사고 중...";
                    }
                    break;
                case GamePhase.GameOver:
                    if (_session.IsWon)
                    {
                        _settingsPanel.TurnLine = "게임 종료";
                        if (_session.Mode == GameMode.AiVsAi)
                            _settingsPanel.StatusLine = $"{StoneLabel(_session.Winner)} AI 승리!";
                        else
                        {
                            bool playerWon = _session.Winner == _session.PlayerStone;
                            _settingsPanel.StatusLine = playerWon
                                ? $"{StoneLabel(_session.PlayerStone)} 승리!"
                                : $"AI ({StoneLabel(_session.AiStone)}) 승리!";
                        }
                    }
                    else
                    {
                        _settingsPanel.TurnLine = "게임 종료";
                        _settingsPanel.StatusLine = "무승부 (보드 가득 참)";
                    }
                    break;
            }
        }

        private static string StoneLabel(Stone s) => s == Stone.Black ? "흑" : "백";

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Theme.WindowBg);
            _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);

            _boardRenderer.Draw(_spriteBatch, _session.UiBoard, _session.History);

            if (_session.Phase == GamePhase.AiThinking) DrawSpinner();
            if (_session.Phase == GamePhase.GameOver)   DrawGameOverOverlay();

            _settingsPanel.Draw(_spriteBatch, _fontNormal, _fontSmall, _pixel);

            _spriteBatch.End();
            base.Draw(gameTime);
        }

        private void DrawSpinner()
        {
            int cx = Theme.BoardAreaW / 2;
            int cy = Theme.WindowH - 30;
            int r = 10;
            for (int i = 0; i < 8; i++)
            {
                double angle = _spinnerAngle + i * Math.PI / 4;
                int sx = cx + (int)(Math.Cos(angle) * r);
                int sy = cy + (int)(Math.Sin(angle) * r);
                float alpha = 0.2f + 0.1f * i;
                _spriteBatch.Draw(_pixel, new Rectangle(sx - 2, sy - 2, 4, 4), Color.White * alpha);
            }
        }

        private void DrawGameOverOverlay()
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, Theme.WindowH / 2 - 30, Theme.BoardAreaW, 60), Theme.WinBg);
            string msg;
            if (_session.IsWon)
            {
                if (_session.Mode == GameMode.AiVsAi)
                    msg = $"{StoneLabel(_session.Winner)} AI 승리!";
                else
                    msg = _session.Winner == _session.PlayerStone ? "승리하셨습니다!" : "AI가 이겼습니다!";
            }
            else msg = "무승부!";

            var sz = _fontNormal.MeasureString(msg);
            _fontNormal.DrawText(_spriteBatch, msg,
                new Vector2((Theme.BoardAreaW - sz.X) / 2f, Theme.WindowH / 2f - sz.Y / 2f),
                _session.Winner == _session.PlayerStone ? Color.Gold : Color.White);
        }

        protected override void UnloadContent()
        {
            _pixel?.Dispose();
            _stoneBlack?.Dispose();
            _stoneWhite?.Dispose();
            _lastMoveMarker?.Dispose();
            _starPoint?.Dispose();
            _fontSystem?.Dispose();
            _spriteBatch?.Dispose();
            _sound?.Dispose();
            base.UnloadContent();
        }
    }
}
