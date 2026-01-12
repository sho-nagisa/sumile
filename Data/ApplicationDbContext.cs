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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ShiftDay>()
                .HasIndex(d => new { d.RecruitmentPeriodId, d.Id });

            // SubmitBackup: 募集期間 × 日付
            builder.Entity<SubmitBackup>()
                .HasIndex(b => new { b.RecruitmentPeriodId, b.ShiftDayId });
                }
    }
}
