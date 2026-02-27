using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeManager.Api.Controllers;
using RecipeManager.Api.Data;
using RecipeManager.Api.Models;
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
        var household = CreateHousehold(db);
        CreateMember(db, household.Id, owner.Id, "Owner");
        await db.SaveChangesAsync();

        var controller = CreateController(db, owner.Id);
        var result = await controller.DisableMember(owner.Id);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Owner cannot disable themselves", badRequest.Value);
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
