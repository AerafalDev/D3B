using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.EntityFrameworkCore;

namespace D3B.Storage;

/// <summary>D3B's database: the accounts and characters it saw, and the profiles (D100).</summary>
public sealed class StorageContext : DbContext
{
    /// <summary>Gets the accounts.</summary>
    public DbSet<KnownAccount> Accounts => Set<KnownAccount>();

    /// <summary>Gets the characters.</summary>
    public DbSet<KnownCharacter> Characters => Set<KnownCharacter>();

    /// <summary>Gets the profiles.</summary>
    public DbSet<Profile> Profiles => Set<Profile>();

    /// <summary>Initializes a new instance of the <see cref="StorageContext"/> class.</summary>
    /// <param name="options">The context's options: SQLite, on the database's file.</param>
    public StorageContext(DbContextOptions<StorageContext> options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<KnownAccount>(account =>
        {
            account.ToTable("Accounts");
            account.HasKey(known => known.Id);
            account.Property(known => known.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Profile>(profile =>
        {
            profile.ToTable("Profiles");
            profile.HasKey(known => known.Id);
            profile.Property(known => known.Name).HasMaxLength(Profile.NameLength).UseCollation("NOCASE");
            profile.HasIndex(known => known.Name).IsUnique();
        });

        modelBuilder.Entity<KnownCharacter>(character =>
        {
            character.ToTable("Characters");
            character.HasKey(known => new { known.AccountId, known.Id });
            character.Ignore(known => known.Key);

            // Forgetting an account forgets its characters; deleting a profile leaves its characters without one.
            character.HasOne<KnownAccount>().WithMany().HasForeignKey(known => known.AccountId).OnDelete(DeleteBehavior.Cascade);
            character.HasOne<Profile>().WithMany().HasForeignKey(known => known.ProfileId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
