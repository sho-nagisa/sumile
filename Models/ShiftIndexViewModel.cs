namespace sumile.Models
{
    public class ShiftIndexViewModel
    {
        public string CurrentUserCustomId { get; set; }
        public string CurrentUserName { get; set; }

        public List<DateTime> Dates { get; set; } = new List<DateTime>();
        public List<UserInfo> Users { get; set; } = new List<UserInfo>();
        public List<SubmissionInfo> Submissions { get; set; } = new List<SubmissionInfo>();
        public List<RecruitmentPeriod> RecruitmentPeriods { get; set; } = new List<RecruitmentPeriod>();
        public int? SelectedPeriodId { get; set; }

        public class UserInfo
        {
            public string Id { get; set; }
            public int CustomId { get; set; }
            public string Name { get; set; }
        }

        public class SubmissionInfo
        {
            public string UserId { get; set; }
            public DateTime Date { get; set; }
            public string ShiftType { get; set; }
        }
    }
}
