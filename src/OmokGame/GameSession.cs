using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GomokuEngine.AI;
using GomokuEngine.Core;

namespace OmokGame
{
    internal enum GamePhase { WaitingNewGame, PlayerTurn, AiThinking, GameOver }
    internal enum GameMode  { PlayerVsAi, AiVsAi }

    internal class GameSession
    {
        // UI 보드 사본 (엔진 보드와 분리)
        private Stone[,] _uiBoard = new Stone[15, 15];
        private List<Position> _history = new List<Position>();

        // 두 색상별 AI (PvA에서는 AI 측만 채워짐, AvA에서는 둘 다 채워짐)
        private GomokuAI? _blackAi;
        private GomokuAI? _whiteAi;

        // 현재 게임 옵션 스냅샷
        private int _blackLevel = 5;
        private int _whiteLevel = 5;
        private bool _useRenju;
        private GameMode _mode = GameMode.PlayerVsAi;

        // AI 태스크
        private Task<EvaluatedMove?>? _aiTask;
        private int _currentGeneration;
        private int _taskGeneration;
        private DateTime _aiStartTime;

        // 힌트 태스크 (별도 generation: AI 게임 진행과 무관하게 hint 단독 폐기 가능)
        private Task<EvaluatedMove?>? _hintTask;
        private int _hintRequestId;
        private int _hintTaskId;

        // Load 중에는 효과음/승리음 이벤트 억제
        private bool _silent;

        public GamePhase Phase { get; private set; } = GamePhase.WaitingNewGame;
        public GameMode  Mode  => _mode;
        public Stone PlayerStone { get; private set; } = Stone.Black;
        public Stone AiStone => PlayerStone == Stone.Black ? Stone.White : Stone.Black;
        public Stone CurrentTurn { get; private set; } = Stone.Black;
        public int MoveCount => _history.Count;

        public bool IsWon { get; private set; }
        public Stone Winner { get; private set; }
        public (int r1, int c1, int r2, int c2)? WinLine { get; private set; }

        public double AiElapsedMs { get; private set; }

        public List<(int r, int c)> ForbiddenCells { get; } = new();

        public Position? HintPosition { get; private set; }

        // 착수 시각 기록 (페이드 애니메이션용)
        public Dictionary<(int r, int c), double> PlaceTimes { get; } = new();
        public double Now { get; set; }   // Game1에서 매 프레임 갱신

        public event Action? OnStateChanged;
        public event Action<string>? OnToast;
        public event Action<Position, Stone>? OnStonePlaced;   // 효과음 트리거
        public event Action<Stone>? OnGameWon;                 // 승리 효과음 트리거

        public Stone[,] UiBoard => _uiBoard;
        public List<Position> History => _history;

        // ──────────────── 새 게임 ────────────────

        public void StartNewGame(GameMode mode, int blackLevel, int whiteLevel,
                                 Stone playerStone, bool useRenju, bool showMoveNums)
        {
            Interlocked.Increment(ref _currentGeneration);
            _hintRequestId++;
            _aiTask = null;
            _hintTask = null;

            Array.Clear(_uiBoard, 0, _uiBoard.Length);
            _history.Clear();
            ForbiddenCells.Clear();
            PlaceTimes.Clear();
            HintPosition = null;
            WinLine = null;
            IsWon = false;
            Winner = Stone.Empty;

            _mode = mode;
            _blackLevel = blackLevel;
            _whiteLevel = whiteLevel;
            _useRenju   = useRenju;
            PlayerStone = playerStone;
            CurrentTurn = Stone.Black;

            // AI 인스턴스 생성
            _blackAi = null;
            _whiteAi = null;
            if (mode == GameMode.AiVsAi)
            {
                _blackAi = new GomokuAI(new GomokuAiOptions { Level = blackLevel, UseRenju = useRenju, AiStone = Stone.Black });
                _whiteAi = new GomokuAI(new GomokuAiOptions { Level = whiteLevel, UseRenju = useRenju, AiStone = Stone.White });
            }
            else
            {
                int aiLevel = playerStone == Stone.Black ? whiteLevel : blackLevel;
                if (playerStone == Stone.Black)
                    _whiteAi = new GomokuAI(new GomokuAiOptions { Level = aiLevel, UseRenju = useRenju, AiStone = Stone.White });
                else
                    _blackAi = new GomokuAI(new GomokuAiOptions { Level = aiLevel, UseRenju = useRenju, AiStone = Stone.Black });
            }

            Phase = DecidePhaseForCurrentTurn();
            if (Phase == GamePhase.AiThinking) StartAiTask();

            UpdateForbiddenCells();
            OnStateChanged?.Invoke();
        }

