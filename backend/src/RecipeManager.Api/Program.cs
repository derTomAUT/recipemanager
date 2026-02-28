using System.Text;
using System.IO;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using RecipeManager.Api.Data;
using RecipeManager.Api.Infrastructure.Auth;
using RecipeManager.Api.Infrastructure.Storage;
using RecipeManager.Api.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IStorageService, LocalFileStorageService>();

// Caching
builder.Services.AddMemoryCache();

// Application services
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<MealAssistantService>();
builder.Services.AddScoped<RecipeImportService>();
builder.Services.AddScoped<AiRecipeImportService>();
builder.Services.AddScoped<AiDebugLogService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ImageFetchService>();
builder.Services.AddSingleton<HouseholdAiSettingsService>();
builder.Services.AddScoped<AiModelCatalogService>();
builder.Services.AddScoped<IPaperCardVisionService, PaperCardVisionService>();

// Auth services
builder.Services.AddSingleton<GoogleTokenValidator>();
builder.Services.AddSingleton<JwtTokenGenerator>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/api/logs/stream"))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

var configuredDataProtectionPath = builder.Configuration["DataProtection:KeysPath"];
var defaultDataProtectionPath = builder.Environment.IsDevelopment()
    ? "./dpkeys"
    : "/app/dpkeys";
var dataProtectionPath = configuredDataProtectionPath ?? defaultDataProtectionPath;
var dataProtectionKeysRoot = Path.IsPathRooted(dataProtectionPath)
    ? dataProtectionPath
    : Path.Combine(builder.Environment.ContentRootPath, dataProtectionPath);
Directory.CreateDirectory(dataProtectionKeysRoot);

builder.Services.AddDataProtection()
    .SetApplicationName("RecipeManager.Api")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysRoot));

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Host.UseSerilog();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var origins = configuredOrigins
            .Concat(new[]
            {
                "http://localhost:4200",
                "http://127.0.0.1:4200",
                "https://kaia5.tzis.net",
                "http://kaia5.tzis.net"
            })
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
var uploadsPath = builder.Configuration["Storage:LocalPath"] ?? "./uploads";
var uploadsRoot = Path.IsPathRooted(uploadsPath)
    ? uploadsPath
    : Path.Combine(app.Environment.ContentRootPath, uploadsPath);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception != null)
        {
            logger.LogError(exception, "Unhandled exception");
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("Internal server error");
    });
});

app.UseSerilogRequestLogging();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles(); // For wwwroot
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads"
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/api/uploads"
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapFallbackToFile("index.html");

app.Run();
