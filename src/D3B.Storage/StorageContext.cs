using D3B.Storage.Accounts;
using D3B.Storage.Fights;
using D3B.Storage.Graphics;
using D3B.Storage.Harvests;
using D3B.Storage.Npcs;
using D3B.Storage.Runs;
using D3B.Storage.Variables;
using Microsoft.EntityFrameworkCore;

namespace D3B.Storage;

/// <summary>D3B's database: the accounts and characters it saw, what the bots learned of the maps' graphics, the NPCs and the fights' placement cells, the runs, and the variables the characters keep (D100, D138, D275, D298).</summary>
/// <remarks>The profiles left it for the project's files at session 7b (D296, D304).</remarks>
public sealed class StorageContext : DbContext
{
    /// <summary>Gets the accounts.</summary>
    public DbSet<KnownAccount> Accounts => Set<KnownAccount>();

    /// <summary>Gets the characters.</summary>
    public DbSet<KnownCharacter> Characters => Set<KnownCharacter>();

    /// <summary>Gets the graphics the bots learned.</summary>
    public DbSet<LearnedGraphic> Graphics => Set<LearnedGraphic>();

    /// <summary>Gets how long the resources take to grow back, by the skill that gathers each (D145).</summary>
    public DbSet<LearnedRegrowth> Regrowths => Set<LearnedRegrowth>();

    /// <summary>Gets the fights' placement cells, the map fought on and the one left, as the bots saw fights begin (D275).</summary>
    public DbSet<LearnedPlacement> Placements => Set<LearnedPlacement>();

    /// <summary>Gets the NPCs the bots saw on the maps (D383).</summary>
    public DbSet<LearnedMapNpc> MapNpcs => Set<LearnedMapNpc>();

    /// <summary>Gets the questions the NPCs asked, with the replies they offered (D383).</summary>
    public DbSet<LearnedNpcQuestion> NpcQuestions => Set<LearnedNpcQuestion>();

    /// <summary>Gets what followed the replies given to the NPCs (D383).</summary>
    public DbSet<LearnedNpcOutcome> NpcOutcomes => Set<LearnedNpcOutcome>();

    /// <summary>Gets the runs of the activities, for the statistics (D146).</summary>
    public DbSet<RunRecord> Runs => Set<RunRecord>();

    /// <summary>Gets the variables the characters keep (D298).</summary>
    public DbSet<KeptVariable> Variables => Set<KeptVariable>();

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

        modelBuilder.Entity<LearnedPlacement>(placement =>
        {
            placement.ToTable("Placements");
            placement.HasKey(learned => new { learned.FromMapId, learned.MapId, learned.RedCells, learned.BlueCells });
        });

        modelBuilder.Entity<LearnedMapNpc>(npc =>
        {
            npc.ToTable("MapNpcs");
            npc.HasKey(learned => new { learned.MapId, learned.NpcId });
        });

        modelBuilder.Entity<LearnedNpcQuestion>(question =>
        {
            question.ToTable("NpcQuestions");
            question.HasKey(learned => new { learned.MapId, learned.NpcId, learned.MessageId, learned.Replies });
        });

        modelBuilder.Entity<LearnedNpcOutcome>(outcome =>
        {
            outcome.ToTable("NpcOutcomes");
            outcome.HasKey(learned => new { learned.MapId, learned.NpcId, learned.MessageId, learned.ReplyId, learned.Kind, learned.Target });
        });

        modelBuilder.Entity<RunRecord>(run =>
        {
            run.ToTable("Runs");
            run.HasKey(record => record.Id);
            run.Property(record => record.Items).HasConversion(items => RunJson.Write(items), json => RunJson.ReadItems(json), RunJson.ItemsComparer);
            run.Property(record => record.Jobs).HasConversion(jobs => RunJson.Write(jobs), json => RunJson.ReadJobs(json), RunJson.JobsComparer);
        });

        modelBuilder.Entity<KeptVariable>(variable =>
        {
            variable.ToTable("Variables");
            // No key to the characters: a rule may set one as its connection begins, before the relay's record of it is written.
            variable.HasKey(kept => new { kept.AccountId, kept.CharacterId, kept.Name });
        });

        modelBuilder.Entity<KnownCharacter>(character =>
        {
            character.ToTable("Characters");
            character.HasKey(known => new { known.AccountId, known.Id });
            character.Ignore(known => known.Key);

            // Forgetting an account forgets its characters. A profile is a file of the project, which no key here reaches (D296).
            character.HasOne<KnownAccount>().WithMany().HasForeignKey(known => known.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
