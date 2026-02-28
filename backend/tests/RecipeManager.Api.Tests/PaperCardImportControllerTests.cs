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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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

    [Fact]
    public async Task Parse_UsesHeroAndStepRegionsToCreateImportedImages()
    {
        await using var db = CreateDb();
        var user = CreateUser(db, "parse-images@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, user.Id, "Owner");
        await db.SaveChangesAsync();

        var fakeVision = new FakePaperCardVisionService(new PaperCardVisionResult(
            "Test Card",
            null,
            new Dictionary<int, List<IngredientDto>>
            {
                [2] = [new IngredientDto("Potato", "2", "pcs", null)],
                [3] = [],
                [4] = []
            },
            [new StepDto("Chop", null), new StepDto("Cook", null)],
            null,
            null,
            0.9,
            [],
            new ImageRegionDto(0.1, 0.1, 0.8, 0.5, 0),
            [new ImageRegionDto(0.05, 0.15, 0.4, 0.3, 1), new ImageRegionDto(0.55, 0.2, 0.4, 0.3, 0)]
        ));

        var storage = new TestStorageService();
        var controller = CreateController(db, user.Id, fakeVision, storage);
        var front = CreateImageFormFile("recipe1_front.jpeg");
        var back = CreateImageFormFile("recipe1_back.jpeg");

        var result = await controller.Parse(front, back, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PaperCardParseResponseDto>(ok.Value);
        Assert.Equal(3, payload.ImportedImages.Count);
        Assert.True(payload.ImportedImages[0].IsTitleImage);
        Assert.All(payload.ImportedImages.Skip(1), img => Assert.False(img.IsTitleImage));
        Assert.Equal(3, storage.UploadedFileNames.Count);
    }

    [Fact]
    public async Task UpdateDraftImage_UpdatesHeroAndReturnsImportedImages()
    {
        await using var db = CreateDb();
        var user = CreateUser(db, "update-image@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, user.Id, "Owner");

        var draft = new PaperCardImportDraft
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            CreatedByUserId = user.Id,
            Title = "Paper Draft",
            IngredientsByServingsJson = JsonSerializer.Serialize(new Dictionary<int, List<IngredientDto>> { [2] = [] }),
            StepsJson = JsonSerializer.Serialize(new List<StepDto> { new("Step one", null) }),
            HeroImageUrl = "/uploads/temp_old_hero.jpg",
            StepImageUrlsJson = JsonSerializer.Serialize(new List<string> { "/uploads/temp_step.jpg" }),
            WarningsJson = "[]",
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(2)
        };
        db.PaperCardImportDrafts.Add(draft);
        await db.SaveChangesAsync();

        var controller = CreateController(db, user.Id);
        var updateFile = CreateImageFormFile("updated.jpg");

        var result = await controller.UpdateDraftImage(draft.Id, 0, updateFile, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PaperCardUpdateImagesResponseDto>(ok.Value);
        Assert.Equal(2, payload.ImportedImages.Count);
        Assert.True(payload.ImportedImages[0].IsTitleImage);
        Assert.Contains("edited_0", payload.ImportedImages[0].Url);
    }

    private static PaperCardImportController CreateController(
        AppDbContext db,
        Guid userId,
        IPaperCardVisionService? vision = null,
        TestStorageService? storage = null)
    {
        storage ??= new TestStorageService();
        vision ??= new PaperCardVisionService(
            new HttpClientFactoryStub(),
            new HouseholdAiSettingsService(new TestDataProtectionProvider()),
            NullLogger<PaperCardVisionService>.Instance);

        var controller = new PaperCardImportController(
            db,
            storage,
            vision,
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
        public List<string> UploadedFileNames { get; } = new();

        public Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
        {
            UploadedFileNames.Add(fileName);
            return Task.FromResult($"/uploads/{fileName}");
        }

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

    private sealed class FakePaperCardVisionService(PaperCardVisionResult result) : IPaperCardVisionService
    {
        public Task<PaperCardVisionResult> ExtractAsync(
            IFormFile frontImage,
            IFormFile backImage,
            Household household,
            Guid? userId,
            CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private static IFormFile CreateImageFormFile(string fileName)
    {
        var diskPath = ResolveTestImagePath(fileName);
        if (diskPath != null)
        {
            var diskBytes = File.ReadAllBytes(diskPath);
            return new FormFile(new MemoryStream(diskBytes), 0, diskBytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };
        }

        using var image = new Image<Rgba32>(400, 600, new Rgba32(240, 240, 240));
        using var generated = new MemoryStream();
        image.SaveAsPng(generated);
        var bytes = generated.ToArray();
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }

    private static string? ResolveTestImagePath(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var candidate1 = Path.Combine(current.FullName, "testimages", fileName);
            if (File.Exists(candidate1))
            {
                return candidate1;
            }

            var candidate2 = Path.Combine(current.FullName, "backend", "src", "RecipeManager.Api", "uploads", "testimages", fileName);
            if (File.Exists(candidate2))
            {
                return candidate2;
            }

            current = current.Parent;
        }

        return null;
    }
}
