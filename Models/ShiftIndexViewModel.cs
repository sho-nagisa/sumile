using System;
using System.Collections.Generic;

namespace sumile.Models
{
    public class ShiftIndexViewModel
    {
        public string CurrentUserCustomId { get; set; } = string.Empty;
        public string CurrentUserName { get; set; } = string.Empty;

        public List<UserInfo> Users { get; set; } = new();
        public List<DateTime> Dates { get; set; } = new();
        public List<SubmissionInfo> Submissions { get; set; } = new();
        public List<RecruitmentPeriod> RecruitmentPeriods { get; set; } = new();
        public int? SelectedPeriodId { get; set; }

        public class UserInfo
        {
            public string Id { get; set; } = string.Empty;
            public int CustomId { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class SubmissionInfo
        {
            public string UserId { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public string ShiftType { get; set; } = string.Empty;

            // ★ ここを追加！
            public ShiftState ShiftStatus { get; set; }
        }
    }
}