        private GomokuAI? CurrentAi() => CurrentTurn == Stone.Black ? _blackAi : _whiteAi;

        private GamePhase DecidePhaseForCurrentTurn()
        {
            if (_mode == GameMode.AiVsAi) return GamePhase.AiThinking;
            return CurrentTurn == PlayerStone ? GamePhase.PlayerTurn : GamePhase.AiThinking;
        }

        // ──────────────── 플레이어 착수 ────────────────

        public bool TryPlayerMove(int row, int col)
        {
            if (Phase != GamePhase.PlayerTurn) return false;
            if (!IsEmpty(row, col)) return false;

            var pos = new Position(row, col);
            var ai = CurrentAi();   // 플레이어 차례엔 null이지만 금수 체크 위해 반대편 AI 사용
            var renjuAi = _blackAi ?? _whiteAi;
            if (renjuAi != null && _useRenju && PlayerStone == Stone.Black)
            {
                var (ok, reason) = renjuAi.CanPlace(pos, PlayerStone);
                if (!ok) { OnToast?.Invoke($"금수: {reason}"); return false; }
            }

            ClearHint();   // B4: 플레이어 착수 시 in-flight 힌트 폐기
            ApplyMoveBoth(pos, PlayerStone);

            if (CheckWin(pos, PlayerStone)) { EndGameWon(pos, PlayerStone); return true; }
            if (IsBoardFull())              { EndGameDraw(); return true; }

            CurrentTurn = AiStone;
            Phase = GamePhase.AiThinking;
            UpdateForbiddenCells();
            StartAiTask();
            OnStateChanged?.Invoke();
            return true;
        }

        // ──────────────── AI 태스크 관리 ────────────────

        private void StartAiTask()
        {
            var ai = CurrentAi();
            if (ai == null) return;
            _aiStartTime = DateTime.Now;
            _taskGeneration = _currentGeneration;
            _aiTask = Task.Run(() => ai.RequestMove());
        }

        public void Update()
        {
            // 힌트 결과 처리 (엔진 예외가 발생해도 게임은 계속 진행)
            if (_hintTask != null && _hintTask.IsCompleted)
            {
                EvaluatedMove? hintResult = null;
                try { hintResult = _hintTask.Result; }
                catch (Exception ex) { OnToast?.Invoke($"힌트 오류: {ex.GetBaseException().Message}"); }

                if (_hintTaskId == _hintRequestId && hintResult != null)
                    HintPosition = hintResult.Position;
                _hintTask = null;
                OnStateChanged?.Invoke();
            }

            if (Phase != GamePhase.AiThinking || _aiTask == null) return;

            AiElapsedMs = (DateTime.Now - _aiStartTime).TotalMilliseconds;
            if (!_aiTask.IsCompleted) return;

            if (_taskGeneration != _currentGeneration) { _aiTask = null; return; }

            EvaluatedMove? move;
            try { move = _aiTask.Result; }
            catch (Exception ex)
            {
                _aiTask = null;
                OnToast?.Invoke($"AI 오류: {ex.GetBaseException().Message}");
                // 안전 폴백: 현재 차례를 게임 종료(무승부) 처리
                EndGameDraw();
                return;
            }
            _aiTask = null;

            if (move == null) { EndGameDraw(); return; }

            var pos = move.Position;
            Stone moverStone = CurrentTurn;
            ApplyMoveBoth(pos, moverStone);

            if (CheckWin(pos, moverStone)) { EndGameWon(pos, moverStone); return; }
            if (IsBoardFull())             { EndGameDraw(); return; }

            CurrentTurn = moverStone == Stone.Black ? Stone.White : Stone.Black;
            Phase = DecidePhaseForCurrentTurn();
            UpdateForbiddenCells();
            if (Phase == GamePhase.AiThinking) StartAiTask();
            OnStateChanged?.Invoke();
        }

        public bool IsAiThinking => Phase == GamePhase.AiThinking;

        // ──────────────── 무르기 ────────────────

