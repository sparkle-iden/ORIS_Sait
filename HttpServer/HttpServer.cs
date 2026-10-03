using System.Net;
using System.Text;

namespace CustomHttpServer.Core;

public sealed class HttpServer
{
    private static readonly string[] PrivateDirs = [".git", ".vs", "bin", "obj"];
    private static readonly string[] IndexFiles = ["index.html", "index.htm", "default.html"];

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".map"] = "application/json; charset=utf-8",
        [".webmanifest"] = "application/json; charset=utf-8",
        [".xml"] = "application/xml; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
        [".eot"] = "application/vnd.ms-fontobject",
        [".pdf"] = "application/pdf",
        [".wasm"] = "application/wasm",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".mp3"] = "audio/mpeg",
        [".ogg"] = "audio/ogg"
    };

    private readonly HttpListener _listener = new();
    private readonly string _webRoot = FindOrisRoot();
    private string RootPrefix => Path.TrimEndingDirectorySeparator(_webRoot) + Path.DirectorySeparatorChar;

    public HttpServer() => _listener.Prefixes.Add("http://127.0.0.1:8888/");

    public async Task Start()
    {
        _listener.Start();
        Console.WriteLine($"Сервер запущен: http://127.0.0.1:8888/ (корень: {_webRoot})");

        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { break; }

            _ = HandleRequest(context);
        }
    }

    public void Stop()
    {
        if (!_listener.IsListening) return;
        _listener.Stop();
        Console.WriteLine("Сервер остановлен.");
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var (request, response) = (context.Request, context.Response);
        try
        {
            var requestPath = Uri.UnescapeDataString(request.Url?.AbsolutePath ?? "/");

            if (request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && requestPath.Equals("/Login_Form/login.html", StringComparison.OrdinalIgnoreCase))
            {
                await HandleLogin(request, response);
                Console.WriteLine($"POST {requestPath} -> {response.StatusCode}");
                return;
            }

            var path = Path.GetFullPath(Path.Combine(_webRoot, requestPath.TrimStart('/', '\\')));

            if (!(path + Path.DirectorySeparatorChar).StartsWith(RootPrefix, StringComparison.OrdinalIgnoreCase)
                || ContainsPrivateDirectory(path))
            {
                await WriteResponse(response, HttpStatusCode.NotFound, "404 Not Found");
                return;
            }

            if (Directory.Exists(path))
            {
                var index = IndexFiles.Select(name => Path.Combine(path, name)).FirstOrDefault(File.Exists);
                if (index is null)
                {
                    await WriteResponse(response, HttpStatusCode.OK, CreateDirectoryListing(path), "text/html; charset=utf-8");
                    Console.WriteLine($"{request.HttpMethod} {requestPath} -> 200 (каталог)");
                    return;
                }
                path = index;
            }

            if (!File.Exists(path) || !ContentTypes.TryGetValue(Path.GetExtension(path), out var contentType))
            {
                await WriteResponse(response, HttpStatusCode.NotFound, "404 Not Found");
                return;
            }

            await WriteResponse(response, HttpStatusCode.OK, await File.ReadAllBytesAsync(path), contentType);
            Console.WriteLine($"{request.HttpMethod} {requestPath} -> 200");
        }
        catch (Exception e) when (e is IOException or HttpListenerException)
        {
            Console.WriteLine("Соединение с клиентом было прервано.");
        }
        finally
        {
            response.Close();
        }
    }

    private static async Task HandleLogin(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (request.ContentType?.Split(';', 2)[0].Trim()
            .Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) != true)
        {
            await WriteResponse(response, HttpStatusCode.UnsupportedMediaType, "Ожидалась HTML-форма.");
            return;
        }

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var fields = ParseFormData(await reader.ReadToEndAsync());
        Console.WriteLine($"Данные формы: {string.Join(", ", fields.Select(f => $"{f.Key}={f.Value}"))}");

        if (!fields.TryGetValue("email", out var email) || string.IsNullOrWhiteSpace(email)
            || !fields.TryGetValue("password", out var password) || string.IsNullOrWhiteSpace(password))
        {
            await WriteResponse(response, HttpStatusCode.BadRequest, "Необходимо указать email и пароль.");
            return;
        }

        Console.WriteLine("Получен запрос входа: email и пароль приняты.");
        await WriteResponse(response, HttpStatusCode.OK, "Данные формы получены.");
    }

    private static Dictionary<string, string> ParseFormData(string body) => body
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(item => item.Split('=', 2))
        .GroupBy(p => WebUtility.UrlDecode(p[0]))
        .ToDictionary(g => g.Key, g => g.Last().Length > 1 ? WebUtility.UrlDecode(g.Last()[1]) : "");

    private static Task WriteResponse(HttpListenerResponse response, HttpStatusCode status, string text,
        string contentType = "text/plain; charset=utf-8")
        => WriteResponse(response, status, Encoding.UTF8.GetBytes(text), contentType);

    private static async Task WriteResponse(HttpListenerResponse response, HttpStatusCode status, byte[] content,
        string contentType = "text/plain; charset=utf-8")
    {
        response.StatusCode = (int)status;
        response.ContentType = contentType;
        response.ContentLength64 = content.Length;
        await response.OutputStream.WriteAsync(content);
    }

    private string CreateDirectoryListing(string directoryPath)
    {
        var relative = Path.GetRelativePath(_webRoot, directoryPath).Replace('\\', '/');
        var segments = relative == "." ? [] : relative.Split('/');
        var baseHref = "/" + string.Concat(segments.Select(s => Uri.EscapeDataString(s) + "/"));

        var entries = Directory.EnumerateDirectories(directoryPath)
            .Where(d => !ContainsPrivateDirectory(d))
            .Select(d => (Name: Path.GetFileName(d), Suffix: "/"))
            .Concat(Directory.EnumerateFiles(directoryPath)
                .Where(f => ContentTypes.ContainsKey(Path.GetExtension(f)))
                .Select(f => (Name: Path.GetFileName(f), Suffix: "")));

        var html = new StringBuilder($"<!doctype html><html lang=\"ru\"><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(baseHref)}</title>")
            .Append($"<h1>{WebUtility.HtmlEncode(baseHref)}</h1><ul>");

        if (segments.Length > 0) html.Append("<li><a href=\"../\">..</a></li>");

        foreach (var (name, suffix) in entries.OrderBy(e => e.Suffix == "" ? 1 : 0).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            html.Append($"<li><a href=\"{WebUtility.HtmlEncode(baseHref + Uri.EscapeDataString(name) + suffix)}\">{WebUtility.HtmlEncode(name + suffix)}</a></li>");

        return html.Append("</ul></html>").ToString();
    }

    private static bool ContainsPrivateDirectory(string path) => path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(s => PrivateDirs.Contains(s, StringComparer.OrdinalIgnoreCase));

    private static string FindOrisRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "HttpServer.csproj")) && dir.Parent is not null)
                    return dir.Parent.FullName;

                var git = Path.Combine(dir.FullName, ".git");
                if (File.Exists(Path.Combine(dir.FullName, "HttpServer", "HttpServer.csproj"))
                    && (Directory.Exists(git) || File.Exists(git)))
                    return dir.FullName;
            }

        throw new DirectoryNotFoundException("Не удалось найти корень ORIS с проектом HttpServer.");
    }
}