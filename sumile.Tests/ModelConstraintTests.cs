using Microsoft.EntityFrameworkCore;
using sumile.Models;
using Xunit;

namespace sumile.Tests;

public class ModelConstraintTests
{
    [Fact]
    public void ApplicationUser_HasUniqueIndexForPositiveCustomId()
    {
        using var context = TestDb.CreateContext();

        var index = context.Model
            .FindEntityType(typeof(ApplicationUser))!
            .GetIndexes()
            .SingleOrDefault(i => i.Properties
                .Select(p => p.Name)
                .SequenceEqual(new[] { nameof(ApplicationUser.CustomId) }));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
        Assert.Equal("\"CustomId\" > 0", index.GetFilter());
    }

    [Fact]
    public void ShiftDay_HasUniqueIndexForPeriodAndDate()
    {
        using var context = TestDb.CreateContext();

        var index = context.Model
            .FindEntityType(typeof(ShiftDay))!
            .GetIndexes()
            .SingleOrDefault(i => i.Properties
                .Select(p => p.Name)
                .SequenceEqual(new[] { nameof(ShiftDay.RecruitmentPeriodId), nameof(ShiftDay.Date) }));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public void ShiftSubmission_HasUniqueIndexForUserShiftDayAndShiftType()
    {
        using var context = TestDb.CreateContext();

        var index = context.Model
            .FindEntityType(typeof(ShiftSubmission))!
            .GetIndexes()
            .SingleOrDefault(i => i.Properties
                .Select(p => p.Name)
                .SequenceEqual(new[]
                {
                    nameof(ShiftSubmission.UserId),
                    nameof(ShiftSubmission.ShiftDayId),
                    nameof(ShiftSubmission.ShiftType)
                }));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }
}
