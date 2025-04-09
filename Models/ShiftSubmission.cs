using System;
using System.ComponentModel.DataAnnotations;

namespace sumile.Models
{
    public class ShiftSubmission
    {
        public int Id { get; set; }
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }
        public ShiftType ShiftType { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public ShiftState ShiftStatus { get; set; }
        public bool IsSelected { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public UserType UserType { get; set; }
        public int RecruitmentPeriodId { get; set; }
        public RecruitmentPeriod RecruitmentPeriod { get; set; }
        public UserShiftRole UserShiftRole { get; set; }
    }
}
