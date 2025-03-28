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

        public DateTime ShiftDate { get; set; }
        public string ShiftType { get; set; }

        public ShiftState OldState { get; set; }
        public ShiftState NewState { get; set; }

        public string Note { get; set; }  // 任意のコメント欄
    }

}
