using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeManager.Api.Infrastructure.Storage;

namespace RecipeManager.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize]
public class ImportCleanupController : ControllerBase
{
    private readonly IStorageService _storage;

    public ImportCleanupController(IStorageService storage)
    {
        _storage = storage;
    }

    [HttpPost("cleanup")]
    public async Task<IActionResult> Cleanup([FromBody] CleanupImagesRequest request)
    {
        if (request.TempUrls == null || request.TempUrls.Count == 0)
        {
            return Ok();
        }

        foreach (var url in request.TempUrls)
        {
            if (IsTempImageUrl(url))
            {
                await _storage.DeleteAsync(url);
            }
        }

        return Ok();
    }

    private static bool IsTempImageUrl(string url)
    {
        return url.StartsWith("/uploads/temp_", StringComparison.OrdinalIgnoreCase);
    }
}

public record CleanupImagesRequest(List<string> TempUrls);