        public bool CanUndo()
        {
            if (Phase == GamePhase.WaitingNewGame) return false;
            if (_mode == GameMode.AiVsAi) return false;
            if (Phase == GamePhase.AiThinking) return false;
            // PvA: 플레이어 + AI 두 수가 있어야 무르기 가능
            return _history.Count >= (PlayerStone == Stone.Black ? 2 : 1);
        }

        public bool Undo()
        {
            if (!CanUndo()) return false;

            // PlayerVsAi: 두 수(자신 + AI) 되돌림. 단, 플레이어가 백이면 첫 수는 AI 흑만 있을 수도 있음.
            int undoCount = Math.Min(2, _history.Count);
            for (int i = 0; i < undoCount; i++)
            {
                var pos = _history[_history.Count - 1];
                _history.RemoveAt(_history.Count - 1);
                _uiBoard[pos.Row, pos.Col] = Stone.Empty;
                PlaceTimes.Remove((pos.Row, pos.Col));
                _blackAi?.UndoMove(pos);
                _whiteAi?.UndoMove(pos);
            }

            // 게임 오버 상태였으면 복원
            IsWon = false;
            Winner = Stone.Empty;
            WinLine = null;
            ClearHint();   // 무르기 직전에 진행 중이던 힌트는 stale 보드 기반이므로 폐기

            CurrentTurn = PlayerStone;
            Phase = GamePhase.PlayerTurn;
            UpdateForbiddenCells();
            OnStateChanged?.Invoke();
            return true;
        }

        // ──────────────── 항복 ────────────────

        public bool Surrender()
        {
            if (Phase != GamePhase.PlayerTurn && Phase != GamePhase.AiThinking) return false;
            if (_mode == GameMode.AiVsAi) return false;
            Phase = GamePhase.GameOver;
            IsWon = true;
            Winner = AiStone;
            WinLine = null;
            ClearHint();              // B4
            ForbiddenCells.Clear();   // B2
            if (!_silent) OnGameWon?.Invoke(AiStone);
            OnStateChanged?.Invoke();
            return true;
        }

        // ──────────────── 힌트 ────────────────

        public bool RequestHint()
        {
            if (Phase != GamePhase.PlayerTurn) return false;
            if (_hintTask != null) return false;
            // B9: 힌트는 항상 최강(Lv20)으로 추천 — 약한 설정에서도 의미 있는 도움 제공
            var hintAi = new GomokuAI(new GomokuAiOptions
            {
                Level = 20,
                UseRenju = _useRenju,
                AiStone = PlayerStone
            });
            // 현재 보드 상태 복제
            for (int i = 0; i < _history.Count; i++)
            {
                var p = _history[i];
                Stone s = (i % 2 == 0) ? Stone.Black : Stone.White;
                hintAi.ApplyMove(p, s);
            }
            _hintRequestId++;
            _hintTaskId = _hintRequestId;
            _hintTask = Task.Run(() => hintAi.RequestMove());
            return true;
        }

        public void ClearHint()
        {
            HintPosition = null;
            _hintRequestId++;   // B4: in-flight 힌트 결과 폐기
        }

        // ──────────────── Save / Load ────────────────

        private record SaveData(int Mode, int BlackLevel, int WhiteLevel, int PlayerStone, bool UseRenju, int[][] Moves);

        public void Save(string path)
        {
            int[][] moves = new int[_history.Count][];
            for (int i = 0; i < _history.Count; i++)
                moves[i] = new[] { _history[i].Row, _history[i].Col };

            var data = new SaveData((int)_mode, _blackLevel, _whiteLevel,
                                    (int)PlayerStone, _useRenju, moves);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(data));
        }

        public bool Load(string path)
        {
            if (!File.Exists(path)) return false;
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
            if (data == null) return false;

            _silent = true;   // B3: Load 도중에는 효과음/승리음 억제
            try
            {
                StartNewGame((GameMode)data.Mode, data.BlackLevel, data.WhiteLevel,
                             (Stone)data.PlayerStone, data.UseRenju, true);

                // 진행 중인 AI 태스크 취소
                Interlocked.Increment(ref _currentGeneration);
                _aiTask = null;

                // 모든 수 재현
                for (int i = 0; i < data.Moves.Length; i++)
                {
                    var arr = data.Moves[i];
                    var pos = new Position(arr[0], arr[1]);
                    Stone s = (i % 2 == 0) ? Stone.Black : Stone.White;
                    ApplyMoveBoth(pos, s);
                    if (CheckWin(pos, s)) { EndGameWon(pos, s); OnStateChanged?.Invoke(); return true; }
                    CurrentTurn = s == Stone.Black ? Stone.White : Stone.Black;
                }

                if (IsBoardFull()) { EndGameDraw(); }
                else
                {
                    Phase = DecidePhaseForCurrentTurn();
                    if (Phase == GamePhase.AiThinking) StartAiTask();
                }
                UpdateForbiddenCells();
                OnStateChanged?.Invoke();
                return true;
            }
            finally
            {
                _silent = false;
            }
        }

