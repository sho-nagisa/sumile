using System;
using System.ComponentModel.DataAnnotations;

namespace sumile.Models
{
    public class ShiftSubmission
    {
        public int Id { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        public string ShiftType { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        // ★ enumに置き換え
        public ShiftState ShiftStatus { get; set; }

        public bool IsSelected { get; set; }

        public DateTime? SubmittedAt { get; set; }

        public string UserType { get; set; } // ←追加済み項目
    }
}
