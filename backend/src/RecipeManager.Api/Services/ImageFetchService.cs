namespace RecipeManager.Api.Services;

public record FetchedImage(
    string Url,
    byte[] Bytes,
    string ContentType
);

public class ImageFetchService
{
    private readonly IHttpClientFactory _factory;

    public ImageFetchService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<List<FetchedImage>> FetchImagesAsync(
        IEnumerable<string> urls,
        int maxImages = 30,
        int maxBytes = 1_000_000)
    {
        var client = _factory.CreateClient();
        var results = new List<FetchedImage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in urls)
        {
            if (results.Count >= maxImages) break;
            if (!seen.Add(url)) continue;

            try
            {
                using var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) continue;

                var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0 || bytes.Length > maxBytes) continue;

                results.Add(new FetchedImage(url, bytes, contentType));
            }
            catch
            {
                // Skip failures
            }
        }

        return results;
    }
}
