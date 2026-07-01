using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using sumile.Models;

namespace sumile.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Shift> Shifts { get; set; }
        public DbSet<ShiftSubmission> ShiftSubmissions { get; set; }
        public DbSet<ShiftAssignment> ShiftAssignments { get; set; }
        public DbSet<ShiftExchange> ShiftExchanges { get; set; }
        public DbSet<RecruitmentPeriod> RecruitmentPeriods { get; set; }
        public DbSet<ShiftEditLog> ShiftEditLogs { get; set; }
        public DbSet<DailyWorkload> DailyWorkloads { get; set; }
        public DbSet<ShiftDay> ShiftDays { get; set; }
        public DbSet<SubmitBackup> SubmitBackups { get; set; }
        public DbSet<ShiftImportHistory> ShiftImportHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ShiftDay>()
                .HasIndex(d => new { d.RecruitmentPeriodId, d.Date })
                .IsUnique();

            builder.Entity<ShiftSubmission>()
                .HasIndex(s => new { s.UserId, s.ShiftDayId, s.ShiftType })
                .IsUnique();

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.CustomId)
                .IsUnique()
                .HasFilter("\"CustomId\" > 0");

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.ShiftImportApiKey)
                .IsUnique()
                .HasFilter("\"ShiftImportApiKey\" IS NOT NULL AND \"ShiftImportApiKey\" <> ''");

            builder.Entity<ShiftImportHistory>()
                .HasIndex(history => new { history.UserId, history.RangeStartDate, history.RangeEndDate })
                .IsUnique();

            builder.Entity<ShiftImportHistory>()
                .HasOne(history => history.User)
                .WithMany()
                .HasForeignKey(history => history.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // SubmitBackup: 募集期間 × 日付
            builder.Entity<SubmitBackup>()
                .HasIndex(b => new { b.RecruitmentPeriodId, b.ShiftDayId });
        }
    }
}
