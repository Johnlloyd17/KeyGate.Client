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
    public DbSet<Session> Sessions => Set<Session>();

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

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.EndReason).HasConversion<int>();

            entity.HasOne(s => s.Individual)
                .WithMany(i => i.Sessions)
                .HasForeignKey(s => s.IndividualId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Device)
                .WithMany(d => d.Sessions)
                .HasForeignKey(s => s.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
