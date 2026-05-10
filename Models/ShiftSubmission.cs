using System;
using System.ComponentModel.DataAnnotations;

namespace sumile.Models
{
    public class ShiftSubmission
    {
        public int Id { get; set; }

        // 新構造：ShiftDay 経由で日付と期間を管理
        public int ShiftDayId { get; set; }
        public ShiftDay ShiftDay { get; set; } = null!;

        public ShiftType ShiftType { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public ShiftState ShiftStatus { get; set; }

        public bool IsSelected { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public ShiftSubmissionSource Source { get; set; } = ShiftSubmissionSource.UserSubmitted;

        public UserShiftRole UserShiftRole { get; set; }
    }
}
