using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RecipeManager.Api.Controllers;
using RecipeManager.Api.Data;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Infrastructure.Storage;
using RecipeManager.Api.Models;
using RecipeManager.Api.Services;
using Xunit;

public class PaperCardImportControllerTests
{
    [Fact]
    public async Task Parse_RejectsMissingImages()
    {
        await using var db = CreateDb();
        var user = CreateUser(db, "parse-owner@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, user.Id, "Owner");
        await db.SaveChangesAsync();

        var controller = CreateController(db, user.Id);
        var result = await controller.Parse(null, null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Both frontImage and backImage are required.", badRequest.Value);
    }

    [Fact]
    public async Task Commit_RejectsUnavailableServingScale()
    {
        await using var db = CreateDb();
        var user = CreateUser(db, "commit-owner@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, user.Id, "Owner");

        var draft = new PaperCardImportDraft
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            CreatedByUserId = user.Id,
            Title = "Paper Draft",
            IngredientsByServingsJson = JsonSerializer.Serialize(new Dictionary<int, List<IngredientDto>>
            {
                [2] = [new IngredientDto("Tomato", "2", "pcs", null)]
            }),
            StepsJson = JsonSerializer.Serialize(new List<StepDto> { new("Step one", null) }),
            HeroImageUrl = "/uploads/temp_hero.jpg",
            StepImageUrlsJson = JsonSerializer.Serialize(new List<string> { "/uploads/temp_step.jpg" }),
            WarningsJson = "[]",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        };
        db.PaperCardImportDrafts.Add(draft);
        await db.SaveChangesAsync();

        var controller = CreateController(db, user.Id);
        var result = await controller.Commit(
            new CommitPaperCardImportRequest(draft.Id, 4, null, null, null, null, null, null, null),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Selected serving scale is not available in draft.", badRequest.Value);
    }

    [Fact]
    public async Task Commit_CreatesRecipeWithSelectedServing()
    {
        await using var db = CreateDb();
        var user = CreateUser(db, "commit-owner2@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, user.Id, "Owner");

        var draft = new PaperCardImportDraft
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            CreatedByUserId = user.Id,
            Title = "Paper Draft",
            IngredientsByServingsJson = JsonSerializer.Serialize(new Dictionary<int, List<IngredientDto>>
            {
                [2] = [new IngredientDto("Tomato", "2", "pcs", null)]
            }),
            StepsJson = JsonSerializer.Serialize(new List<StepDto> { new("Step one", null) }),
            HeroImageUrl = "/uploads/temp_hero.jpg",
            StepImageUrlsJson = JsonSerializer.Serialize(new List<string> { "/uploads/temp_step.jpg" }),
            WarningsJson = "[]",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        };
        db.PaperCardImportDrafts.Add(draft);
        await db.SaveChangesAsync();

        var controller = CreateController(db, user.Id);
        var result = await controller.Commit(
            new CommitPaperCardImportRequest(draft.Id, 2, null, null, null, null, null, null, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<CommitPaperCardImportResponse>(ok.Value);

        var recipe = await db.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Steps)
            .Include(r => r.Images)
            .FirstAsync(r => r.Id == payload.RecipeId);

        Assert.Equal(2, recipe.Servings);
        Assert.Single(recipe.Ingredients);
        Assert.Equal("Tomato", recipe.Ingredients.First().Name);
        Assert.Equal(2, recipe.Images.Count);
    }

    private static PaperCardImportController CreateController(AppDbContext db, Guid userId)
    {
        var controller = new PaperCardImportController(
            db,
            new TestStorageService(),
            new PaperCardVisionService(new HttpClientFactoryStub(), new HouseholdAiSettingsService(new TestDataProtectionProvider()), NullLogger<PaperCardVisionService>.Instance),
            NullLogger<PaperCardImportController>.Instance
        );

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "TestAuth"))
            }
        };
        return controller;
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static User CreateUser(AppDbContext db, string email)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            GoogleId = Guid.NewGuid().ToString("N"),
            Email = email,
            Name = email
        };
        db.Users.Add(user);
        return user;
    }

    private static Household CreateHousehold(AppDbContext db)
    {
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Paper",
            InviteCode = "PAPER123",
            InviteCodeCreatedAtUtc = DateTime.UtcNow,
            InviteCodeExpiresAtUtc = DateTime.UtcNow.AddDays(5)
        };
        db.Households.Add(household);
        return household;
    }

    private static void CreateMember(AppDbContext db, Guid householdId, Guid userId, string role)
    {
        db.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            UserId = userId,
            Role = role,
            IsActive = true
        });
    }

    private sealed class TestStorageService : IStorageService
    {
        public Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
            => Task.FromResult($"/uploads/{fileName}");

        public Task DeleteAsync(string url) => Task.CompletedTask;
    }

    private sealed class HttpClientFactoryStub : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class TestDataProtectionProvider : Microsoft.AspNetCore.DataProtection.IDataProtectionProvider
    {
        public Microsoft.AspNetCore.DataProtection.IDataProtector CreateProtector(string purpose)
            => new PassthroughProtector();
    }

    private sealed class PassthroughProtector : Microsoft.AspNetCore.DataProtection.IDataProtector
    {
        public Microsoft.AspNetCore.DataProtection.IDataProtector CreateProtector(string purpose) => this;
        public byte[] Protect(byte[] plaintext) => plaintext;
        public byte[] Unprotect(byte[] protectedData) => protectedData;
    }
}
