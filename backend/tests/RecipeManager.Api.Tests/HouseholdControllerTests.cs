using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Controllers;
using RecipeManager.Api.Data;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Models;
using RecipeManager.Api.Services;
using Xunit;

public class HouseholdControllerTests
{
    [Fact]
    public async Task DisableMember_SetsMemberInactive()
    {
        await using var db = CreateDb();
        var owner = CreateUser(db, "owner@test.com");
        var target = CreateUser(db, "member@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, owner.Id, "Owner");
        CreateMember(db, household.Id, target.Id, "Member");
        await db.SaveChangesAsync();

        var controller = CreateController(db, owner.Id);
        var result = await controller.DisableMember(target.Id);

        Assert.IsType<NoContentResult>(result);
        var targetMembership = await db.HouseholdMembers.FirstAsync(h => h.UserId == target.Id);
        Assert.False(targetMembership.IsActive);
    }

    [Fact]
    public async Task EnableMember_SetsMemberActive()
    {
        await using var db = CreateDb();
        var owner = CreateUser(db, "owner2@test.com");
        var target = CreateUser(db, "member2@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, owner.Id, "Owner");
        CreateMember(db, household.Id, target.Id, "Member", isActive: false);
        await db.SaveChangesAsync();

        var controller = CreateController(db, owner.Id);
        var result = await controller.EnableMember(target.Id);

        Assert.IsType<NoContentResult>(result);
        var targetMembership = await db.HouseholdMembers.FirstAsync(h => h.UserId == target.Id);
        Assert.True(targetMembership.IsActive);
    }

    [Fact]
    public async Task DisableMember_RejectsSelfDisable()
    {
        await using var db = CreateDb();
        var owner = CreateUser(db, "owner3@test.com");
        var otherOwner = CreateUser(db, "owner3b@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, owner.Id, "Owner");
        CreateMember(db, household.Id, otherOwner.Id, "Owner");
        await db.SaveChangesAsync();

        var controller = CreateController(db, owner.Id);
        var result = await controller.DisableMember(owner.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Owner cannot disable themselves", badRequest.Value);
    }

    [Fact]
    public async Task DisableMember_RejectsWhenItWouldLeaveNoActiveMembers()
    {
        await using var db = CreateDb();
        var owner = CreateUser(db, "owner4@test.com");
        var target = CreateUser(db, "member4@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, owner.Id, "Owner");
        CreateMember(db, household.Id, target.Id, "Member", isActive: false);
        await db.SaveChangesAsync();

        var controller = CreateController(db, owner.Id);
        var result = await controller.DisableMember(owner.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("At least one active member must remain in the household", badRequest.Value);
    }

    [Fact]
    public async Task DisableMember_RejectsWhenTargetIsLastActiveOwner()
    {
        await using var db = CreateDb();
        var owner = CreateUser(db, "owner5@test.com");
        var member = CreateUser(db, "member5@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, owner.Id, "Owner");
        CreateMember(db, household.Id, member.Id, "Member");
        await db.SaveChangesAsync();

        var controller = CreateController(db, owner.Id);
        var result = await controller.DisableMember(owner.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Cannot disable the last active owner. Promote another member to Owner first.", badRequest.Value);
    }

    [Fact]
    public async Task JoinHousehold_RejectsExpiredInviteCode()
    {
        await using var db = CreateDb();
        var owner = CreateUser(db, "owner6@test.com");
        var joiner = CreateUser(db, "joiner6@test.com");
        var household = CreateHousehold(db);
        household.InviteCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        CreateMember(db, household.Id, owner.Id, "Owner");
        await db.SaveChangesAsync();

        var controller = CreateController(db, joiner.Id);
        var result = await controller.JoinHousehold(new JoinHouseholdRequest(household.InviteCode));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Invite code expired. Ask the owner to regenerate a new invite link.", badRequest.Value);
    }

    [Fact]
    public async Task RegenerateInvite_RotatesCodeAndMarksPreviousInvitesInactive()
    {
        await using var db = CreateDb();
        var owner = CreateUser(db, "owner7@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, owner.Id, "Owner");
        db.HouseholdInvites.Add(new HouseholdInvite
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            InviteCode = household.InviteCode,
            IsActive = true,
            CreatedByUserId = owner.Id,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(4)
        });
        await db.SaveChangesAsync();

        var oldCode = household.InviteCode;
        var controller = CreateController(db, owner.Id);
        var result = await controller.RegenerateInvite();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<HouseholdInviteDto>(ok.Value);
        Assert.NotEqual(oldCode, dto.InviteCode);

        var activeCount = await db.HouseholdInvites.CountAsync(i => i.HouseholdId == household.Id && i.IsActive);
        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task UpdateAiSettings_SavesCoordinates()
    {
        await using var db = CreateDb();
        var owner = CreateUser(db, "owner8@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, owner.Id, "Owner");
        await db.SaveChangesAsync();

        var controller = CreateController(db, owner.Id);
        var aiSettings = new HouseholdAiSettingsService(DataProtectionProvider.Create("HouseholdControllerTests"));

        var result = await controller.UpdateAiSettings(
            new UpdateHouseholdAiSettingsRequest("OpenAI", "gpt-4o-mini", "key", 47.0707, 15.4395),
            aiSettings);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<HouseholdAiSettingsDto>(ok.Value);
        Assert.Equal(47.0707, dto.Latitude);
        Assert.Equal(15.4395, dto.Longitude);

        var saved = await db.Households.FindAsync(household.Id);
        Assert.NotNull(saved);
        Assert.Equal(47.0707, saved!.Latitude);
        Assert.Equal(15.4395, saved.Longitude);
    }

    [Fact]
    public async Task UpdateAiSettings_RejectsInvalidLatitude()
    {
        await using var db = CreateDb();
        var owner = CreateUser(db, "owner9@test.com");
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, owner.Id, "Owner");
        await db.SaveChangesAsync();

        var controller = CreateController(db, owner.Id);
        var aiSettings = new HouseholdAiSettingsService(DataProtectionProvider.Create("HouseholdControllerTestsInvalid"));

        var result = await controller.UpdateAiSettings(
            new UpdateHouseholdAiSettingsRequest("OpenAI", "gpt-4o-mini", "key", 123.0, 10.0),
            aiSettings);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Latitude must be between -90 and 90.", badRequest.Value);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static HouseholdController CreateController(AppDbContext db, Guid userId)
    {
        var controller = new HouseholdController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    "TestAuth"))
            }
        };
        return controller;
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
            Name = "Test",
            InviteCode = "INV12345"
        };
        db.Households.Add(household);
        return household;
    }

    private static void CreateMember(AppDbContext db, Guid householdId, Guid userId, string role, bool isActive = true)
    {
        db.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            UserId = userId,
            Role = role,
            IsActive = isActive
        });
    }
}
