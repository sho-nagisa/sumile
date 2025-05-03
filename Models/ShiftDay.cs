using sumile.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace sumile.Models
{
    public class ShiftDay
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }

        public int RecruitmentPeriodId { get; set; }
        public RecruitmentPeriod RecruitmentPeriod { get; set; }

        public ICollection<ShiftSubmission> ShiftSubmissions { get; set; }
        public DailyWorkload DailyWorkload { get; set; }
    }

}
