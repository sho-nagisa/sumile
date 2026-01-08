using sumile.Models;

namespace sumile.ViewModels
{
    public class ShiftColumnViewModel
    {
        public int ShiftDayId { get; set; }

        // Morning / Night
        public ShiftType ShiftType { get; set; }

        // 枚数系
        public int AcceptedCount { get; set; }     // 全体の〇数
        public int KeyHolderCount { get; set; }    // 赤丸
        public int RequiredWorkers { get; set; }   // 必要人数
        public int RemainingWorkers { get; set; }  // 必要人数 - 〇数（マイナス可）
    }
}

