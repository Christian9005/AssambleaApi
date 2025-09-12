using Microsoft.EntityFrameworkCore;

namespace AssambleaApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    public DbSet<Models.Meeting> Meetings { get; set; } = null!;
    public DbSet<Models.Attendee> Attendees { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.Meeting>()
            .HasMany(m => m.Attendees)
            .WithOne(a => a.Meeting)
            .HasForeignKey(a => a.MeetingId);
        base.OnModelCreating(modelBuilder);
    }
}
