using System;
using System.Collections.Generic;
using System.Linq;
using GomokuEngine.Core;
using GomokuEngine.Evaluation;

namespace GomokuEngine.AI
{
    public enum PlayerSkillLevel
    {
        Novice, Beginner, Intermediate, Advanced, Expert
    }

    public class PlayerWeaknesses
    {
        public bool WeakDefense { get; set; }
        public bool WeakAttack { get; set; }
        public bool InconsistentPlay { get; set; }
        public bool TooFast { get; set; }
        public bool TooSlow { get; set; }
    }

    public class PlayerSkillAnalyzer
    {
        public List<MoveQuality> moveHistory = new List<MoveQuality>();
        private int recentMovesWindow = 10;

        public class MoveQuality
        {
            public Position PlayerMove { get; set; }
            public int OptimalScore { get; set; }
            public int ActualScore { get; set; }
            public double Quality { get; set; }
            public bool MissedThreat { get; set; }
            public bool MissedOpportunity { get; set; }
            public long ThinkingTime { get; set; }
        }

        public MoveQuality AnalyzePlayerMove(GomokuBoard board, Position playerMove,
                                             Stone playerStone, long thinkingTimeMs)
        {
            var evaluator = new MoveEvaluator(board);
            var topMoves = evaluator.EvaluateAllMoves(playerStone, 10);
            if (topMoves.Count == 0)
                return new MoveQuality { Quality = 1.0 };

            var optimalMove = topMoves[0];
            var actualMove = topMoves.FirstOrDefault(m => m.Position.Equals(playerMove));
            int actualScore = actualMove?.Score ?? 0;
            double quality = optimalMove.Score > 0 ? (double)actualScore / optimalMove.Score : 1.0;

            var mq = new MoveQuality
            {
                PlayerMove = playerMove,
                OptimalScore = optimalMove.Score,
                ActualScore = actualScore,
                Quality = quality,
                MissedThreat = CheckMissedThreat(topMoves, actualMove),
                MissedOpportunity = CheckMissedOpportunity(topMoves, actualMove),
                ThinkingTime = thinkingTimeMs
            };

            moveHistory.Add(mq);
            return mq;
        }

        private bool CheckMissedThreat(List<EvaluatedMove> top, EvaluatedMove? actual)
        {
            if (!top.Take(3).Any(m => m.Type == MoveType.DefendFour || m.Type == MoveType.DefendThree))
                return false;
            return actual == null ||
                   (actual.Type != MoveType.DefendFour && actual.Type != MoveType.DefendThree);
        }

        private bool CheckMissedOpportunity(List<EvaluatedMove> top, EvaluatedMove? actual)
        {
            return top.Take(3).Any(m => m.Type == MoveType.Winning || m.Type == MoveType.MakeFour) &&
                   (actual == null || (actual.Type != MoveType.Winning && actual.Type != MoveType.MakeFour));
        }

        public PlayerSkillLevel GetCurrentSkillLevel()
        {
            if (moveHistory.Count < 5) return PlayerSkillLevel.Beginner;
            var recent = moveHistory.TakeLast(recentMovesWindow).ToList();
            double avg = recent.Average(m => m.Quality);
            double threatMiss = recent.Count(m => m.MissedThreat) / (double)recent.Count;
            double oppMiss = recent.Count(m => m.MissedOpportunity) / (double)recent.Count;

            if (avg > 0.85 && threatMiss < 0.1 && oppMiss < 0.15) return PlayerSkillLevel.Expert;
            if (avg > 0.70 && threatMiss < 0.2 && oppMiss < 0.3) return PlayerSkillLevel.Advanced;
            if (avg > 0.55 && threatMiss < 0.35) return PlayerSkillLevel.Intermediate;
            if (avg > 0.40) return PlayerSkillLevel.Beginner;
            return PlayerSkillLevel.Novice;
        }

        public double GetSkillScore()
        {
            if (moveHistory.Count < 3) return 50.0;
            var recent = moveHistory.TakeLast(recentMovesWindow).ToList();
            double q = recent.Average(m => m.Quality) * 60;
            double t = (1 - recent.Count(m => m.MissedThreat) / (double)recent.Count) * 25;
            double o = (1 - recent.Count(m => m.MissedOpportunity) / (double)recent.Count) * 15;
            return Math.Min(100, q + t + o);
        }

        public PlayerWeaknesses AnalyzeWeaknesses()
        {
            var recent = moveHistory.TakeLast(20).ToList();
            if (recent.Count == 0) return new PlayerWeaknesses();
            return new PlayerWeaknesses
            {
                WeakDefense = recent.Count(m => m.MissedThreat) > recent.Count * 0.3,
                WeakAttack = recent.Count(m => m.MissedOpportunity) > recent.Count * 0.3,
                InconsistentPlay = StdDev(recent.Select(m => m.Quality)) > 0.25,
                TooFast = recent.Average(m => m.ThinkingTime) < 2000,
                TooSlow = recent.Average(m => m.ThinkingTime) > 30000
            };
        }

        private double StdDev(IEnumerable<double> values)
        {
            var list = values.ToList();
            if (list.Count == 0) return 0;
            double avg = list.Average();
            return Math.Sqrt(list.Sum(v => Math.Pow(v - avg, 2)) / list.Count);
        }

        public void Reset() => moveHistory.Clear();
    }
}
