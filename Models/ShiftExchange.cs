using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace sumile.Models
{
    public class ShiftExchange
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        // リクエストしたユーザー（投稿者）
        [Required]
        public string RequestedByUserId { get; set; }
        [ForeignKey("RequestedByUserId")]
        public ApplicationUser RequestedByUser { get; set; }

        // 応募したユーザー（成立者）
        public string? AcceptedByUserId { get; set; }
        [ForeignKey("AcceptedByUserId")]
        public ApplicationUser? AcceptedByUser { get; set; }

        // 提示されたシフト
        public int OfferedShiftSubmissionId { get; set; }
        public ShiftSubmission OfferedShiftSubmission { get; set; }

        // 応募してきたシフト（成立した場合）
        public int? AcceptedShiftSubmissionId { get; set; }
        public ShiftSubmission? AcceptedShiftSubmission { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime UpdatedAt { get; set; } // ← これを追加！


        // 状態（例: "Open", "Accepted", "Closed" など）
        [Required]
        public string Status { get; set; }
    }
}
