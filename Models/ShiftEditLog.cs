namespace sumile.Models
{
    public class ShiftEditLog
    {
        public int Id { get; set; }

        public string AdminUserId { get; set; } = string.Empty;
        public ApplicationUser AdminUser { get; set; } = null!;

        public string TargetUserId { get; set; } = string.Empty;
        public ApplicationUser TargetUser { get; set; } = null!;

        public DateTime EditDate { get; set; }

        // ✅ ShiftDayIdで日付と募集期間を一元管理
        public int ShiftDayId { get; set; }
        public ShiftDay ShiftDay { get; set; } = null!;

        public ShiftType ShiftType { get; set; }

        public ShiftState OldState { get; set; }
        public ShiftState NewState { get; set; }

        public string Note { get; set; } = string.Empty;  // 任意のコメント欄
    }
}
