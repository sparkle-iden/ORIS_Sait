using System.Net;

namespace CustomHttpServer.Framework.Handlers;

public sealed class StaticFileHandler(string webRoot) : Handler
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".css"] = "text/css; charset=utf-8",
        [".html"] = "text/html; charset=utf-8",
        [".ico"] = "image/x-icon",
        [".js"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".png"] = "image/png",
        [".svg"] = "image/svg+xml",
        [".txt"] = "text/plain; charset=utf-8"
    };

    private readonly string _webRoot = Path.GetFullPath(webRoot);
    private readonly string _webRootPrefix = Path.GetFullPath(webRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    protected override async Task<bool> TryHandleAsync(HttpListenerContext context)
    {
        var relativePath = Uri.UnescapeDataString(context.Request.Url?.AbsolutePath ?? "/")
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);

        if (string.IsNullOrEmpty(relativePath))
            relativePath = "index.html";

        var filePath = Path.GetFullPath(Path.Combine(_webRoot, relativePath));
        if (!filePath.StartsWith(_webRootPrefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(filePath, _webRoot, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return true;
        }

        if (!File.Exists(filePath))
            return false;

        context.Response.ContentType = ContentTypes.TryGetValue(Path.GetExtension(filePath), out var contentType)
            ? contentType
            : "application/octet-stream";
        context.Response.ContentLength64 = new FileInfo(filePath).Length;

        await using var file = File.OpenRead(filePath);
        await file.CopyToAsync(context.Response.OutputStream);
        return true;
    }
}
