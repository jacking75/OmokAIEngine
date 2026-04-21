using System;

namespace GomokuEngine.Core
{
    public struct Position : IEquatable<Position>
    {
        public int Row { get; set; }
        public int Col { get; set; }

        public Position(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public bool Equals(Position other) => Row == other.Row && Col == other.Col;

        public override bool Equals(object? obj) => obj is Position other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Row, Col);

        public static bool operator ==(Position left, Position right) => left.Equals(right);
        public static bool operator !=(Position left, Position right) => !left.Equals(right);

        public override string ToString() => $"({Row}, {Col})";
    }

    public class EvaluatedMove
    {
        public Position Position { get; set; }
        public int Score { get; set; }
        public MoveType Type { get; set; }
        public string Description { get; set; }

        public EvaluatedMove(Position pos, int score, MoveType type, string desc = "")
        {
            Position = pos;
            Score = score;
            Type = type;
            Description = desc;
        }

        public override string ToString() => $"Move: {Position}, Score: {Score}, Type: {Type}";
    }
}
