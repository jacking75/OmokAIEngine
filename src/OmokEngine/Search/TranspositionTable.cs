using System;
using System.Collections.Generic;
using System.Linq;
using GomokuEngine.Core;

namespace GomokuEngine.Search
{
    public class TranspositionTable
    {
        private Dictionary<ulong, TTEntry> table;
        private readonly int maxEntries;

        public class TTEntry
        {
            public ulong Hash { get; set; }
            public int Score { get; set; }
            public int Depth { get; set; }
            public Position BestMove { get; set; }
            public EntryType Type { get; set; }
            public DateTime Timestamp { get; set; }

            public enum EntryType { Exact, LowerBound, UpperBound }
        }

        public TranspositionTable(int maxSizeMB = 128)
        {
            maxEntries = (maxSizeMB * 1024 * 1024) / 40;
            table = new Dictionary<ulong, TTEntry>(maxEntries);
        }

        public void Store(ulong hash, int score, int depth, Position bestMove, TTEntry.EntryType type)
        {
            if (table.Count >= maxEntries && !table.ContainsKey(hash))
                CleanOldEntries();

            if (table.TryGetValue(hash, out var existing))
                if (existing.Depth > depth && existing.Type == TTEntry.EntryType.Exact)
                    return;

            table[hash] = new TTEntry
            {
                Hash = hash, Score = score, Depth = depth,
                BestMove = bestMove, Type = type, Timestamp = DateTime.Now
            };
        }

        public bool Probe(ulong hash, int depth, int alpha, int beta, out int score, out Position bestMove)
        {
            score = 0;
            bestMove = new Position(-1, -1);
            if (!table.TryGetValue(hash, out var entry)) return false;
            if (entry.Depth < depth) return false;

            bestMove = entry.BestMove;
            switch (entry.Type)
            {
                case TTEntry.EntryType.Exact:
                    score = entry.Score;
                    return true;
                case TTEntry.EntryType.LowerBound:
                    if (entry.Score >= beta) { score = entry.Score; return true; }
                    break;
                case TTEntry.EntryType.UpperBound:
                    if (entry.Score <= alpha) { score = entry.Score; return true; }
                    break;
            }
            return false;
        }

        public Position GetBestMove(ulong hash) =>
            table.TryGetValue(hash, out var e) ? e.BestMove : new Position(-1, -1);

        private void CleanOldEntries()
        {
            var cutoff = DateTime.Now.AddSeconds(-30);
            var old = table.Where(kvp => kvp.Value.Timestamp < cutoff)
                           .Select(kvp => kvp.Key)
                           .Take(maxEntries / 4)
                           .ToList();
            foreach (var k in old) table.Remove(k);
        }

        public void Clear() => table.Clear();
        public int GetSize() => table.Count;
        public double GetUsagePercent() => (double)table.Count / maxEntries * 100;
    }
}
