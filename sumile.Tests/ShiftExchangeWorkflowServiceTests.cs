using Microsoft.EntityFrameworkCore;
using sumile.Models;
using sumile.Services;
using Xunit;

namespace sumile.Tests;

public class ShiftExchangeWorkflowServiceTests
{
    [Fact]
    public async Task CancelRequestAsync_CancelsOwnOpenRequest()
    {
        await using var context = TestDb.CreateContext();
        var service = new ShiftExchangeWorkflowService(context);
        var updatedAt = new DateTime(2026, 5, 1, 11, 0, 0, DateTimeKind.Utc);
        context.ShiftExchanges.Add(new ShiftExchange
        {
            Id = 1,
            RequestedByUserId = "owner",
            OfferedShiftSubmissionId = 10,
            Status = ShiftExchange.StatusOpen,
            CreatedAt = updatedAt.AddDays(-1),
            UpdatedAt = updatedAt.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var result = await service.CancelRequestAsync(1, "owner", updatedAt);

        Assert.True(result.Success);
        var exchange = await context.ShiftExchanges.SingleAsync(e => e.Id == 1);
        Assert.Equal(ShiftExchange.StatusCanceled, exchange.Status);
        Assert.Equal(updatedAt, exchange.UpdatedAt);
    }

    [Fact]
    public async Task CancelRequestAsync_RejectsDifferentUser()
    {
        await using var context = TestDb.CreateContext();
        var service = new ShiftExchangeWorkflowService(context);
        context.ShiftExchanges.Add(new ShiftExchange
        {
            Id = 1,
            RequestedByUserId = "owner",
            OfferedShiftSubmissionId = 10,
            Status = ShiftExchange.StatusOpen,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await service.CancelRequestAsync(1, "other", DateTime.UtcNow);

        Assert.False(result.Success);
        Assert.True(result.Forbidden);
        var exchange = await context.ShiftExchanges.SingleAsync(e => e.Id == 1);
        Assert.Equal(ShiftExchange.StatusOpen, exchange.Status);
    }

    [Fact]
    public async Task CancelApplicationAsync_ReopensRequestAndClearsAcceptedUser()
    {
        await using var context = TestDb.CreateContext();
        var service = new ShiftExchangeWorkflowService(context);
        var updatedAt = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        context.ShiftExchanges.Add(new ShiftExchange
        {
            Id = 1,
            RequestedByUserId = "owner",
            AcceptedByUserId = "applicant",
            OfferedShiftSubmissionId = 10,
            AcceptedShiftSubmissionId = 20,
            AcceptedAt = updatedAt.AddHours(-1),
            Status = ShiftExchange.StatusPendingApproval,
            CreatedAt = updatedAt.AddDays(-1),
            UpdatedAt = updatedAt.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var result = await service.CancelApplicationAsync(1, "applicant", updatedAt);

        Assert.True(result.Success);
        var exchange = await context.ShiftExchanges.SingleAsync(e => e.Id == 1);
        Assert.Equal(ShiftExchange.StatusOpen, exchange.Status);
        Assert.Null(exchange.AcceptedByUserId);
        Assert.Null(exchange.AcceptedAt);
        Assert.Null(exchange.AcceptedShiftSubmissionId);
        Assert.Equal(updatedAt, exchange.UpdatedAt);
    }

    [Fact]
    public async Task RejectExchangeAsync_RejectsPendingExchange()
    {
        await using var context = TestDb.CreateContext();
        var service = new ShiftExchangeWorkflowService(context);
        var updatedAt = new DateTime(2026, 5, 1, 13, 0, 0, DateTimeKind.Utc);
        context.ShiftExchanges.Add(new ShiftExchange
        {
            Id = 1,
            RequestedByUserId = "owner",
            AcceptedByUserId = "applicant",
            OfferedShiftSubmissionId = 10,
            AcceptedShiftSubmissionId = 20,
            Status = ShiftExchange.StatusPendingApproval,
            CreatedAt = updatedAt.AddDays(-1),
            UpdatedAt = updatedAt.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var result = await service.RejectExchangeAsync(1, updatedAt);

        Assert.True(result.Success);
        var exchange = await context.ShiftExchanges.SingleAsync(e => e.Id == 1);
        Assert.Equal(ShiftExchange.StatusRejected, exchange.Status);
        Assert.Equal(updatedAt, exchange.UpdatedAt);
    }
}
