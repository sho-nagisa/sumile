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
            var currentWorkloads = await _context.DailyWorkloads
                .Where(w => shiftDays.Select(sd => sd.Id).Contains(w.ShiftDayId))
                .Include(w => w.ShiftDay)
                .ToListAsync();

            // ===== 前期間最終日 workload を先頭に追加 =====
            var prevPeriod = allPeriods
                .Where(p => p.EndDate.Date < selectedPeriod.StartDate.Date)
                .OrderByDescending(p => p.EndDate)
                .ThenByDescending(p => p.Id)
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
                        currentWorkloads.Insert(0, prevWorkload);
                    }
                }
            }

            // ===== Submissions =====
            var shiftDayIds = shiftDays.Select(d => d.Id).ToList();
            var rawSubmissions = await _context.ShiftSubmissions
                .Where(s => shiftDayIds.Contains(s.ShiftDayId))
                .ToListAsync();
            var submissions = NormalizeSubmissions(rawSubmissions);

            // ================================
            // ① Accepted 数（2n 列）
            // ================================
            var shiftColumns = BuildShiftColumns(shiftDays);
            var totalAcceptedList = shiftColumns
                .Select(column => submissions.Count(s =>
                    s.ShiftDayId == column.ShiftDayId &&
                    s.ShiftType == column.ShiftType &&
                    IsAssignedShift(s)))
                .ToList();

            var keyHolderAcceptedList = shiftColumns
                .Select(column => submissions.Count(s =>
                    s.ShiftDayId == column.ShiftDayId &&
                    s.ShiftType == column.ShiftType &&
                    IsKeyHolderShift(s)))
                .ToList();

            // ================================
            // ② 必要人数（1 + 2(n-1) + 1）
            // ================================
            var workloadCells = BuildWorkloadCells(selectedPeriod.Id, shiftDays, currentWorkloads);
            var requiredWorkersList = new List<int>();

            foreach (var cell in workloadCells)
            {
                requiredWorkersList.AddRange(Enumerable.Repeat(cell.RequiredWorkers, cell.Colspan));
            }

            // ================================
            // ③ 残り人数
            // ================================
            var remainingWorkersList = new List<int>();

            for (int i = 0; i < requiredWorkersList.Count; i++)
            {
                int accepted = (i < totalAcceptedList.Count)
                    ? totalAcceptedList[i]
                    : 0;

                remainingWorkersList.Add(accepted - requiredWorkersList[i]);
            }

            return new ShiftTableResult
            {
                ShiftDays = shiftDays,
                Submissions = submissions,
                Workloads = currentWorkloads,
                WorkloadCells = workloadCells,
                ShiftColumns = shiftColumns,
                TotalAcceptedList = totalAcceptedList,
                KeyHolderAcceptedList = keyHolderAcceptedList,
                RequiredWorkersList = requiredWorkersList,
                RemainingWorkersList = remainingWorkersList
            };
        }

        private static List<ShiftTableWorkloadCell> BuildWorkloadCells(
            int selectedPeriodId,
            List<ShiftDay> shiftDays,
            List<DailyWorkload> workloads)
        {
            var cells = new List<ShiftTableWorkloadCell>();
            if (!shiftDays.Any()) return cells;

            var workloadByDayId = workloads
                .Where(w => w.ShiftDay.RecruitmentPeriodId == selectedPeriodId)
                .ToDictionary(w => w.ShiftDayId);

            var previousWorkload = workloads
                .Where(w => w.ShiftDay.RecruitmentPeriodId != selectedPeriodId)
                .OrderByDescending(w => w.ShiftDay.Date)
                .FirstOrDefault();

            cells.Add(CreateWorkloadCell(previousWorkload, 1));

            for (var i = 0; i < shiftDays.Count - 1; i++)
            {
                workloadByDayId.TryGetValue(shiftDays[i].Id, out var workload);
                cells.Add(CreateWorkloadCell(workload, 2));
            }

            workloadByDayId.TryGetValue(shiftDays.Last().Id, out var lastWorkload);
            cells.Add(CreateWorkloadCell(lastWorkload, 1));

            return cells;
        }

        private static List<ShiftTableColumn> BuildShiftColumns(List<ShiftDay> shiftDays)
        {
            return shiftDays
                .SelectMany(day => new[]
                {
                    new ShiftTableColumn
                    {
                        Date = day.Date,
                        ShiftDayId = day.Id,
                        ShiftType = ShiftType.Morning
                    },
                    new ShiftTableColumn
                    {
                        Date = day.Date,
                        ShiftDayId = day.Id,
                        ShiftType = ShiftType.Night
                    }
                })
                .ToList();
        }

        private static ShiftTableWorkloadCell CreateWorkloadCell(DailyWorkload? workload, int colspan)
        {
            var requiredCount = workload?.RequiredCount ?? 0;
            var requiredWorkers = workload == null
                ? 0
                : workload.RequiredWorkers > 0
                    ? workload.RequiredWorkers
                    : DailyWorkload.CalculateRequiredWorkers(workload.RequiredCount);

            return new ShiftTableWorkloadCell
            {
                RequiredCount = requiredCount,
                RequiredWorkers = requiredWorkers,
                Colspan = colspan
            };
        }

        private static List<ShiftSubmission> NormalizeSubmissions(List<ShiftSubmission> submissions)
        {
            return submissions
                .GroupBy(s => new
                {
                    s.UserId,
                    s.ShiftDayId,
                    s.ShiftType
                })
                .Select(g => g
                    .OrderByDescending(s => s.SubmittedAt ?? DateTime.MinValue)
                    .ThenByDescending(s => s.Id)
                    .First())
                .ToList();
        }

        private static bool IsAssignedShift(ShiftSubmission submission)
        {
            return submission.ShiftStatus == ShiftState.Accepted ||
                   submission.ShiftStatus == ShiftState.KeyHolder;
        }

        private static bool IsKeyHolderShift(ShiftSubmission submission)
        {
            return IsAssignedShift(submission) &&
                   (submission.UserShiftRole == UserShiftRole.KeyHolder ||
                    submission.ShiftStatus == ShiftState.KeyHolder);
        }
    }

    public class ShiftTableWorkloadCell
    {
        public int RequiredCount { get; set; }
        public int RequiredWorkers { get; set; }
        public int Colspan { get; set; }
    }

    public class ShiftTableColumn
    {
        public DateTime Date { get; set; }
        public int ShiftDayId { get; set; }
        public ShiftType ShiftType { get; set; }
    }

    public class ShiftTableResult
    {
        public List<ShiftDay> ShiftDays { get; set; } = new();
        public List<DailyWorkload> Workloads { get; set; } = new();
        public List<ShiftTableWorkloadCell> WorkloadCells { get; set; } = new();
        public List<ShiftTableColumn> ShiftColumns { get; set; } = new();
        public List<ShiftSubmission> Submissions { get; set; } = new();
        public List<int> TotalAcceptedList { get; set; } = new();
        public List<int> KeyHolderAcceptedList { get; set; } = new();
        public List<int> RequiredWorkersList { get; set; } = new();
        public List<int> RemainingWorkersList { get; set; } = new();
    }
}
