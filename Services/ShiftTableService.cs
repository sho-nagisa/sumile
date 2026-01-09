using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sumile.Services
{
    public class ShiftTableService
    {
        private readonly ApplicationDbContext _context;

        public ShiftTableService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ShiftTableResult> BuildAsync(int? periodId)
        {
            // ===== 募集期間 =====
            var allPeriods = await _context.RecruitmentPeriods
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            var selectedPeriod = periodId.HasValue
                ? allPeriods.FirstOrDefault(r => r.Id == periodId.Value)
                : allPeriods.FirstOrDefault();

            if (selectedPeriod == null)
            {
                return new ShiftTableResult();
            }

            // ===== ShiftDay =====
            var shiftDays = await _context.ShiftDays
                .Where(d => d.RecruitmentPeriodId == selectedPeriod.Id)
                .OrderBy(d => d.Date)
                .ToListAsync();

            // ===== DailyWorkload =====
            var workloads = await _context.DailyWorkloads
                .Where(w => shiftDays.Select(sd => sd.Id).Contains(w.ShiftDayId))
                .Include(w => w.ShiftDay)
                .ToListAsync();

            // ===== 前期間最終日 workload を先頭に追加 =====
            var prevPeriod = allPeriods
                .Where(p => p.Id < selectedPeriod.Id)
                .OrderByDescending(p => p.Id)
                .FirstOrDefault();

            if (prevPeriod != null)
            {
                var prevLastShiftDayId = await _context.ShiftDays
                    .Where(sd => sd.RecruitmentPeriodId == prevPeriod.Id)
                    .OrderByDescending(sd => sd.Date)
                    .Select(sd => sd.Id)
                    .FirstOrDefaultAsync();

                if (prevLastShiftDayId != 0)
                {
                    var prevWorkload = await _context.DailyWorkloads
                        .Include(w => w.ShiftDay)
                        .FirstOrDefaultAsync(w => w.ShiftDayId == prevLastShiftDayId);

                    if (prevWorkload != null)
                    {
                        workloads.Insert(0, prevWorkload);
                    }
                }
            }

            // ===== Submissions =====
            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            var submissions = await _context.ShiftSubmissions
                .Where(s => shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();

            // ================================
            // ① Accepted 数（2n 列）
            // ================================
            var totalAcceptedList = new List<int>();
            var keyHolderAcceptedList = new List<int>();

            foreach (var day in shiftDays)
            {
                // Morning
                totalAcceptedList.Add(
                    submissions.Count(s =>
                        s.ShiftDayId == day.Id &&
                        s.ShiftType == ShiftType.Morning &&
                        s.ShiftStatus == ShiftState.Accepted)
                );

                keyHolderAcceptedList.Add(
                    submissions.Count(s =>
                        s.ShiftDayId == day.Id &&
                        s.ShiftType == ShiftType.Morning &&
                        s.ShiftStatus == ShiftState.Accepted &&
                        s.UserShiftRole == UserShiftRole.KeyHolder)
                );

                // Night
                totalAcceptedList.Add(
                    submissions.Count(s =>
                        s.ShiftDayId == day.Id &&
                        s.ShiftType == ShiftType.Night &&
                        s.ShiftStatus == ShiftState.Accepted)
                );

                keyHolderAcceptedList.Add(
                    submissions.Count(s =>
                        s.ShiftDayId == day.Id &&
                        s.ShiftType == ShiftType.Night &&
                        s.ShiftStatus == ShiftState.Accepted &&
                        s.UserShiftRole == UserShiftRole.KeyHolder)
                );
            }

            // ================================
            // ② 必要人数（1 + 2(n-1) + 1）
            // ================================
            var requiredList = new List<int>();

            for (int i = 0; i < workloads.Count; i++)
            {
                var w = workloads[i];
                var day = w.ShiftDay;

                bool isPrevPeriodDay =
                    day.RecruitmentPeriodId != selectedPeriod.Id;

                bool isLastDayOfCurrent =
                    day.RecruitmentPeriodId == selectedPeriod.Id &&
                    day.Date == shiftDays.Last().Date;

                if (isPrevPeriodDay || isLastDayOfCurrent)
                {
                    requiredList.Add(w.RequiredWorkers);
                }
                else
                {
                    requiredList.Add(w.RequiredWorkers);
                    requiredList.Add(w.RequiredWorkers);
                }
            }

            // ================================
            // ③ 残り人数
            // ================================
            var remainingWorkersList = new List<int>();

            for (int i = 0; i < requiredList.Count; i++)
            {
                int accepted = (i < totalAcceptedList.Count)
                    ? totalAcceptedList[i]
                    : 0;

                remainingWorkersList.Add(requiredList[i] - accepted);
            }

            return new ShiftTableResult
            {
                ShiftDays = shiftDays,
                Submissions = submissions,
                Workloads = workloads,
                TotalAcceptedList = totalAcceptedList,
                KeyHolderAcceptedList = keyHolderAcceptedList,
                RemainingWorkersList = remainingWorkersList
            };
        }
    }

    public class ShiftTableResult
    {
        public List<ShiftDay> ShiftDays { get; set; } = new();
        public List<DailyWorkload> Workloads { get; set; } = new();
        public List<ShiftSubmission> Submissions { get; set; } = new();
        public List<int> TotalAcceptedList { get; set; } = new();
        public List<int> KeyHolderAcceptedList { get; set; } = new();
        public List<int> RemainingWorkersList { get; set; } = new();
    }
}
