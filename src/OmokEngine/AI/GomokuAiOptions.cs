using GomokuEngine.Core;

namespace GomokuEngine.AI
{
    public class GomokuAiOptions
    {
        /// <summary>AI 실력 단계 1(최약) ~ 10(최강)</summary>
        public int Level { get; set; } = 5;

        /// <summary>렌주 금수 규칙 (흑 33/44/장목) 적용 여부</summary>
        public bool UseRenju { get; set; } = false;

        /// <summary>AI가 사용하는 돌 색상</summary>
        public Stone AiStone { get; set; } = Stone.White;
    }
}
