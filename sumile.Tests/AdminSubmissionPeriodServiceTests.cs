using Microsoft.EntityFrameworkCore;
using sumile.Models;
using sumile.Services;
using Xunit;

namespace sumile.Tests;

public class AdminSubmissionPeriodServiceTests
{
    [Fact]
    public async Task ToggleSubmissionStatusAsync_WhenClosingPeriod_BacksUpSubmissionsOnlyOnce()
    {
        await using var context = TestDb.CreateContext();
        var service = new AdminSubmissionPeriodService(context);
        var firstClosedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var secondClosedAt = new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc);

        context.RecruitmentPeriods.Add(new RecruitmentPeriod
        {
            Id = 1,
            StartDate = new DateTime(2026, 5, 1),
            EndDate = new DateTime(2026, 5, 1),
            IsOpen = true
        });
        context.ShiftDays.Add(new ShiftDay
        {
            Id = 101,
            Date = new DateTime(2026, 5, 1),
            RecruitmentPeriodId = 1
        });
        context.ShiftSubmissions.Add(new ShiftSubmission
        {
            UserId = "user-1",
            ShiftDayId = 101,
            ShiftType = ShiftType.Morning,
            ShiftStatus = ShiftState.Accepted,
            IsSelected = true,
            SubmittedAt = firstClosedAt.AddHours(-1),
            UserType = UserType.Normal,
            UserShiftRole = UserShiftRole.Normal
        });
        await context.SaveChangesAsync();

        var closed = await service.ToggleSubmissionStatusAsync(1, firstClosedAt);
        var reopened = await service.ToggleSubmissionStatusAsync(1, firstClosedAt.AddHours(1));
        var closedAgain = await service.ToggleSubmissionStatusAsync(1, secondClosedAt);

        Assert.True(closed);
        Assert.True(reopened);
        Assert.True(closedAgain);

        var period = await context.RecruitmentPeriods.SingleAsync(p => p.Id == 1);
        Assert.False(period.IsOpen);

        var backup = Assert.Single(await context.SubmitBackups.ToListAsync());
        Assert.Equal(1, backup.RecruitmentPeriodId);
        Assert.Equal("user-1", backup.UserId);
        Assert.Equal(101, backup.ShiftDayId);
        Assert.Equal(ShiftType.Morning, backup.ShiftType);
        Assert.Equal(ShiftState.Accepted, backup.ShiftStatus);
        Assert.Equal(firstClosedAt, backup.BackedUpAt);
    }
}
