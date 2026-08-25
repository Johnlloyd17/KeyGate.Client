using KeyGate.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KeyGate.Api.Data;

public class KeyGateDbContext : DbContext
{
    public KeyGateDbContext(DbContextOptions<KeyGateDbContext> options)
        : base(options)
    {
    }

    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Individual> Individuals => Set<Individual>();
    public DbSet<RegistrationToken> RegistrationTokens => Set<RegistrationToken>();
    public DbSet<AccessKey> AccessKeys => Set<AccessKey>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<LockScreenConfig> LockScreenConfigs => Set<LockScreenConfig>();
    public DbSet<ConfigChangeLog> ConfigChangeLogs => Set<ConfigChangeLog>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FullName).IsRequired();
            entity.Property(a => a.Email).IsRequired();
            entity.HasIndex(a => a.Email).IsUnique();
            entity.Property(a => a.PasswordHash).IsRequired();
            entity.Property(a => a.Role).IsRequired();
        });

        modelBuilder.Entity<Individual>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.FullName).IsRequired();
            entity.Property(i => i.EmailOrEmployeeId).IsRequired();
            entity.HasIndex(i => i.EmailOrEmployeeId).IsUnique();
            entity.Property(i => i.Status).HasConversion<int>();

            entity.HasOne(i => i.CreatedByAdmin)
                .WithMany()
                .HasForeignKey(i => i.CreatedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RegistrationToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Token).IsRequired();
            entity.HasIndex(t => t.Token).IsUnique();
            entity.Property(t => t.QrCodeUrl).IsRequired();

            entity.HasOne(t => t.Individual)
                .WithMany(i => i.RegistrationTokens)
                .HasForeignKey(t => t.IndividualId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessKey>(entity =>
        {
            entity.HasKey(k => k.Id);
            entity.Property(k => k.KeyHash).IsRequired();

            entity.HasOne(k => k.Individual)
                .WithMany(i => i.AccessKeys)
                .HasForeignKey(k => k.IndividualId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.DeviceName).IsRequired();
            entity.Property(d => d.DeviceFingerprint).IsRequired();
            entity.HasIndex(d => d.DeviceFingerprint).IsUnique();
            entity.Property(d => d.Status).HasConversion<int>();
        });

        modelBuilder.Entity<LockScreenConfig>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.HasOne(c => c.Device)
                .WithMany(d => d.LockScreenConfigs)
                .HasForeignKey(c => c.DeviceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConfigChangeLog>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.FieldChanged).IsRequired();
            entity.Property(l => l.ChangedBy).HasMaxLength(200);

            entity.HasOne(l => l.Device)
                .WithMany(d => d.ConfigChangeLogs)
                .HasForeignKey(l => l.DeviceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.EndReason).HasConversion<int>();

            entity.HasIndex(s => new { s.DeviceId, s.EndedAt });
            entity.HasIndex(s => s.StartedAt);

            entity.HasOne(s => s.Individual)
                .WithMany(i => i.Sessions)
                .HasForeignKey(s => s.IndividualId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Device)
                .WithMany(d => d.Sessions)
                .HasForeignKey(s => s.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.Key).IsUnique();
            entity.Property(s => s.Key).IsRequired().HasMaxLength(200);
            entity.Property(s => s.Value).IsRequired();
        });

        modelBuilder.Entity<SystemSetting>().HasData(new SystemSetting
        {
            Id = 1,
            Key = "ConfigVersion",
            Value = "1",
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
