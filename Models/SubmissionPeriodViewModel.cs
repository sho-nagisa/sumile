using System;
using System.ComponentModel.DataAnnotations;

namespace sumile.Models
{
    public class SubmissionPeriodViewModel
    {
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "提出開始日")]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "提出終了日")]
        public DateTime EndDate { get; set; }
    }
}
