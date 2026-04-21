namespace GomokuEngine.AI
{
    public class LevelProfile
    {
        public int SearchDepth { get; set; }
        public double OptimalProb { get; set; }
        public double GoodProb { get; set; }
        public double MistakeProb { get; set; }
        public int VcfDepth { get; set; }   // 0 = VCF 비활성
        public int TimeLimitMs { get; set; }
        public string Name { get; set; } = "";

        // 20단계 프로파일 테이블
        // depth + 실수 확률 + VcfDepth 조합으로 단계별 체감 차이 제공
        public static readonly LevelProfile[] Profiles = new[]
        {
            // ── depth 1 (Lv1~4) ──────────────────────────────────────────────
            new LevelProfile { SearchDepth=1, OptimalProb=0.05, GoodProb=0.20, MistakeProb=0.75, VcfDepth=0, TimeLimitMs=300,  Name="Lv1 왕초보" },
            new LevelProfile { SearchDepth=1, OptimalProb=0.10, GoodProb=0.25, MistakeProb=0.65, VcfDepth=0, TimeLimitMs=400,  Name="Lv2 입문" },
            new LevelProfile { SearchDepth=1, OptimalProb=0.20, GoodProb=0.30, MistakeProb=0.50, VcfDepth=0, TimeLimitMs=500,  Name="Lv3 초급" },
            new LevelProfile { SearchDepth=1, OptimalProb=0.35, GoodProb=0.30, MistakeProb=0.35, VcfDepth=0, TimeLimitMs=600,  Name="Lv4 초급+" },
            // ── depth 2 (Lv5~9) ──────────────────────────────────────────────
            new LevelProfile { SearchDepth=2, OptimalProb=0.50, GoodProb=0.30, MistakeProb=0.20, VcfDepth=0, TimeLimitMs=800,  Name="Lv5 중급" },
            new LevelProfile { SearchDepth=2, OptimalProb=0.62, GoodProb=0.28, MistakeProb=0.10, VcfDepth=0, TimeLimitMs=1000, Name="Lv6 중급+" },
            new LevelProfile { SearchDepth=2, OptimalProb=0.72, GoodProb=0.23, MistakeProb=0.05, VcfDepth=0, TimeLimitMs=1500, Name="Lv7 중급++" },
            new LevelProfile { SearchDepth=2, OptimalProb=0.82, GoodProb=0.16, MistakeProb=0.02, VcfDepth=0, TimeLimitMs=2000, Name="Lv8 고급" },
            new LevelProfile { SearchDepth=2, OptimalProb=0.90, GoodProb=0.09, MistakeProb=0.01, VcfDepth=0, TimeLimitMs=2500, Name="Lv9 고급+" },
            // ── depth 3 (Lv10~13) ────────────────────────────────────────────
            new LevelProfile { SearchDepth=3, OptimalProb=0.92, GoodProb=0.07, MistakeProb=0.01, VcfDepth=0, TimeLimitMs=3000, Name="Lv10 고급++" },
            new LevelProfile { SearchDepth=3, OptimalProb=0.94, GoodProb=0.05, MistakeProb=0.01, VcfDepth=0, TimeLimitMs=3500, Name="Lv11 전문" },
            new LevelProfile { SearchDepth=3, OptimalProb=0.96, GoodProb=0.04, MistakeProb=0.00, VcfDepth=0, TimeLimitMs=4000, Name="Lv12 전문+" },
            new LevelProfile { SearchDepth=3, OptimalProb=0.98, GoodProb=0.02, MistakeProb=0.00, VcfDepth=0, TimeLimitMs=4500, Name="Lv13 전문++" },
            // ── depth 4 (Lv14~17) ────────────────────────────────────────────
            new LevelProfile { SearchDepth=4, OptimalProb=0.98, GoodProb=0.02, MistakeProb=0.00, VcfDepth=0,  TimeLimitMs=5000, Name="Lv14 마스터" },
            new LevelProfile { SearchDepth=4, OptimalProb=0.99, GoodProb=0.01, MistakeProb=0.00, VcfDepth=0,  TimeLimitMs=5500, Name="Lv15 마스터+" },
            new LevelProfile { SearchDepth=4, OptimalProb=1.00, GoodProb=0.00, MistakeProb=0.00, VcfDepth=0,  TimeLimitMs=6000, Name="Lv16 마스터++" },
            new LevelProfile { SearchDepth=4, OptimalProb=1.00, GoodProb=0.00, MistakeProb=0.00, VcfDepth=8,  TimeLimitMs=6500, Name="Lv17 그랜드마스터" },
            // ── depth 5 + VCF (Lv18~20) ──────────────────────────────────────
            new LevelProfile { SearchDepth=5, OptimalProb=1.00, GoodProb=0.00, MistakeProb=0.00, VcfDepth=10, TimeLimitMs=7000, Name="Lv18 고수" },
            new LevelProfile { SearchDepth=5, OptimalProb=1.00, GoodProb=0.00, MistakeProb=0.00, VcfDepth=12, TimeLimitMs=8000, Name="Lv19 최강" },
            new LevelProfile { SearchDepth=5, OptimalProb=1.00, GoodProb=0.00, MistakeProb=0.00, VcfDepth=14, TimeLimitMs=9000, Name="Lv20 신" },
        };
    }
}
