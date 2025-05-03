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

        [Required]
        public string RequestedByUserId { get; set; }
        [ForeignKey("RequestedByUserId")]
        public ApplicationUser RequestedByUser { get; set; }

        public string? AcceptedByUserId { get; set; }
        [ForeignKey("AcceptedByUserId")]
        public ApplicationUser? AcceptedByUser { get; set; }

        public int OfferedShiftSubmissionId { get; set; }
        [ForeignKey("OfferedShiftSubmissionId")]
        public ShiftSubmission OfferedShiftSubmission { get; set; }

        public int? AcceptedShiftSubmissionId { get; set; }
        [ForeignKey("AcceptedShiftSubmissionId")]
        public ShiftSubmission? AcceptedShiftSubmission { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [Required]
        public string Status { get; set; }
    }
}
