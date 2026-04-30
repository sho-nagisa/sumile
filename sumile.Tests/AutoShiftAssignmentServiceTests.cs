using Microsoft.EntityFrameworkCore;
using sumile.Models;
using sumile.Services;
using Xunit;

namespace sumile.Tests;

public class AutoShiftAssignmentServiceTests
{
    [Fact]
    public async Task AssignAsync_KeepsHalfOfRequiredWorkersAsKeyHoldersAndBalancesBlankRate()
    {
        await using var context = TestDb.CreateContext();
        var service = new AutoShiftAssignmentService(context);
        var assignedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var users = new[]
        {
            new ApplicationUser { Id = "key-1", Name = "Key 1", UserShiftRole = UserShiftRole.KeyHolder },
            new ApplicationUser { Id = "key-2", Name = "Key 2", UserShiftRole = UserShiftRole.KeyHolder },
            new ApplicationUser { Id = "normal-1", Name = "Normal 1", UserShiftRole = UserShiftRole.Normal },
            new ApplicationUser { Id = "normal-2", Name = "Normal 2", UserShiftRole = UserShiftRole.Normal }
        };

        context.RecruitmentPeriods.Add(new RecruitmentPeriod
        {
            Id = 1,
            StartDate = new DateTime(2026, 5, 1),
            EndDate = new DateTime(2026, 5, 1)
        });
        context.ShiftDays.Add(new ShiftDay
        {
            Id = 101,
            Date = new DateTime(2026, 5, 1),
            RecruitmentPeriodId = 1
        });
        context.DailyWorkloads.Add(new global::DailyWorkload
        {
            ShiftDayId = 101,
            RequiredCount = 40,
            RequiredWorkers = 2
        });

        foreach (var user in users)
        {
            foreach (ShiftType shiftType in Enum.GetValues(typeof(ShiftType)))
            {
                context.ShiftSubmissions.Add(new ShiftSubmission
                {
                    UserId = user.Id,
                    ShiftDayId = 101,
                    ShiftType = shiftType,
                    ShiftStatus = ShiftState.Accepted,
                    IsSelected = true,
                    SubmittedAt = assignedAt.AddDays(-1),
                    UserType = UserType.Normal,
                    UserShiftRole = user.UserShiftRole
                });
            }
        }

        await context.SaveChangesAsync();

        var result = await service.AssignAsync(1, assignedAt);

        Assert.Empty(result.KeyHolderShortages);
        Assert.Empty(result.WorkerShortages);
        Assert.Equal(2, result.ShiftCellCount);
        Assert.Equal(4, result.RequiredWorkerTotal);
        Assert.Equal(4, result.AssignedCount);
        Assert.Equal(2, result.KeyHolderAssignedCount);
        Assert.Equal(0, result.WorkerShortageSlots);
        Assert.Equal(0, result.KeyHolderShortageSlots);

        var submissions = await context.ShiftSubmissions.ToListAsync();
        foreach (ShiftType shiftType in Enum.GetValues(typeof(ShiftType)))
        {
            var assigned = submissions
                .Where(s => s.ShiftDayId == 101 && s.ShiftType == shiftType && s.IsSelected)
                .ToList();

            Assert.Equal(2, assigned.Count);
            Assert.Single(assigned, s => s.ShiftStatus == ShiftState.KeyHolder);
            Assert.All(assigned, s => Assert.Equal(assignedAt, s.SubmittedAt));
        }

        var assignedCounts = submissions
            .Where(s => s.IsSelected)
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.All(users, user => Assert.True(assignedCounts.ContainsKey(user.Id)));
        Assert.Equal(1, assignedCounts.Values.Min());
        Assert.Equal(1, assignedCounts.Values.Max());
    }

    [Fact]
    public async Task AssignAsync_ReportsWorkerAndKeyHolderShortagesWhenCandidatesAreInsufficient()
    {
        await using var context = TestDb.CreateContext();
        var service = new AutoShiftAssignmentService(context);
        var assignedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

        context.RecruitmentPeriods.Add(new RecruitmentPeriod
        {
            Id = 1,
            StartDate = new DateTime(2026, 5, 1),
            EndDate = new DateTime(2026, 5, 1)
        });
        context.ShiftDays.Add(new ShiftDay
        {
            Id = 101,
            Date = new DateTime(2026, 5, 1),
            RecruitmentPeriodId = 1
        });
        context.DailyWorkloads.Add(new global::DailyWorkload
        {
            ShiftDayId = 101,
            RequiredCount = 40,
            RequiredWorkers = 2
        });

        foreach (ShiftType shiftType in Enum.GetValues(typeof(ShiftType)))
        {
            context.ShiftSubmissions.Add(new ShiftSubmission
            {
                UserId = "normal-1",
                ShiftDayId = 101,
                ShiftType = shiftType,
                ShiftStatus = ShiftState.Accepted,
                IsSelected = true,
                SubmittedAt = assignedAt.AddDays(-1),
                UserType = UserType.Normal,
                UserShiftRole = UserShiftRole.Normal
            });
        }

        await context.SaveChangesAsync();

        var result = await service.AssignAsync(1, assignedAt);

        Assert.Equal(2, result.ShiftCellCount);
        Assert.Equal(4, result.RequiredWorkerTotal);
        Assert.Equal(2, result.AssignedCount);
        Assert.Equal(0, result.KeyHolderAssignedCount);
        Assert.Equal(2, result.WorkerShortages.Count);
        Assert.Equal(2, result.KeyHolderShortages.Count);
        Assert.Equal(2, result.WorkerShortageSlots);
        Assert.Equal(2, result.KeyHolderShortageSlots);
        Assert.All(result.WorkerShortages, shortage =>
        {
            Assert.Equal(1, shortage.Actual);
            Assert.Equal(2, shortage.Required);
        });
        Assert.All(result.KeyHolderShortages, shortage =>
        {
            Assert.Equal(0, shortage.Actual);
            Assert.Equal(1, shortage.Required);
        });
    }
}
