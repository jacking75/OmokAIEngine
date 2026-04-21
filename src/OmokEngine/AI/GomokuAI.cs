using System;
using System.Linq;
using System.Threading.Tasks;
using GomokuEngine.Core;
using GomokuEngine.Evaluation;
using GomokuEngine.Search;
using GomokuEngine.Analysis;

namespace GomokuEngine.AI
{
    /// <summary>
    /// 10단계 실력 조절 오목 AI 퍼사드.
    /// 엔진 내부의 SearchDepth dead-code 문제를 우회하여
    /// MinimaxEngine/VCFEngine을 직접 호출하고 반복 심화(iterative deepening)를 적용.
    /// 서버/UI 양쪽에서 사용 가능한 라이브러리 진입점.
    /// </summary>
    public class GomokuAI
    {
        private readonly GomokuBoard _board;
        private readonly MinimaxEngine _minimax;
        private readonly RenjuRuleChecker _renju;
        private readonly GomokuAiOptions _options;
        private readonly Random _rng;

        public GomokuAI(GomokuAiOptions options)
        {
            _options = options;
            _board = new GomokuBoard();
            _minimax = new MinimaxEngine(_board);
            _renju = new RenjuRuleChecker(_board);
            _rng = new Random();
        }

        /// <summary>현재 옵션 (읽기 전용 복사본)</summary>
        public GomokuAiOptions Options => new GomokuAiOptions
        {
            Level = _options.Level,
            UseRenju = _options.UseRenju,
            AiStone = _options.AiStone
        };

        /// <summary>
        /// 플레이어 또는 AI의 수를 내부 보드에 적용.
        /// 실제로 두기 전에 UI에서 호출해야 함.
        /// </summary>
        public bool ApplyMove(Position pos, Stone stone)
        {
            return _board.PlaceStone(pos, stone);
        }

        /// <summary>이전에 둔 수를 되돌림 (보드에서 제거).</summary>
        public bool UndoMove(Position pos)
        {
            bool ok = _board.RemoveStone(pos);
            if (ok) _minimax.ClearCache();
            return ok;
        }

        /// <summary>
        /// AI의 다음 수를 동기적으로 계산하여 반환.
        /// 결과를 ApplyMove로 적용하는 것은 호출자 책임.
        /// null 반환 시 보드 가득 참(무승부).
        /// </summary>
        public EvaluatedMove? RequestMove()
        {
            var profile = LevelProfile.Profiles[Math.Clamp(_options.Level, 1, 20) - 1];
            Stone aiStone = _options.AiStone;

            // 1. 후보 수 생성 (5개)
            var evaluator = new MoveEvaluator(_board);
            var candidates = evaluator.EvaluateAllMoves(aiStone, 10);

            // 렌주 금수 필터 (AI가 흑이고 렌주 ON일 때)
            if (_options.UseRenju && aiStone == Stone.Black)
            {
                candidates = candidates
                    .Where(m => _renju.CheckForbiddenMove(m.Position, aiStone).IsAllowed)
                    .ToList();
            }

            if (candidates.Count == 0) return null;

            // 2. 즉시 승리 / 4목 방어는 항상 최우선
            var critical = candidates.FirstOrDefault(m =>
                m.Type == MoveType.Winning || m.Type == MoveType.DefendFour);
            if (critical != null) return critical;

            // 3. VCF 탐색 (VcfDepth > 0인 단계만)
            Position vcfPos = new Position(-1, -1);
            if (profile.VcfDepth > 0)
            {
                var vcf = new VCFEngine(_board, profile.VcfDepth);
                var vcfResult = vcf.FindVCFSequence(aiStone);
                if (vcfResult.IsVCF && vcfResult.WinningSequence.Count > 0)
                    vcfPos = vcfResult.WinningSequence[0];
            }

            // 4. 반복 심화 Minimax
            Position minimaxPos = candidates[0].Position;
            if (profile.SearchDepth > 0)
            {
                var startTime = DateTime.Now;
                Position lastGood = candidates[0].Position;
                for (int d = 1; d <= profile.SearchDepth; d++)
                {
                    double elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    if (elapsed > profile.TimeLimitMs * 0.85) break;
                    int remaining = (int)(profile.TimeLimitMs - elapsed);
                    var pos = _minimax.FindBestMove(aiStone, d, remaining);
                    if (pos.Row >= 0) lastGood = pos;
                }
                minimaxPos = lastGood;
            }

            // VCF가 있으면 우선
            Position optimalPos = vcfPos.Row >= 0 ? vcfPos : minimaxPos;

            // 5. 확률적 수 선택
            double r = _rng.NextDouble();
            if (r < profile.OptimalProb)
            {
                // 최선 (minimax/VCF 결과)
                var best = candidates.FirstOrDefault(m => m.Position.Equals(optimalPos));
                return best ?? candidates[0];
            }
            else if (r < profile.OptimalProb + profile.GoodProb)
            {
                // 2~3위
                int idx = _rng.Next(1, Math.Min(3, candidates.Count));
                return candidates[idx];
            }
            else
            {
                // 4~5위 (실수)
                int idx = _rng.Next(Math.Min(3, candidates.Count - 1),
                                    Math.Min(5, candidates.Count));
                return candidates[idx];
            }
        }

        /// <summary>비동기 래퍼 (UI Thread용)</summary>
        public Task<EvaluatedMove?> RequestMoveAsync() =>
            Task.Run(() => RequestMove());

        /// <summary>
        /// 게임 초기화 — 보드/캐시만 비우고 인스턴스는 그대로 재사용.
        /// 같은 옵션으로 다음 게임을 이어 시작할 때 사용.
        /// </summary>
        public void Reset()
        {
            _board.Clear();
            _minimax.ClearCache();
        }

        /// <summary>
        /// 게임 초기화 + 옵션 갱신 — 인스턴스 재사용하면서 레벨/색/렌주 변경.
        /// newOptions가 null이면 기존 옵션 유지.
        /// </summary>
        public void Reset(GomokuAiOptions? newOptions)
        {
            _board.Clear();
            _minimax.ClearCache();
            if (newOptions != null)
            {
                _options.Level    = newOptions.Level;
                _options.UseRenju = newOptions.UseRenju;
                _options.AiStone  = newOptions.AiStone;
            }
        }

        /// <summary>현재 내부 보드 사본 (읽기 전용 스냅샷)</summary>
        public Stone[,] GetBoardSnapshot() => _board.GetBoardCopy();

        /// <summary>승리 여부 확인</summary>
        public bool CheckWin(Position pos, Stone stone) => _board.CheckWin(pos, stone);

        /// <summary>
        /// 특정 위치에 착수 가능한지 확인.
        /// 렌주 룰이 활성화된 경우 흑의 금수도 체크.
        /// </summary>
        public (bool ok, string reason) CanPlace(Position pos, Stone stone)
        {
            if (!_board.IsEmpty(pos.Row, pos.Col))
                return (false, "이미 돌이 있습니다");
            if (_options.UseRenju && stone == Stone.Black)
            {
                var info = _renju.CheckForbiddenMove(pos, stone);
                if (!info.IsAllowed)
                    return (false, string.Join(", ", info.Reasons));
            }
            return (true, "");
        }
    }
}
