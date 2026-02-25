namespace RecipeManager.Api.Infrastructure.Storage;

public class LocalFileStorageService : IStorageService
{
    private readonly string _basePath;
    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IConfiguration config, IWebHostEnvironment env)
    {
        var configuredPath = config["Storage:LocalPath"] ?? "./uploads";
        _env = env;
        _basePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_env.ContentRootPath, configuredPath);
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        var safeFileName = Path.GetFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";
        var filePath = Path.Combine(_basePath, uniqueFileName);

        using var fileStreamOut = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(fileStreamOut);

        return $"/uploads/{uniqueFileName}";
    }

    public Task DeleteAsync(string url)
    {
        var fileName = Path.GetFileName(url);
        var filePath = Path.Combine(_basePath, fileName);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }
}
