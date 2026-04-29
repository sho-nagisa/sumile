using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;

namespace sumile.Services
{
    public class ShiftExchangeWorkflowService
    {
        private readonly ApplicationDbContext _context;

        public ShiftExchangeWorkflowService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ShiftExchangeWorkflowResult> CancelRequestAsync(
            int exchangeId,
            string userId,
            DateTime updatedAt)
        {
            var exchange = await _context.ShiftExchanges.FirstOrDefaultAsync(e => e.Id == exchangeId);
            if (exchange == null)
            {
                return ShiftExchangeWorkflowResult.NotFoundResult("交換募集が見つかりません。");
            }

            if (exchange.RequestedByUserId != userId)
            {
                return ShiftExchangeWorkflowResult.ForbiddenResult("この交換募集は取り消せません。");
            }

            if (!IsCancelableByRequesterStatus(exchange.Status))
            {
                return ShiftExchangeWorkflowResult.InvalidResult("この交換募集は取り消せません。");
            }

            exchange.Status = ShiftExchange.StatusCanceled;
            exchange.UpdatedAt = updatedAt;
            _context.ShiftExchanges.Update(exchange);
            await _context.SaveChangesAsync();

            return ShiftExchangeWorkflowResult.SuccessResult("交換募集を取り消しました。");
        }

        public async Task<ShiftExchangeWorkflowResult> CancelApplicationAsync(
            int exchangeId,
            string userId,
            DateTime updatedAt)
        {
            var exchange = await _context.ShiftExchanges.FirstOrDefaultAsync(e => e.Id == exchangeId);
            if (exchange == null)
            {
                return ShiftExchangeWorkflowResult.NotFoundResult("交換募集が見つかりません。");
            }

            if (!IsPendingApprovalStatus(exchange.Status) || exchange.AcceptedByUserId != userId)
            {
                return ShiftExchangeWorkflowResult.InvalidResult("この応募は取り消せません。");
            }

            exchange.AcceptedByUserId = null;
            exchange.AcceptedAt = null;
            exchange.AcceptedShiftSubmissionId = null;
            exchange.Status = ShiftExchange.StatusOpen;
            exchange.UpdatedAt = updatedAt;
            _context.ShiftExchanges.Update(exchange);
            await _context.SaveChangesAsync();

            return ShiftExchangeWorkflowResult.SuccessResult("応募を取り消しました。");
        }

        public async Task<ShiftExchangeWorkflowResult> RejectExchangeAsync(
            int exchangeId,
            DateTime updatedAt)
        {
            var exchange = await _context.ShiftExchanges.FirstOrDefaultAsync(e => e.Id == exchangeId);
            if (exchange == null || !IsPendingApprovalStatus(exchange.Status))
            {
                return ShiftExchangeWorkflowResult.NotFoundResult("承認待ちの交換が見つかりません。");
            }

            exchange.Status = ShiftExchange.StatusRejected;
            exchange.UpdatedAt = updatedAt;
            _context.ShiftExchanges.Update(exchange);
            await _context.SaveChangesAsync();

            return ShiftExchangeWorkflowResult.SuccessResult("交換を却下しました。");
        }

        private static bool IsOpenStatus(string status)
        {
            return status == ShiftExchange.StatusOpen;
        }

        private static bool IsPendingApprovalStatus(string status)
        {
            return status == ShiftExchange.StatusPendingApproval ||
                   status == ShiftExchange.StatusAcceptedLegacy;
        }

        private static bool IsCancelableByRequesterStatus(string status)
        {
            return IsOpenStatus(status) || IsPendingApprovalStatus(status);
        }
    }

    public class ShiftExchangeWorkflowResult
    {
        private ShiftExchangeWorkflowResult(bool success, bool notFound, bool forbidden, string message)
        {
            Success = success;
            NotFound = notFound;
            Forbidden = forbidden;
            Message = message;
        }

        public bool Success { get; }
        public bool NotFound { get; }
        public bool Forbidden { get; }
        public string Message { get; }

        public static ShiftExchangeWorkflowResult SuccessResult(string message)
        {
            return new ShiftExchangeWorkflowResult(true, false, false, message);
        }

        public static ShiftExchangeWorkflowResult NotFoundResult(string message)
        {
            return new ShiftExchangeWorkflowResult(false, true, false, message);
        }

        public static ShiftExchangeWorkflowResult ForbiddenResult(string message)
        {
            return new ShiftExchangeWorkflowResult(false, false, true, message);
        }

        public static ShiftExchangeWorkflowResult InvalidResult(string message)
        {
            return new ShiftExchangeWorkflowResult(false, false, false, message);
        }
    }
}
