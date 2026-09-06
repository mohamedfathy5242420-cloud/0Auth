namespace OAuthDemo.Api.Data;

using Microsoft.EntityFrameworkCore;
using OAuthDemo.Api.Entities;

/// <summary>
/// DbContext for managing application users in SQL Server.
/// Handles creation and updating of user records during Google OAuth authentication.
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
    /// </summary>
    /// <param name="options">The DbContext options configured by the dependency injection container.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the Users DbSet for database operations.
    /// </summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary>
    /// Configures the database model and mappings.
    /// </summary>
    /// <param name="modelBuilder">The builder that configures the model for the database context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity mappings
        modelBuilder.Entity<User>(entity =>
        {
            // Set primary key
            entity.HasKey(e => e.Id);

            // Configure properties
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Name)
                .HasMaxLength(100);

            entity.Property(e => e.GoogleId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.ProfilePictureUrl)
                .HasMaxLength(500);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.LastLoginAt)
                .IsRequired();

            // Ensure email is unique - a user can only have one account
            entity.HasIndex(e => e.Email)
                .IsUnique();

            // Index for GoogleId lookups
            entity.HasIndex(e => e.GoogleId)
                .IsUnique();
        });
    }
}