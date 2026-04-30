using Microsoft.EntityFrameworkCore;
using sumile.Data;
using sumile.Models;
using sumile.ViewModels;

namespace sumile.Services
{
    public class ExchangePageService
    {
        private readonly ApplicationDbContext _context;

        public ExchangePageService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ExchangeCreatePageViewModel> BuildCreateAsync(string userId)
        {
            var userShifts = await _context.ShiftSubmissions
                .Include(s => s.ShiftDay)
                    .ThenInclude(d => d.RecruitmentPeriod)
                .Where(s => s.UserId == userId)
                .ToListAsync();

            return new ExchangeCreatePageViewModel
            {
                ShiftsByPeriod = userShifts
                    .GroupBy(s => s.ShiftDay.RecruitmentPeriod)
                    .ToDictionary(g => g.Key, g => g.ToList()),
                TargetUsers = await _context.Users
                    .Where(u => u.Id != userId)
                    .OrderBy(u => u.CustomId)
                    .ToListAsync()
            };
        }

        public async Task<ExchangeIndexPageViewModel?> BuildIndexAsync(string currentUserId, bool relatedOnly)
        {
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);
            if (currentUser == null)
            {
                return null;
            }

            var isAdmin = currentUser.IsAdmin;
            var query = _context.ShiftExchanges
                .Include(e => e.RequestedByUser)
                .Include(e => e.AcceptedByUser)
                .Include(e => e.TargetUser)
                .Include(e => e.OfferedShiftSubmission)
                    .ThenInclude(s => s.ShiftDay)
                .Include(e => e.AcceptedShiftSubmission)
                    .ThenInclude(s => s!.ShiftDay)
                .AsQueryable();

            if (relatedOnly)
            {
                query = query.Where(e =>
                    e.TargetUserId == currentUserId ||
                    e.RequestedByUserId == currentUserId ||
                    e.AcceptedByUserId == currentUserId);
            }
            else if (!isAdmin)
            {
                query = query.Where(e =>
                    e.TargetUserId == null ||
                    e.TargetUserId == currentUserId ||
                    e.RequestedByUserId == currentUserId ||
                    e.AcceptedByUserId == currentUserId);
            }

            return new ExchangeIndexPageViewModel
            {
                CurrentUserId = currentUserId,
                CurrentUserRole = currentUser.UserShiftRole.ToString(),
                IsAdmin = isAdmin,
                RelatedOnly = relatedOnly,
                Exchanges = await query
                    .OrderByDescending(e => e.UpdatedAt)
                    .ToListAsync()
            };
        }

        public async Task<bool> IsAdminUserAsync(string? userId)
        {
            if (string.IsNullOrEmpty(userId)) return false;
            return await _context.Users.AnyAsync(u => u.Id == userId && u.IsAdmin);
        }

        public async Task<object> GetShiftStatusInfoAsync(DateTime date, ShiftType shiftType, int periodId)
        {
            var shiftDay = await _context.ShiftDays
                .FirstOrDefaultAsync(d => d.Date == date && d.RecruitmentPeriodId == periodId);

            if (shiftDay == null)
            {
                return new { redCount = 0, blackCount = 0, total = 0 };
            }

            var submissions = await _context.ShiftSubmissions
                .Where(s => s.ShiftDayId == shiftDay.Id && s.ShiftType == shiftType)
                .ToListAsync();

            var redCount = submissions.Count(s => s.UserShiftRole == UserShiftRole.KeyHolder);
            var blackCount = submissions.Count(s => s.UserShiftRole != UserShiftRole.KeyHolder);
            var total = redCount + blackCount;

            return new { redCount, blackCount, total };
        }
    }
}