        // ──────────────── 보조 ────────────────

        private void ApplyMoveBoth(Position pos, Stone stone)
        {
            _uiBoard[pos.Row, pos.Col] = stone;
            _history.Add(pos);
            PlaceTimes[(pos.Row, pos.Col)] = Now;
            _blackAi?.ApplyMove(pos, stone);
            _whiteAi?.ApplyMove(pos, stone);
            if (!_silent) OnStonePlaced?.Invoke(pos, stone);   // B3
        }

        private void EndGameWon(Position pos, Stone stone)
        {
            Phase = GamePhase.GameOver;
            IsWon = true;
            Winner = stone;
            FindWinLine(pos, stone);
            HintPosition = null;
            ForbiddenCells.Clear();    // B2
            if (!_silent) OnGameWon?.Invoke(stone);   // B3
            OnStateChanged?.Invoke();
        }

        private void EndGameDraw()
        {
            Phase = GamePhase.GameOver;
            HintPosition = null;
            ForbiddenCells.Clear();    // B2
            OnStateChanged?.Invoke();
        }

        public (bool ok, string reason) CanPlaceForUi(Position pos, Stone stone)
        {
            var ai = _blackAi ?? _whiteAi;
            if (ai == null) return (true, "");
            return ai.CanPlace(pos, stone);
        }

        private bool IsEmpty(int r, int c) =>
            r >= 0 && r < 15 && c >= 0 && c < 15 && _uiBoard[r, c] == Stone.Empty;

        private bool IsBoardFull()
        {
            foreach (var s in _uiBoard) if (s == Stone.Empty) return false;
            return true;
        }

        private bool CheckWin(Position pos, Stone stone)
        {
            int[] dx = { 0, 1, 1, 1 };
            int[] dy = { 1, 0, 1, -1 };
            for (int d = 0; d < 4; d++)
            {
                int count = 1;
                count += CountDir(pos, stone, dx[d], dy[d]);
                count += CountDir(pos, stone, -dx[d], -dy[d]);
                if (count >= 5) return true;
            }
            return false;
        }

        private int CountDir(Position pos, Stone stone, int dr, int dc)
        {
            int count = 0;
            int r = pos.Row + dr, c = pos.Col + dc;
            while (r >= 0 && r < 15 && c >= 0 && c < 15 && _uiBoard[r, c] == stone)
            { count++; r += dr; c += dc; }
            return count;
        }

        private void FindWinLine(Position pos, Stone stone)
        {
            int[] dx = { 0, 1, 1, 1 };
            int[] dy = { 1, 0, 1, -1 };
            for (int d = 0; d < 4; d++)
            {
                int fwd = CountDir(pos, stone, dx[d], dy[d]);
                int bwd = CountDir(pos, stone, -dx[d], -dy[d]);
                if (fwd + bwd + 1 >= 5)
                {
                    int r1 = pos.Row - bwd * dx[d];
                    int c1 = pos.Col - bwd * dy[d];
                    int r2 = pos.Row + fwd * dx[d];
                    int c2 = pos.Col + fwd * dy[d];
                    WinLine = (r1, c1, r2, c2);
                    return;
                }
            }
        }

        private void UpdateForbiddenCells()
        {
            ForbiddenCells.Clear();
            if (!_useRenju || _mode == GameMode.AiVsAi) return;
            if (PlayerStone != Stone.Black || Phase != GamePhase.PlayerTurn) return;
            var ai = _blackAi ?? _whiteAi;
            if (ai == null) return;

            for (int r = 0; r < 15; r++)
                for (int c = 0; c < 15; c++)
                {
                    if (_uiBoard[r, c] != Stone.Empty) continue;
                    var pos = new Position(r, c);
                    var (ok, _) = ai.CanPlace(pos, Stone.Black);
                    if (!ok) ForbiddenCells.Add((r, c));
                }
        }
    }
}
