using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<HouseholdInvite> HouseholdInvites => Set<HouseholdInvite>();
    public DbSet<HouseholdActivityLog> HouseholdActivityLogs => Set<HouseholdActivityLog>();
    public DbSet<PaperCardImportDraft> PaperCardImportDrafts => Set<PaperCardImportDraft>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
    public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();
    public DbSet<CookEvent> CookEvents => Set<CookEvent>();
    public DbSet<VotingRound> VotingRounds => Set<VotingRound>();
    public DbSet<VotingNomination> VotingNominations => Set<VotingNomination>();
    public DbSet<VotingVote> VotingVotes => Set<VotingVote>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<FavoriteRecipe> FavoriteRecipes => Set<FavoriteRecipe>();
    public DbSet<AiDebugLog> AiDebugLogs => Set<AiDebugLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.GoogleId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Household>(entity =>
        {
            entity.HasIndex(h => h.InviteCode).IsUnique();
        });

        modelBuilder.Entity<HouseholdMember>(entity =>
        {
            entity.HasIndex(hm => new { hm.HouseholdId, hm.UserId }).IsUnique();
        });

        modelBuilder.Entity<HouseholdInvite>(entity =>
        {
            entity.HasIndex(i => new { i.HouseholdId, i.IsActive });
            entity.HasIndex(i => i.InviteCode);
        });

        modelBuilder.Entity<HouseholdActivityLog>(entity =>
        {
            entity.HasIndex(l => new { l.HouseholdId, l.CreatedAtUtc });
            entity.Property(l => l.EventType).HasMaxLength(100);
        });

        modelBuilder.Entity<PaperCardImportDraft>(entity =>
        {
            entity.HasIndex(d => new { d.HouseholdId, d.CreatedAtUtc });
            entity.HasIndex(d => d.ExpiresAtUtc);
        });

        modelBuilder.Entity<FavoriteRecipe>(entity =>
        {
            entity.HasIndex(fr => new { fr.UserId, fr.RecipeId }).IsUnique();
        });

        modelBuilder.Entity<VotingVote>(entity =>
        {
            entity.HasIndex(v => new { v.RoundId, v.UserId }).IsUnique();
        });

        modelBuilder.Entity<AiDebugLog>(entity =>
        {
            entity.HasIndex(l => l.CreatedAtUtc);
            entity.HasIndex(l => new { l.Provider, l.Operation });
            entity.HasIndex(l => l.Success);
        });
    }
}
