namespace GomokuEngine.Evaluation
{
    public class Pattern
    {
        public int ConsecutiveStones { get; set; }
        public int OpenEnds { get; set; }
        public bool HasSpace { get; set; }
        public int TotalLength { get; set; }

        public Pattern(int consecutive, int openEnds, bool hasSpace = false, int totalLength = 0)
        {
            ConsecutiveStones = consecutive;
            OpenEnds = openEnds;
            HasSpace = hasSpace;
            TotalLength = totalLength;
        }
    }
}
