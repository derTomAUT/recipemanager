using System.Security.Cryptography;
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
        int maxBytes = 1_000_000,
        Uri? pageUri = null)
    {
        var client = _factory.CreateClient();
        var results = new List<FetchedImage>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var url in urls)
        {
            if (results.Count >= maxImages) break;
            if (!seen.Add(url)) continue;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddBrowserHeaders(request, pageUri);
                using var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode) continue;

                var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0 || bytes.Length > maxBytes) continue;

                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                if (!seenHashes.Add(hash)) continue;

                results.Add(new FetchedImage(url, bytes, contentType));
            }
            catch
            {
                // Skip failures
            }
        }

        return results;
    }

    private static void AddBrowserHeaders(HttpRequestMessage request, Uri? pageUri)
    {
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Accept.ParseAdd("image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        request.Headers.Referrer = pageUri;
    }
}
