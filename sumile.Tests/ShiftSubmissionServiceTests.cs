using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using sumile.Models;
using sumile.Services;
using Xunit;

namespace sumile.Tests;

public class ShiftSubmissionServiceTests
{
    [Fact]
    public async Task SubmitShiftsAsync_ReplacesExistingSubmissionsAndCreatesEveryShiftCell()
    {
        await using var context = TestDb.CreateContext();
        var service = new ShiftSubmissionService(context);
        var submittedAt = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
        var user = new ApplicationUser
        {
            Id = "user-1",
            Name = "Test User",
            UserShiftRole = UserShiftRole.Normal
        };

        context.RecruitmentPeriods.Add(new RecruitmentPeriod
        {
            Id = 1,
            StartDate = new DateTime(2026, 5, 1),
            EndDate = new DateTime(2026, 5, 2)
        });
        context.ShiftDays.AddRange(
            new ShiftDay { Id = 101, Date = new DateTime(2026, 5, 1), RecruitmentPeriodId = 1 },
            new ShiftDay { Id = 102, Date = new DateTime(2026, 5, 2), RecruitmentPeriodId = 1 });
        context.ShiftSubmissions.Add(new ShiftSubmission
        {
            UserId = user.Id,
            ShiftDayId = 101,
            ShiftType = ShiftType.Morning,
            ShiftStatus = ShiftState.NotAccepted,
            IsSelected = false,
            SubmittedAt = submittedAt.AddDays(-1),
            UserType = UserType.AdminUpdated,
            UserShiftRole = UserShiftRole.Normal
        });
        await context.SaveChangesAsync();

        var selectedShifts = JsonConvert.SerializeObject(new[]
        {
            new ShiftSubmissionViewModel
            {
                Date = "2026-05-01",
                ShiftType = ShiftType.Morning,
                ShiftSymbol = "〇"
            },
            new ShiftSubmissionViewModel
            {
                Date = "2026-05-02",
                ShiftType = ShiftType.Night,
                ShiftSymbol = "△"
            }
        });

        await service.SubmitShiftsAsync(user, selectedShifts, 1, UserType.Normal, submittedAt);

        var submissions = await context.ShiftSubmissions
            .OrderBy(s => s.ShiftDayId)
            .ThenBy(s => s.ShiftType)
            .ToListAsync();

        Assert.Equal(4, submissions.Count);
        Assert.All(submissions, submission =>
        {
            Assert.Equal(user.Id, submission.UserId);
            Assert.Equal(submittedAt, submission.SubmittedAt);
            Assert.Equal(UserType.Normal, submission.UserType);
            Assert.Equal(UserShiftRole.Normal, submission.UserShiftRole);
        });

        var firstMorning = Assert.Single(submissions, s => s.ShiftDayId == 101 && s.ShiftType == ShiftType.Morning);
        Assert.Equal(ShiftState.Accepted, firstMorning.ShiftStatus);
        Assert.True(firstMorning.IsSelected);

        var secondNight = Assert.Single(submissions, s => s.ShiftDayId == 102 && s.ShiftType == ShiftType.Night);
        Assert.Equal(ShiftState.WantToGiveAway, secondNight.ShiftStatus);
        Assert.True(secondNight.IsSelected);

        Assert.All(
            submissions.Where(s => s != firstMorning && s != secondNight),
            submission =>
            {
                Assert.Equal(ShiftState.None, submission.ShiftStatus);
                Assert.False(submission.IsSelected);
            });
    }

    [Fact]
    public async Task SubmitShiftsAsync_EmptySubmissionCreatesNoneCellsAndCountsAsSubmitted()
    {
        await using var context = TestDb.CreateContext();
        var service = new ShiftSubmissionService(context);
        var pageService = new ShiftPageService(context, new ShiftTableService(context));
        var submittedAt = new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
        var user = new ApplicationUser
        {
            Id = "user-empty",
            Name = "Empty User",
            UserShiftRole = UserShiftRole.Normal
        };

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
        await context.SaveChangesAsync();

        await service.SubmitShiftsAsync(user, "", 1, UserType.Normal, submittedAt);

        var submissions = await context.ShiftSubmissions
            .OrderBy(s => s.ShiftType)
            .ToListAsync();
        Assert.Equal(2, submissions.Count);
        Assert.All(submissions, submission =>
        {
            Assert.Equal(user.Id, submission.UserId);
            Assert.Equal(ShiftState.None, submission.ShiftStatus);
            Assert.False(submission.IsSelected);
            Assert.Equal(submittedAt, submission.SubmittedAt);
        });

        var page = await pageService.BuildSubmissionAsync(user, 1);
        Assert.True(page.HasSubmitted);
        Assert.Equal(2, page.ExistingSubmissions.Count);
    }
}
