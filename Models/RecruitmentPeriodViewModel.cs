using System;
using System.ComponentModel.DataAnnotations;

namespace sumile.Models
{
    public class RecruitmentPeriodViewModel
    {
        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "募集開始日")]
        public DateTime StartDate { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "募集終了日")]
        public DateTime EndDate { get; set; }
    }
}
