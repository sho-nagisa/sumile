namespace sumile.Models
{
    public class ShiftEditLog
    {
        public int Id { get; set; }

        public string AdminUserId { get; set; }
        public ApplicationUser AdminUser { get; set; }

        public string TargetUserId { get; set; }
        public ApplicationUser TargetUser { get; set; }

        public DateTime EditDate { get; set; }

        // ✅ ShiftDayIdで日付と募集期間を一元管理
        public int ShiftDayId { get; set; }
        public ShiftDay ShiftDay { get; set; }

        public ShiftType ShiftType { get; set; }

        public ShiftState OldState { get; set; }
        public ShiftState NewState { get; set; }

        public string Note { get; set; }  // 任意のコメント欄
    }
}
