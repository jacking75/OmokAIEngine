using System;
using System.Collections.Generic;

namespace GomokuEngine.Core
{
    public class GomokuBoard
    {
        private const int BOARD_SIZE = 15;
        private Stone[,] board;
        private List<Position> moveHistory;

        private static readonly (int dx, int dy)[] Directions =
        {
            (0, 1),
            (1, 0),
            (1, 1),
            (1, -1)
        };

        public GomokuBoard()
        {
            board = new Stone[BOARD_SIZE, BOARD_SIZE];
            moveHistory = new List<Position>();
        }

        public bool IsValidPosition(int row, int col) =>
            row >= 0 && row < BOARD_SIZE && col >= 0 && col < BOARD_SIZE;

        public bool IsEmpty(int row, int col) =>
            IsValidPosition(row, col) && board[row, col] == Stone.Empty;

        public Stone GetStone(int row, int col) =>
            IsValidPosition(row, col) ? board[row, col] : Stone.Empty;

        public bool PlaceStone(Position pos, Stone stone)
        {
            if (!IsEmpty(pos.Row, pos.Col) || stone == Stone.Empty)
                return false;
            board[pos.Row, pos.Col] = stone;
            moveHistory.Add(pos);
            return true;
        }

        public bool RemoveStone(Position pos)
        {
            if (!IsValidPosition(pos.Row, pos.Col))
                return false;
            board[pos.Row, pos.Col] = Stone.Empty;
            if (moveHistory.Count > 0 && moveHistory[moveHistory.Count - 1].Equals(pos))
                moveHistory.RemoveAt(moveHistory.Count - 1);
            return true;
        }

        public List<Position> GetEmptyPositions()
        {
            var positions = new List<Position>();
            for (int i = 0; i < BOARD_SIZE; i++)
                for (int j = 0; j < BOARD_SIZE; j++)
                    if (board[i, j] == Stone.Empty)
                        positions.Add(new Position(i, j));
            return positions;
        }

        public bool HasNeighbor(Position pos, int distance = 2)
        {
            for (int i = Math.Max(0, pos.Row - distance);
                 i <= Math.Min(BOARD_SIZE - 1, pos.Row + distance); i++)
                for (int j = Math.Max(0, pos.Col - distance);
                     j <= Math.Min(BOARD_SIZE - 1, pos.Col + distance); j++)
                    if (board[i, j] != Stone.Empty)
                        return true;
            return false;
        }

        public bool CheckWin(Position lastMove, Stone stone)
        {
            if (stone == Stone.Empty) return false;
            foreach (var (dx, dy) in Directions)
            {
                int count = 1;
                count += CountConsecutive(lastMove, stone, dx, dy);
                count += CountConsecutive(lastMove, stone, -dx, -dy);
                if (count >= 5) return true;
            }
            return false;
        }

        public int CountConsecutive(Position pos, Stone stone, int dx, int dy)
        {
            int count = 0;
            int row = pos.Row + dx;
            int col = pos.Col + dy;
            while (IsValidPosition(row, col) && board[row, col] == stone)
            {
                count++;
                row += dx;
                col += dy;
            }
            return count;
        }

        public Stone[,] GetBoardCopy() => (Stone[,])board.Clone();

        public int GetBoardSize() => BOARD_SIZE;

        public List<Position> GetMoveHistory() => new List<Position>(moveHistory);

        public void Clear()
        {
            for (int i = 0; i < BOARD_SIZE; i++)
                for (int j = 0; j < BOARD_SIZE; j++)
                    board[i, j] = Stone.Empty;
            moveHistory.Clear();
        }
    }
}
