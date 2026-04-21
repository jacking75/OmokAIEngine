using System;
using System.Collections.Generic;
using System.Linq;
using GomokuEngine.Core;
using GomokuEngine.Evaluation;
using GomokuEngine.Search;
using GomokuEngine.Analysis;

namespace GomokuEngine.AI
{
    public class AdaptiveGomokuAI
    {
        private GomokuBoard board;
        private MinimaxEngine minimaxEngine;
        private VCFEngine vcfEngine;
        private RenjuRuleChecker renjuChecker;
        public PlayerSkillAnalyzer analyzer;
        private DifficultyConfig currentConfig;
        private Random random;
        private int consecutiveWins;
        private int consecutiveLosses;
        private bool useRenjuRules;

        public class DifficultyConfig
        {
            public double OptimalMoveProb { get; set; }
            public double GoodMoveProb { get; set; }
            public double MistakeProbability { get; set; }
            public bool IgnoreCriticalThreats { get; set; }
            public int SearchDepth { get; set; }
            public string Description { get; set; } = "";
        }

        public AdaptiveGomokuAI(bool useRenjuRules = false)
        {
            board = new GomokuBoard();
            minimaxEngine = new MinimaxEngine(board);
            vcfEngine = new VCFEngine(board, 12);
            renjuChecker = new RenjuRuleChecker(board);
            analyzer = new PlayerSkillAnalyzer();
            random = new Random();
            this.useRenjuRules = useRenjuRules;
            currentConfig = GetConfigForSkillLevel(PlayerSkillLevel.Beginner);
        }

        public EvaluatedMove? GetAIMove(Stone aiStone, Position lastPlayerMove, long playerThinkingTime)
        {
            if (lastPlayerMove.Row >= 0 && lastPlayerMove.Col >= 0)
            {
                analyzer.AnalyzePlayerMove(board, lastPlayerMove, GetOpponent(aiStone), playerThinkingTime);
                UpdateDifficulty();
            }

            var vcfResult = vcfEngine.FindVCFSequence(aiStone);
            if (vcfResult.IsVCF && vcfResult.WinningSequence.Count > 0 &&
                random.NextDouble() < currentConfig.OptimalMoveProb)
            {
                return new EvaluatedMove(vcfResult.WinningSequence[0], 1000000, MoveType.Winning, "VCF");
            }

            var topMoves = GetTopMoves(aiStone, 15);
            if (topMoves.Count == 0) return null;
            return SelectMoveAdaptively(topMoves, aiStone);
        }

        private List<EvaluatedMove> GetTopMoves(Stone aiStone, int count)
        {
            var evaluator = new MoveEvaluator(board);
            var moves = evaluator.EvaluateAllMoves(aiStone, count);
            if (useRenjuRules && aiStone == Stone.Black)
                moves = moves.Where(m => renjuChecker.CheckForbiddenMove(m.Position, aiStone).IsAllowed).ToList();
            return moves;
        }

        private EvaluatedMove SelectMoveAdaptively(List<EvaluatedMove> moves, Stone aiStone)
        {
            var critical = moves.FirstOrDefault(m => m.Type == MoveType.Winning || m.Type == MoveType.DefendFour);
            if (critical != null && (currentConfig.IgnoreCriticalThreats == false || random.NextDouble() > 0.1))
                return critical;

            double r = random.NextDouble();
            if (r < currentConfig.OptimalMoveProb)
                return moves[0];
            if (r < currentConfig.OptimalMoveProb + currentConfig.GoodMoveProb)
                return moves[random.Next(1, Math.Min(4, moves.Count))];
            return moves[random.Next(4, Math.Min(10, moves.Count))];
        }

        private void UpdateDifficulty()
        {
            currentConfig = GetConfigForSkillLevel(analyzer.GetCurrentSkillLevel());
            if (consecutiveWins >= 3)
            {
                currentConfig.OptimalMoveProb = Math.Min(0.95, currentConfig.OptimalMoveProb + 0.10);
                currentConfig.MistakeProbability = Math.Max(0.01, currentConfig.MistakeProbability - 0.05);
            }
            else if (consecutiveLosses >= 3)
            {
                currentConfig.OptimalMoveProb = Math.Max(0.15, currentConfig.OptimalMoveProb - 0.10);
                currentConfig.MistakeProbability = Math.Min(0.50, currentConfig.MistakeProbability + 0.10);
            }
            var w = analyzer.AnalyzeWeaknesses();
            if (w.WeakDefense) currentConfig.OptimalMoveProb *= 0.95;
            if (w.WeakAttack) currentConfig.MistakeProbability += 0.05;
        }

        private DifficultyConfig GetConfigForSkillLevel(PlayerSkillLevel level) => level switch
        {
            PlayerSkillLevel.Novice => new DifficultyConfig
            { OptimalMoveProb = 0.20, GoodMoveProb = 0.40, MistakeProbability = 0.40, SearchDepth = 1, Description = "Very Easy" },
            PlayerSkillLevel.Beginner => new DifficultyConfig
            { OptimalMoveProb = 0.35, GoodMoveProb = 0.45, MistakeProbability = 0.20, SearchDepth = 1, Description = "Easy" },
            PlayerSkillLevel.Intermediate => new DifficultyConfig
            { OptimalMoveProb = 0.50, GoodMoveProb = 0.35, MistakeProbability = 0.15, SearchDepth = 2, Description = "Medium" },
            PlayerSkillLevel.Advanced => new DifficultyConfig
            { OptimalMoveProb = 0.70, GoodMoveProb = 0.25, MistakeProbability = 0.05, SearchDepth = 3, Description = "Hard" },
            PlayerSkillLevel.Expert => new DifficultyConfig
            { OptimalMoveProb = 0.85, GoodMoveProb = 0.13, MistakeProbability = 0.02, SearchDepth = 4, Description = "Expert" },
            _ => new DifficultyConfig
            { OptimalMoveProb = 0.50, GoodMoveProb = 0.35, MistakeProbability = 0.15, SearchDepth = 2, Description = "Default" }
        };

        public void RecordGameResult(bool aiWon)
        {
            if (aiWon) { consecutiveWins++; consecutiveLosses = 0; }
            else { consecutiveLosses++; consecutiveWins = 0; }
        }

        public void StartNewGame() { board.Clear(); minimaxEngine.ClearCache(); }

        public void ResetAll()
        {
            board.Clear(); minimaxEngine.ClearCache(); analyzer.Reset();
            consecutiveWins = consecutiveLosses = 0;
            currentConfig = GetConfigForSkillLevel(PlayerSkillLevel.Beginner);
        }

        public GomokuBoard GetBoard() => board;

        public AIStatus GetStatus() => new AIStatus
        {
            PlayerSkillLevel = analyzer.GetCurrentSkillLevel(),
            PlayerSkillScore = analyzer.GetSkillScore(),
            CurrentDifficulty = currentConfig.Description,
            ConsecutiveWins = consecutiveWins,
            ConsecutiveLosses = consecutiveLosses,
            Weaknesses = analyzer.AnalyzeWeaknesses()
        };

        private static Stone GetOpponent(Stone s) => s == Stone.Black ? Stone.White : Stone.Black;
    }

    public class AIStatus
    {
        public PlayerSkillLevel PlayerSkillLevel { get; set; }
        public double PlayerSkillScore { get; set; }
        public string CurrentDifficulty { get; set; } = "";
        public int ConsecutiveWins { get; set; }
        public int ConsecutiveLosses { get; set; }
        public PlayerWeaknesses Weaknesses { get; set; } = new PlayerWeaknesses();
    }
}
