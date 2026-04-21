using System.Collections.Generic;
using GomokuEngine.Core;

namespace GomokuEngine.Evaluation
{
    public static class PatternAnalyzer
    {
        private static readonly Dictionary<string, int> PatternScores = new Dictionary<string, int>
        {
            ["Five"]      = 100000,
            ["OpenFour"]  = 50000,
            ["Four"]      = 10000,
            ["OpenThree"] = 5000,
            ["Three"]     = 1000,
            ["OpenTwo"]   = 500,
            ["Two"]       = 100,
            ["One"]       = 10
        };

        public static Dictionary<string, Pattern> AnalyzePosition(GomokuBoard board, Position pos, Stone stone)
        {
            var patterns = new Dictionary<string, Pattern>();
            var directions = new[] { (0, 1), (1, 0), (1, 1), (1, -1) };
            string[] dirNames = { "Horizontal", "Vertical", "Diagonal1", "Diagonal2" };

            for (int i = 0; i < directions.Length; i++)
            {
                var (dx, dy) = directions[i];
                patterns[dirNames[i]] = AnalyzeDirection(board, pos, stone, dx, dy);
            }

            return patterns;
        }

        private static Pattern AnalyzeDirection(GomokuBoard board, Position pos, Stone stone, int dx, int dy)
        {
            var (forwardConsec, forwardOpen, forwardSpace, forwardLen) = ScanDirection(board, pos, stone, dx, dy);
            var (backwardConsec, backwardOpen, backwardSpace, backwardLen) = ScanDirection(board, pos, stone, -dx, -dy);

            return new Pattern(
                1 + forwardConsec + backwardConsec,
                forwardOpen + backwardOpen,
                forwardSpace || backwardSpace,
                1 + forwardLen + backwardLen
            );
        }

        private static (int consecutive, int openEnd, bool hasSpace, int length)
            ScanDirection(GomokuBoard board, Position pos, Stone stone, int dx, int dy)
        {
            int consecutive = 0;
            int length = 0;
            bool hasSpace = false;
            bool foundSpace = false;
            int openEnd = 0;
            int row = pos.Row + dx;
            int col = pos.Col + dy;

            for (int i = 0; i < 5; i++)
            {
                if (!board.IsValidPosition(row, col)) break;
                Stone current = board.GetStone(row, col);

                if (current == stone)
                {
                    consecutive++;
                    length++;
                }
                else if (current == Stone.Empty && !foundSpace && consecutive > 0)
                {
                    hasSpace = true;
                    foundSpace = true;
                    length++;
                }
                else if (current == Stone.Empty)
                {
                    openEnd = 1;
                    break;
                }
                else
                {
                    break;
                }

                row += dx;
                col += dy;
            }

            return (consecutive, openEnd, hasSpace, length);
        }

        public static int CalculatePatternScore(Pattern pattern)
        {
            int consec = pattern.ConsecutiveStones;
            int open = pattern.OpenEnds;

            if (consec >= 5) return PatternScores["Five"];

            if (consec == 4)
            {
                if (open == 2) return PatternScores["OpenFour"];
                if (open == 1) return PatternScores["Four"];
                return PatternScores["Four"] / 2;
            }

            if (consec == 3)
            {
                if (open == 2) return PatternScores["OpenThree"];
                if (open == 1) return PatternScores["Three"];
                return PatternScores["Three"] / 2;
            }

            if (consec == 2)
            {
                if (open == 2) return PatternScores["OpenTwo"];
                if (open == 1) return PatternScores["Two"];
                return PatternScores["Two"] / 2;
            }

            if (consec == 1) return PatternScores["One"];

            return 0;
        }
    }
}
