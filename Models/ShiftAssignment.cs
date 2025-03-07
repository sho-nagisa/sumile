using System;

namespace sumile.Models
{
    /// <summary>
    /// あるユーザーが特定のシフトにアサインされている情報
    /// </summary>
    public class ShiftAssignment
    {
        public int Id { get; set; }

        /// <summary>紐づくシフトID (FK: Shift.Id)</summary>
        public int ShiftId { get; set; }
        public Shift Shift { get; set; }

        /// <summary>紐づくユーザーID (FK: AspNetUsers.Id)</summary>
        public string UserId { get; set; }
        // ApplicationUserを参照する場合は型をApplicationUserに
        public ApplicationUser User { get; set; }

        /// <summary>割り当てられた日時など</summary>
        public DateTime AssignedAt { get; set; } = DateTime.Now;

        /// <summary>シフトが最終確定かどうか</summary>
        public bool IsConfirmed { get; set; } = true;
    }
}
