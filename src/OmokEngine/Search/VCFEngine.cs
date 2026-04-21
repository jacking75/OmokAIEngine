using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GomokuEngine.Core;
using GomokuEngine.Evaluation;

namespace GomokuEngine.Search
{
    public class VCFEngine
    {
        private GomokuBoard board;
        private Dictionary<string, VCFResult> vcfCache;
        private int maxSearchDepth;

        public class VCFResult
        {
            public bool IsVCF { get; set; }
            public List<Position> WinningSequence { get; set; } = new List<Position>();
            public int Depth { get; set; }
            public string Description { get; set; } = "";
        }

        public VCFEngine(GomokuBoard board, int maxDepth = 12)
        {
            this.board = board;
            maxSearchDepth = maxDepth;
            vcfCache = new Dictionary<string, VCFResult>();
        }

        public VCFResult FindVCFSequence(Stone attackerStone)
        {
            vcfCache.Clear();
            var sequence = new List<Position>();
            bool found = SearchVCF(attackerStone, maxSearchDepth, sequence, true);
            return new VCFResult
            {
                IsVCF = found,
                WinningSequence = found ? new List<Position>(sequence) : new List<Position>(),
                Depth = sequence.Count,
                Description = found ? $"VCF in {sequence.Count} moves" : "No VCF"
            };
        }

        private bool SearchVCF(Stone attackerStone, int depth, List<Position> sequence, bool isAttackerTurn)
        {
            if (depth <= 0) return false;

            string boardHash = GetBoardHash();
            if (vcfCache.TryGetValue(boardHash, out var cached))
            {
                if (cached.IsVCF) sequence.AddRange(cached.WinningSequence);
                return cached.IsVCF;
            }

            if (isAttackerTurn)
            {
                var fourMoves = FindFourMoves(attackerStone);

                foreach (var move in fourMoves)
                {
                    board.PlaceStone(move, attackerStone);
                    if (board.CheckWin(move, attackerStone))
                    {
                        sequence.Add(move);
                        board.RemoveStone(move);
                        CacheResult(boardHash, true, new List<Position> { move });
                        return true;
                    }
                    board.RemoveStone(move);
                }

                var openFour = fourMoves.FirstOrDefault(m => IsOpenFour(m, attackerStone));
                if (openFour.Row >= 0)
                {
                    sequence.Add(openFour);
                    CacheResult(boardHash, true, new List<Position> { openFour });
                    return true;
                }

                foreach (var move in fourMoves.OrderByDescending(m => GetMovePriority(m, attackerStone)))
                {
                    board.PlaceStone(move, attackerStone);
                    sequence.Add(move);
                    if (SearchVCF(attackerStone, depth - 1, sequence, false))
                    {
                        board.RemoveStone(move);
                        CacheResult(boardHash, true, new List<Position>(sequence));
                        return true;
                    }
                    board.RemoveStone(move);
                    sequence.RemoveAt(sequence.Count - 1);
                }

                CacheResult(boardHash, false, new List<Position>());
                return false;
            }
            else
            {
                Stone defenderStone = GetOpponent(attackerStone);
                var defenses = FindMandatoryDefenses(attackerStone);

                if (defenses.Count >= 2 || defenses.Count == 0)
                    return true;

                var defMove = defenses[0];
                board.PlaceStone(defMove, defenderStone);
                sequence.Add(defMove);
                bool result = SearchVCF(attackerStone, depth - 1, sequence, true);
                board.RemoveStone(defMove);
                if (!result) sequence.RemoveAt(sequence.Count - 1);
                return result;
            }
        }

        private List<Position> FindFourMoves(Stone stone)
        {
            var moves = new List<Position>();
            foreach (var pos in GetCandidatePositions())
            {
                board.PlaceStone(pos, stone);
                var patterns = PatternAnalyzer.AnalyzePosition(board, pos, stone);
                board.RemoveStone(pos);
                if (patterns.Values.Any(p => p.ConsecutiveStones >= 4))
                    moves.Add(pos);
            }
            return moves.Distinct().ToList();
        }

        private List<Position> FindMandatoryDefenses(Stone attackerStone)
        {
            var defenses = new List<Position>();
            foreach (var pos in GetCandidatePositions())
            {
                board.PlaceStone(pos, attackerStone);
                bool win = board.CheckWin(pos, attackerStone);
                bool openFour = !win && IsOpenFour(pos, attackerStone);
                board.RemoveStone(pos);
                if (win || openFour) defenses.Add(pos);
            }
            return defenses;
        }

        private bool IsOpenFour(Position pos, Stone stone)
        {
            var patterns = PatternAnalyzer.AnalyzePosition(board, pos, stone);
            return patterns.Values.Any(p => p.ConsecutiveStones == 4 && p.OpenEnds == 2);
        }

        private int GetMovePriority(Position pos, Stone stone)
        {
            board.PlaceStone(pos, stone);
            var patterns = PatternAnalyzer.AnalyzePosition(board, pos, stone);
            board.RemoveStone(pos);
            int max = 0;
            foreach (var p in patterns.Values)
                max = Math.Max(max, PatternAnalyzer.CalculatePatternScore(p));
            return max;
        }

        private List<Position> GetCandidatePositions()
        {
            var list = new List<Position>();
            int size = board.GetBoardSize();
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    var pos = new Position(i, j);
                    if (board.IsEmpty(i, j) && board.HasNeighbor(pos, 2))
                        list.Add(pos);
                }
            return list;
        }

        private string GetBoardHash()
        {
            var sb = new StringBuilder();
            int size = board.GetBoardSize();
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                    sb.Append((int)board.GetStone(i, j));
            return sb.ToString();
        }

        private void CacheResult(string hash, bool isVCF, List<Position> seq)
        {
            vcfCache[hash] = new VCFResult
            {
                IsVCF = isVCF,
                WinningSequence = new List<Position>(seq),
                Depth = seq.Count
            };
        }

        private static Stone GetOpponent(Stone stone) =>
            stone == Stone.Black ? Stone.White : Stone.Black;
    }
}
