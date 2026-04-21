using System;
using System.Collections.Generic;
using System.Linq;
using GomokuEngine.Core;

namespace GomokuEngine.Evaluation
{
    public class MoveEvaluator
    {
        private GomokuBoard board;

        public MoveEvaluator(GomokuBoard board)
        {
            this.board = board;
        }

        public List<EvaluatedMove> EvaluateAllMoves(Stone playerStone, int maxCandidates = 20)
        {
            Stone opponentStone = GetOpponent(playerStone);

            if (board.GetMoveHistory().Count == 0)
            {
                int center = board.GetBoardSize() / 2;
                return new List<EvaluatedMove>
                {
                    new EvaluatedMove(new Position(center, center), 10000, MoveType.Strategic, "Opening")
                };
            }

            var candidates = GetCandidatePositions();
            var evaluated = new List<EvaluatedMove>(candidates.Count);

            foreach (var pos in candidates)
            {
                int score = EvaluatePosition(pos, playerStone, opponentStone, out MoveType moveType);
                evaluated.Add(new EvaluatedMove(pos, score, moveType));
            }

            return evaluated.OrderByDescending(m => m.Score).Take(maxCandidates).ToList();
        }

        private List<Position> GetCandidatePositions()
        {
            var candidates = new List<Position>();
            int size = board.GetBoardSize();
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    var pos = new Position(i, j);
                    if (board.IsEmpty(i, j) && board.HasNeighbor(pos, 2))
                        candidates.Add(pos);
                }
            return candidates;
        }

        private int EvaluatePosition(Position pos, Stone player, Stone opponent, out MoveType moveType)
        {
            moveType = MoveType.Neutral;

            board.PlaceStone(pos, player);
            if (board.CheckWin(pos, player))
            {
                board.RemoveStone(pos);
                moveType = MoveType.Winning;
                return 1000000;
            }
            var playerPatterns = PatternAnalyzer.AnalyzePosition(board, pos, player);
            int attackScore = CalcTotal(playerPatterns);
            board.RemoveStone(pos);

            board.PlaceStone(pos, opponent);
            if (board.CheckWin(pos, opponent))
            {
                board.RemoveStone(pos);
                moveType = MoveType.DefendFour;
                return 900000;
            }
            var opponentPatterns = PatternAnalyzer.AnalyzePosition(board, pos, opponent);
            int defenseScore = CalcTotal(opponentPatterns);
            board.RemoveStone(pos);

            moveType = DetermineMoveType(playerPatterns, opponentPatterns);
            int total = attackScore + (int)(defenseScore * 1.1) + GetPositionBonus(pos);
            return total;
        }

        private int CalcTotal(Dictionary<string, Pattern> patterns)
        {
            int total = 0;
            foreach (var p in patterns.Values)
                total += PatternAnalyzer.CalculatePatternScore(p);
            return total;
        }

        private MoveType DetermineMoveType(Dictionary<string, Pattern> player, Dictionary<string, Pattern> opponent)
        {
            foreach (var p in opponent.Values)
            {
                if (p.ConsecutiveStones == 4) return MoveType.DefendFour;
                if (p.ConsecutiveStones == 3 && p.OpenEnds == 2) return MoveType.DefendThree;
            }
            foreach (var p in player.Values)
            {
                if (p.ConsecutiveStones == 4) return MoveType.MakeFour;
                if (p.ConsecutiveStones == 3 && p.OpenEnds == 2) return MoveType.MakeThree;
            }
            return MoveType.Strategic;
        }

        private int GetPositionBonus(Position pos)
        {
            int center = board.GetBoardSize() / 2;
            int dist = Math.Abs(pos.Row - center) + Math.Abs(pos.Col - center);
            return Math.Max(0, 50 - dist * 2);
        }

        private static Stone GetOpponent(Stone stone) =>
            stone == Stone.Black ? Stone.White : Stone.Black;
    }
}
