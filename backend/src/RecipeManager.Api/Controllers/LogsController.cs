using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RecipeManager.Api.Controllers;

[ApiController]
[Route("api/logs")]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LogsController> _logger;

    public LogsController(IWebHostEnvironment env, ILogger<LogsController> logger)
    {
        _env = env;
        _logger = logger;
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

        _logger.LogInformation("Starting log stream for {LogFile}", logFile);

        await using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        stream.Seek(0, SeekOrigin.End);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
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
