using System.Text.Json;
using Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using TaskEntity = Domain.Entities.Task.Task;

namespace Infrastructure;

public sealed class DBContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();

    public DBContext(DbContextOptions<DBContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Email)
                .IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();

            entity.Property(e => e.SleepProfile)
                .HasConversion(
                    value => JsonSerializer.Serialize(
                        value, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<SleepProfile>(
                        value, (JsonSerializerOptions?)null))
                .HasColumnType("jsonb");

            entity.Property(e => e.RhythmPreferences)
                .HasConversion(
                    value => JsonSerializer.Serialize(
                        value, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<RhythmPreferences>(
                        value, (JsonSerializerOptions?)null))
                .HasColumnType("jsonb");

            entity.Property(e => e.PlanningSettings)
                .HasConversion(
                    value => JsonSerializer.Serialize(
                        value, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<PlanningSettings>(
                        value, (JsonSerializerOptions?)null))
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<TaskEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
