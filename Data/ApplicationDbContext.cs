using Microsoft.EntityFrameworkCore;

namespace sumile.Data  // ← `sumile.Data` に修正
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
    }
}
