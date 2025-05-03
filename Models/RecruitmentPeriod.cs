using System;
using System.ComponentModel.DataAnnotations;

namespace sumile.Models
{
    public class RecruitmentPeriod
    {
        public int Id { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "募集開始日")]
        public DateTime StartDate { get; set; }
        [DataType(DataType.Date)]
        [Display(Name = "募集終了日")]
        public DateTime EndDate { get; set; }
        public bool IsOpen { get; set; } = true;
        public ICollection<ShiftDay> ShiftDays { get; set; }
    }
}
