using D3B.Storage.Accounts;
using D3B.Storage.Graphics;
using D3B.Storage.Harvests;
using D3B.Storage.Profiles;
using Microsoft.EntityFrameworkCore;

namespace D3B.Storage;

/// <summary>D3B's database: the accounts and characters it saw, the profiles, and what the bots learned of the maps' graphics (D100, D138).</summary>
public sealed class StorageContext : DbContext
{
    /// <summary>Gets the accounts.</summary>
    public DbSet<KnownAccount> Accounts => Set<KnownAccount>();

    /// <summary>Gets the characters.</summary>
    public DbSet<KnownCharacter> Characters => Set<KnownCharacter>();

    /// <summary>Gets the profiles.</summary>
    public DbSet<Profile> Profiles => Set<Profile>();

    /// <summary>Gets the graphics the bots learned.</summary>
    public DbSet<LearnedGraphic> Graphics => Set<LearnedGraphic>();

    /// <summary>Gets how long the resources take to grow back, by the skill that gathers each (D145).</summary>
    public DbSet<LearnedRegrowth> Regrowths => Set<LearnedRegrowth>();

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

        modelBuilder.Entity<LearnedGraphic>(graphic =>
        {
            graphic.ToTable("Graphics");
            graphic.HasKey(learned => learned.GfxId);
            graphic.Property(learned => learned.GfxId).ValueGeneratedNever();
        });

        modelBuilder.Entity<LearnedRegrowth>(regrowth =>
        {
            regrowth.ToTable("Regrowths");
            regrowth.HasKey(learned => learned.SkillId);
            regrowth.Property(learned => learned.SkillId).ValueGeneratedNever();
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
