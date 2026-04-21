using System;
using GomokuEngine.Core;

namespace GomokuEngine.Search
{
    public class ZobristHasher
    {
        private readonly ulong[,,] table;
        private readonly Random rng;
        private const int BOARD_SIZE = 15;

        public ZobristHasher(int seed = 12345)
        {
            rng = new Random(seed);
            table = new ulong[BOARD_SIZE, BOARD_SIZE, 3];
            for (int i = 0; i < BOARD_SIZE; i++)
                for (int j = 0; j < BOARD_SIZE; j++)
                    for (int k = 0; k < 3; k++)
                    {
                        byte[] buf = new byte[8];
                        rng.NextBytes(buf);
                        table[i, j, k] = BitConverter.ToUInt64(buf, 0);
                    }
        }

        public ulong ComputeHash(GomokuBoard board)
        {
            ulong hash = 0;
            int size = board.GetBoardSize();
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    Stone s = board.GetStone(i, j);
                    if (s != Stone.Empty)
                        hash ^= table[i, j, (int)s];
                }
            return hash;
        }

        public ulong UpdateHash(ulong current, Position pos, Stone stone) =>
            current ^ table[pos.Row, pos.Col, (int)stone];

        public ulong RemoveFromHash(ulong current, Position pos, Stone stone) =>
            current ^ table[pos.Row, pos.Col, (int)stone];
    }
}
