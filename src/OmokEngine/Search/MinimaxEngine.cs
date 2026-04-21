using System;
using System.Collections.Generic;
using System.Linq;
using GomokuEngine.Core;
using GomokuEngine.Evaluation;

namespace GomokuEngine.Search
{
    public class MinimaxEngine
    {
        private GomokuBoard board;
        private TranspositionTable ttable;
        private ZobristHasher hasher;
        private ulong currentHash;
        private int nodesEvaluated;

        public MinimaxEngine(GomokuBoard board)
        {
            this.board = board;
            ttable = new TranspositionTable(256);
            hasher = new ZobristHasher();
            currentHash = hasher.ComputeHash(board);
        }

        public Position FindBestMove(Stone stone, int maxDepth, int timeLimitMs = 5000)
        {
            nodesEvaluated = 0;
            currentHash = hasher.ComputeHash(board);  // sync hash with current board state
            var startTime = DateTime.Now;

            Position bestMove = new Position(-1, -1);
            int bestScore = int.MinValue;
            var candidates = GetOrderedCandidates(stone);

            foreach (var candidate in candidates)
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > timeLimitMs)
                    break;

                board.PlaceStone(candidate, stone);
                currentHash = hasher.UpdateHash(currentHash, candidate, stone);

                if (board.CheckWin(candidate, stone))
                {
                    currentHash = hasher.RemoveFromHash(currentHash, candidate, stone);
                    board.RemoveStone(candidate);
                    return candidate;
                }

                int score = -Negamax(GetOpponent(stone), maxDepth - 1, int.MinValue + 1, int.MaxValue - 1, startTime, timeLimitMs);

                currentHash = hasher.RemoveFromHash(currentHash, candidate, stone);
                board.RemoveStone(candidate);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = candidate;
                }
            }

            return bestMove;
        }

        private int Negamax(Stone stone, int depth, int alpha, int beta, DateTime startTime, int timeLimitMs)
        {
            nodesEvaluated++;
            int alphaOrig = alpha;

            if ((DateTime.Now - startTime).TotalMilliseconds > timeLimitMs)
                return Evaluate(stone);

            if (ttable.Probe(currentHash, depth, alpha, beta, out int ttScore, out Position ttMove))
                return ttScore;

            if (depth == 0)
                return Evaluate(stone);

            var candidates = GetOrderedCandidates(stone, ttMove);
            int bestScore = int.MinValue;
            Position bestMove = new Position(-1, -1);

            foreach (var candidate in candidates)
            {
                board.PlaceStone(candidate, stone);
                currentHash = hasher.UpdateHash(currentHash, candidate, stone);

                if (board.CheckWin(candidate, stone))
                {
                    currentHash = hasher.RemoveFromHash(currentHash, candidate, stone);
                    board.RemoveStone(candidate);
                    int winScore = 1000000 - depth;
                    ttable.Store(currentHash, winScore, depth, candidate, TranspositionTable.TTEntry.EntryType.Exact);
                    return winScore;
                }

                int score = -Negamax(GetOpponent(stone), depth - 1, -beta, -alpha, startTime, timeLimitMs);

                currentHash = hasher.RemoveFromHash(currentHash, candidate, stone);
                board.RemoveStone(candidate);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = candidate;
                }

                alpha = Math.Max(alpha, score);
                if (alpha >= beta) break;
            }

            var entryType = bestScore <= alphaOrig
                ? TranspositionTable.TTEntry.EntryType.UpperBound
                : bestScore >= beta
                    ? TranspositionTable.TTEntry.EntryType.LowerBound
                    : TranspositionTable.TTEntry.EntryType.Exact;

            ttable.Store(currentHash, bestScore, depth, bestMove, entryType);
            return bestScore;
        }

        private int Evaluate(Stone stone)
        {
            return EvaluateForPlayer(stone) - EvaluateForPlayer(GetOpponent(stone));
        }

        private int EvaluateForPlayer(Stone stone)
        {
            int score = 0;
            int size = board.GetBoardSize();
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                    if (board.GetStone(i, j) == stone)
                        foreach (var p in PatternAnalyzer.AnalyzePosition(board, new Position(i, j), stone).Values)
                            score += PatternAnalyzer.CalculatePatternScore(p);
            return score / 4;
        }

        private List<Position> GetOrderedCandidates(Stone stone, Position ttMove = default)
        {
            var evaluator = new MoveEvaluator(board);
            var candidates = evaluator.EvaluateAllMoves(stone, 20);

            if (ttMove.Row >= 0)
                candidates = candidates
                    .OrderByDescending(c => c.Position.Equals(ttMove) ? int.MaxValue : c.Score)
                    .ToList();

            return candidates.Select(c => c.Position).ToList();
        }

        private static Stone GetOpponent(Stone stone) =>
            stone == Stone.Black ? Stone.White : Stone.Black;

        public void ClearCache()
        {
            ttable.Clear();
            currentHash = hasher.ComputeHash(board);
        }

        public double GetCacheUsage() => ttable.GetUsagePercent();
        public int GetNodesEvaluated() => nodesEvaluated;
    }
}
