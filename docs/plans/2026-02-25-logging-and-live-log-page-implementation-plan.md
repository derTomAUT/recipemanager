# Logging + Live Log Page Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add file-based Serilog logging with global exception handling and controller catch logging, plus a live, authenticated frontend log viewer via SSE.

**Architecture:** Configure Serilog file sink with daily rolling logs and request logging; add global exception handler and explicit controller logging for caught exceptions; expose an authenticated SSE endpoint that tails the latest log file; add a Logs page that subscribes via `EventSource` using JWT in query string.

**Tech Stack:** ASP.NET Core, Serilog, Angular standalone components, EventSource (SSE).

---

### Task 1: Add Serilog file sink configuration

**Files:**
- Modify: `backend/src/RecipeManager.Api/RecipeManager.Api.csproj`
- Modify: `backend/src/RecipeManager.Api/appsettings.json`
- Modify: `backend/src/RecipeManager.Api/appsettings.Development.json`

**Step 1: Add Serilog file sink package**

Edit `backend/src/RecipeManager.Api/RecipeManager.Api.csproj`:

```xml
<PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
```

**Step 2: Add Serilog file sink config**

Edit `backend/src/RecipeManager.Api/appsettings.json`:

```json
"Serilog": {
  "MinimumLevel": "Information",
  "WriteTo": [
    {
      "Name": "File",
      "Args": {
        "path": "logs/recipemanager-.log",
        "rollingInterval": "Day",
        "shared": true,
        "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
      }
    }
  ]
}
```

Edit `backend/src/RecipeManager.Api/appsettings.Development.json` to include matching `Serilog` section or override `MinimumLevel` only.

**Step 3: Run a build to validate package restore**

Run: `dotnet build backend/src/RecipeManager.Api/RecipeManager.Api.csproj`
Expected: PASS

**Step 4: Commit**

```bash
git add backend/src/RecipeManager.Api/RecipeManager.Api.csproj backend/src/RecipeManager.Api/appsettings.json backend/src/RecipeManager.Api/appsettings.Development.json
git commit -m "feat: add Serilog file sink configuration"
```

---

### Task 2: Add global exception handler + request logging

**Files:**
- Modify: `backend/src/RecipeManager.Api/Program.cs`

**Step 1: Add Serilog request logging**

Add `app.UseSerilogRequestLogging();` before `app.UseCors();`.

**Step 2: Add global exception handler**

Add:

```csharp
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
```

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Program.cs
git commit -m "feat: add global exception logging"
```

---

### Task 3: Add controller catch logging

**Files:**
- Modify: `backend/src/RecipeManager.Api/Controllers/RecipeController.cs`
- Modify: `backend/src/RecipeManager.Api/Controllers/AuthController.cs`

**Step 1: Inject ILogger into controllers**

Add `ILogger<RecipeController>` and `ILogger<AuthController>` in constructors and fields.

**Step 2: Log exceptions in catch blocks**

Example in `RecipeController`:

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to import recipe from URL: {Url}", request.Url);
    return StatusCode(502, "Failed to import recipe from URL");
}
```

Apply to each existing `catch` block in `RecipeController` and `AuthController`.

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Controllers/RecipeController.cs backend/src/RecipeManager.Api/Controllers/AuthController.cs
git commit -m "feat: log exceptions in controllers"
```

---

### Task 4: Add SSE log streaming endpoint

**Files:**
- Create: `backend/src/RecipeManager.Api/Controllers/LogsController.cs`
- Modify: `backend/src/RecipeManager.Api/Program.cs`

**Step 1: Allow JWT from query for SSE**

Update `AddJwtBearer` in `Program.cs` to accept `access_token` for `/api/logs/stream`:

```csharp
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
```

**Step 2: Create LogsController SSE endpoint**

Add `LogsController`:

```csharp
[ApiController]
[Route("api/logs")]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public LogsController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var logDir = Path.Combine(_env.ContentRootPath, "logs");
        if (!Directory.Exists(logDir))
        {
            await Response.WriteAsync("event: error\ndata: log directory not found\n\n", cancellationToken);
            return;
        }

        var logFile = Directory.GetFiles(logDir, "recipemanager-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        if (logFile == null)
        {
            await Response.WriteAsync("event: error\ndata: log file not found\n\n", cancellationToken);
            return;
        }

        await using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        // Start at end of file
        stream.Seek(0, SeekOrigin.End);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line != null)
            {
                await Response.WriteAsync($"data: {line}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            else
            {
                await Task.Delay(500, cancellationToken);
            }
        }
    }
}
```

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Controllers/LogsController.cs backend/src/RecipeManager.Api/Program.cs
git commit -m "feat: add SSE log streaming endpoint"
```

---

### Task 5: Add frontend Logs page

**Files:**
- Create: `frontend/src/app/pages/logs/logs.component.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/pages/home/home.component.ts`

**Step 1: Create LogsComponent**

Create `logs.component.ts`:

```ts
const token = this.authService.getToken();
this.eventSource = new EventSource(`${environment.apiUrl}/logs/stream?access_token=${encodeURIComponent(token ?? '')}`);
```

Render lines in a list, with pause/resume and clear. Track `connected` status via `onopen` / `onerror`.

**Step 2: Add route**

```ts
{ path: 'logs', canActivate: [authGuard], loadComponent: () => import('./pages/logs/logs.component').then(m => m.LogsComponent) }
```

**Step 3: Add Home nav link**

Add “Logs” link for all authenticated users in `HomeComponent`.

**Step 4: Commit**

```bash
git add frontend/src/app/pages/logs/logs.component.ts frontend/src/app/app.routes.ts frontend/src/app/pages/home/home.component.ts
git commit -m "feat: add live logs page"
```

---

### Task 6: Verification

**Step 1: Backend smoke**

Run: `dotnet build backend/src/RecipeManager.Api/RecipeManager.Api.csproj`
Expected: PASS

**Step 2: Manual**
- Trigger a failing import; confirm log appears in `backend/logs/recipemanager-YYYYMMDD.log`.
- Open `/logs` page and confirm live updates; pause/resume works.

**Step 3: Commit any fixes**

```bash
git add backend/src/RecipeManager.Api/Program.cs backend/src/RecipeManager.Api/Controllers/LogsController.cs frontend/src/app/pages/logs/logs.component.ts
git commit -m "test: verify logging and live log stream"
```
