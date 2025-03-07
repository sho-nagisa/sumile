using System;
using System.ComponentModel.DataAnnotations;

namespace sumile.Models
{
    public class ShiftSubmission
    {
        public int Id { get; set; }

        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        // 例: "Morning" または "Night"
        public string ShiftType { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        // ユーザーがそのシフトを提出（選択）したか否か
        public bool IsSelected { get; set; }

        // 提出日時（更新時に記録）
        public DateTime? SubmittedAt { get; set; }
    }
}
