using System.Collections.Generic;
using System.Linq;
using GomokuEngine.Core;
using GomokuEngine.Evaluation;

namespace GomokuEngine.Analysis
{
    public class RenjuRuleChecker
    {
        private GomokuBoard board;

        public enum ForbiddenType { None, DoubleThree, DoubleFour, Overline, Multiple }

        public class ForbiddenMoveInfo
        {
            public ForbiddenType Type { get; set; }
            public List<string> Reasons { get; set; } = new List<string>();
            public bool IsAllowed { get; set; }
        }

        public RenjuRuleChecker(GomokuBoard board)
        {
            this.board = board;
        }

        public ForbiddenMoveInfo CheckForbiddenMove(Position pos, Stone stone)
        {
            var info = new ForbiddenMoveInfo { Type = ForbiddenType.None, IsAllowed = true };

            if (stone != Stone.Black || !board.IsEmpty(pos.Row, pos.Col))
                return info;

            board.PlaceStone(pos, stone);

            if (board.CheckWin(pos, stone))
            {
                board.RemoveStone(pos);
                return info;
            }

            var types = new List<ForbiddenType>();
            if (CheckOverline(pos, stone, info)) types.Add(ForbiddenType.Overline);
            if (CheckDoubleFour(pos, stone, info)) types.Add(ForbiddenType.DoubleFour);
            if (CheckDoubleThree(pos, stone, info)) types.Add(ForbiddenType.DoubleThree);

            board.RemoveStone(pos);

            if (types.Count > 0)
            {
                info.IsAllowed = false;
                info.Type = types.Count > 1 ? ForbiddenType.Multiple : types[0];
            }

            return info;
        }

        private bool CheckOverline(Position pos, Stone stone, ForbiddenMoveInfo info)
        {
            var dirs = new[] { (0, 1), (1, 0), (1, 1), (1, -1) };
            foreach (var (dx, dy) in dirs)
            {
                int count = 1 + board.CountConsecutive(pos, stone, dx, dy) + board.CountConsecutive(pos, stone, -dx, -dy);
                if (count > 5)
                {
                    info.Reasons.Add($"장목: {count}개");
                    return true;
                }
            }
            return false;
        }

        private bool CheckDoubleFour(Position pos, Stone stone, ForbiddenMoveInfo info)
        {
            var dirs = new[] { (0, 1), (1, 0), (1, 1), (1, -1) };
            int fourCount = dirs.Count(d =>
            {
                int c = 1 + board.CountConsecutive(pos, stone, d.Item1, d.Item2)
                          + board.CountConsecutive(pos, stone, -d.Item1, -d.Item2);
                return c == 4;
            });
            if (fourCount >= 2)
            {
                info.Reasons.Add($"쌍사: {fourCount}개의 4목");
                return true;
            }
            return false;
        }

        private bool CheckDoubleThree(Position pos, Stone stone, ForbiddenMoveInfo info)
        {
            var patterns = PatternAnalyzer.AnalyzePosition(board, pos, stone);
            int threeCount = patterns.Values.Count(p => p.ConsecutiveStones == 3 && p.OpenEnds == 2);
            if (threeCount >= 2)
            {
                info.Reasons.Add($"쌍삼: {threeCount}개의 열린 3목");
                return true;
            }
            return false;
        }

        public List<Position> GetLegalMoves(Stone stone)
        {
            var legal = new List<Position>();
            int size = board.GetBoardSize();
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    var pos = new Position(i, j);
                    if (board.IsEmpty(i, j) && CheckForbiddenMove(pos, stone).IsAllowed)
                        legal.Add(pos);
                }
            return legal;
        }
    }
}
