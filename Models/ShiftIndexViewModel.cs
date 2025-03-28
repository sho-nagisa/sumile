using System;
using System.Collections.Generic;

namespace sumile.Models
{
    public class ShiftIndexViewModel
    {
        public string CurrentUserCustomId { get; set; }
        public string CurrentUserName { get; set; }

        public List<UserInfo> Users { get; set; }
        public List<DateTime> Dates { get; set; }
        public List<SubmissionInfo> Submissions { get; set; }
        public List<RecruitmentPeriod> RecruitmentPeriods { get; set; }
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

            // ★ ここを追加！
            public ShiftState ShiftStatus { get; set; }
        }
    }
}
