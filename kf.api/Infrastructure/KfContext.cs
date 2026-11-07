using KnightFight.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnightFight.Api.Infrastructure;

public class KfContext(DbContextOptions<KfContext> options) : DbContext(options)
{
    public DbSet<ChallengerModel> Challengers => Set<ChallengerModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<ChallengerModel>()
            .ToTable("Challengers", table => table.HasCheckConstraint(
                "CK_Challengers_Avatar_SingleSource",
                "(\"AvatarBlob\" IS NOT NULL AND \"DefaultAvatarIndex\" IS NULL) OR (\"AvatarBlob\" IS NULL AND \"DefaultAvatarIndex\" IS NOT NULL)"));

        // Apply config to every entity inheriting BaseEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseModel).IsAssignableFrom(entityType.ClrType)) continue;
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(BaseModel.CreatedAt))
                .IsRequired();

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(BaseModel.UpdatedAt))
                .IsRequired();
        }
        
    }
    
    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseModel>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    // Prevent accidental overwrite
                    entry.Property(nameof(BaseModel.CreatedAt)).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}