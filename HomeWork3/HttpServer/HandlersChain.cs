using System.Net;
using System.Reflection;
using System.Text;

namespace CustomHttpServer.Core;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class HttpMethodAttribute(string method, string path = "*") : Attribute
{
    public string Method { get; } = string.IsNullOrWhiteSpace(method)
        ? throw new ArgumentException("HTTP-метод не может быть пустым.", nameof(method))
        : method.ToUpperInvariant();

    public string Path { get; } = path == "*"
        ? path
        : NormalizePath(path);

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Путь обработчика не может быть пустым.", nameof(path));

        return "/" + path.Trim('/');
    }
}

public sealed class HttpGetAttribute(string path = "*") : HttpMethodAttribute("GET", path);
public sealed class HttpPostAttribute(string path = "*") : HttpMethodAttribute("POST", path);
public sealed class HttpPutAttribute(string path = "*") : HttpMethodAttribute("PUT", path);
public sealed class HttpPatchAttribute(string path = "*") : HttpMethodAttribute("PATCH", path);
public sealed class HttpDeleteAttribute(string path = "*") : HttpMethodAttribute("DELETE", path);
public sealed class HttpHeadAttribute(string path = "*") : HttpMethodAttribute("HEAD", path);
public sealed class HttpOptionsAttribute(string path = "*") : HttpMethodAttribute("OPTIONS", path);

public sealed class HandlersChain
{
    private readonly Dictionary<string, List<Endpoint>> _endpoints = new(StringComparer.OrdinalIgnoreCase);

    public HandlersChain(IEnumerable<object> handlers)
    {
        foreach (var handler in handlers)
        {
            foreach (var method in handler.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var attribute in method.GetCustomAttributes<HttpMethodAttribute>(inherit: true))
                {
                    var parameters = method.GetParameters();
                    if (method.ReturnType != typeof(Task<bool>)
                        || parameters.Length != 1
                        || parameters[0].ParameterType != typeof(HttpListenerContext))
                    {
                        throw new InvalidOperationException(
                            $"Метод {handler.GetType().Name}.{method.Name} должен принимать HttpListenerContext и возвращать Task<bool>.");
                    }

                    if (!_endpoints.TryGetValue(attribute.Method, out var endpoints))
                    {
                        endpoints = [];
                        _endpoints.Add(attribute.Method, endpoints);
                    }

                    endpoints.Add(new Endpoint(handler, method, attribute.Path));
                }
            }
        }
    }

    public async Task HandleAsync(HttpListenerContext context)
    {
        var method = context.Request.HttpMethod.ToUpperInvariant();
        var routeMethod = method == "HEAD" && !_endpoints.ContainsKey("HEAD") ? "GET" : method;
        if (!_endpoints.TryGetValue(routeMethod, out var endpoints))
        {
            context.Response.Headers[HttpResponseHeader.Allow] = GetAllowedMethods();
            await WriteTextResponse(context.Response, HttpStatusCode.MethodNotAllowed, "Method Not Allowed", method == "HEAD");
            return;
        }

        var path = Uri.UnescapeDataString(context.Request.Url?.AbsolutePath ?? "/");
        foreach (var endpoint in endpoints)
        {
            if (endpoint.Path != "*" && !endpoint.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) continue;

            var result = (Task<bool>)endpoint.Method.Invoke(endpoint.Handler, [context])!;
            if (await result) return;
        }

        await WriteTextResponse(context.Response, HttpStatusCode.NotFound, "404 Not Found", method == "HEAD");
    }

    private string GetAllowedMethods()
    {
        var methods = new HashSet<string>(_endpoints.Keys, StringComparer.OrdinalIgnoreCase);
        if (methods.Contains("GET")) methods.Add("HEAD");
        return string.Join(", ", methods.OrderBy(method => method, StringComparer.OrdinalIgnoreCase));
    }

    private static async Task WriteTextResponse(HttpListenerResponse response, HttpStatusCode status, string text,
        bool headOnly)
    {
        var content = Encoding.UTF8.GetBytes(text);
        response.StatusCode = (int)status;
        response.ContentType = "text/plain; charset=utf-8";
        response.ContentLength64 = content.Length;
        if (!headOnly) await response.OutputStream.WriteAsync(content);
    }

    private sealed record Endpoint(object Handler, MethodInfo Method, string Path);
}

public sealed class StaticFilesHandler(string webRoot)
{
    private static readonly string[] IndexFiles = ["index.html", "index.htm", "default.html"];
    private static readonly HashSet<string> PrivateDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "bin", "obj", "HttpServer"
    };

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
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

    private readonly string _root = Path.GetFullPath(webRoot);
    private readonly string _rootPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(webRoot)) + Path.DirectorySeparatorChar;

    [HttpGet]
    [HttpHead]
    public async Task<bool> ServeFileAsync(HttpListenerContext context)
    {
        var request = context.Request;

        var requestPath = Uri.UnescapeDataString(request.Url?.AbsolutePath ?? "/");
        var relativePath = requestPath.TrimStart('/', '\\');
        if (relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(PrivateDirectories.Contains))
            return false;

        var path = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!path.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase)
            && !path.Equals(_root, StringComparison.OrdinalIgnoreCase))
            return false;

        if (Directory.Exists(path))
        {
            var indexPath = IndexFiles.Select(name => Path.Combine(path, name)).FirstOrDefault(File.Exists);
            if (indexPath is not null)
            {
                path = indexPath;
            }
            else
            {
                await WriteResponse(context.Response, HttpStatusCode.OK,
                    CreateDirectoryListing(path), "text/html; charset=utf-8", request.HttpMethod == "HEAD");
                return true;
            }
        }

        if (!File.Exists(path) || !ContentTypes.TryGetValue(Path.GetExtension(path), out var contentType))
            return false;

        await WriteResponse(context.Response, HttpStatusCode.OK, await File.ReadAllBytesAsync(path),
            contentType, request.HttpMethod == "HEAD");
        return true;
    }

    private string CreateDirectoryListing(string directoryPath)
    {
        var relative = Path.GetRelativePath(_root, directoryPath).Replace('\\', '/');
        var segments = relative == "." ? [] : relative.Split('/');
        var baseHref = "/" + string.Concat(segments.Select(segment => Uri.EscapeDataString(segment) + "/"));
        var directories = Directory.EnumerateDirectories(directoryPath)
            .Where(directory => !directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(PrivateDirectories.Contains))
            .Select(directory => (Name: Path.GetFileName(directory), Suffix: "/"));
        var files = Directory.EnumerateFiles(directoryPath)
            .Where(file => ContentTypes.ContainsKey(Path.GetExtension(file)))
            .Select(file => (Name: Path.GetFileName(file), Suffix: ""));

        var html = new StringBuilder($"<!doctype html><html lang=\"ru\"><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(baseHref)}</title>")
            .Append($"<h1>{WebUtility.HtmlEncode(baseHref)}</h1><ul>");

        if (segments.Length > 0) html.Append("<li><a href=\"../\">..</a></li>");

        foreach (var (name, suffix) in directories.Concat(files)
                     .OrderBy(entry => entry.Suffix == "" ? 1 : 0)
                     .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
        {
            var href = baseHref + Uri.EscapeDataString(name) + suffix;
            html.Append($"<li><a href=\"{WebUtility.HtmlEncode(href)}\">{WebUtility.HtmlEncode(name + suffix)}</a></li>");
        }

        return html.Append("</ul></html>").ToString();
    }

    private static Task WriteResponse(HttpListenerResponse response, HttpStatusCode status, string text,
        string contentType = "text/plain; charset=utf-8", bool headOnly = false)
        => WriteResponse(response, status, Encoding.UTF8.GetBytes(text), contentType, headOnly);

    private static async Task WriteResponse(HttpListenerResponse response, HttpStatusCode status, byte[] content,
        string contentType, bool headOnly)
    {
        response.StatusCode = (int)status;
        response.ContentType = contentType;
        response.ContentLength64 = content.Length;
        if (!headOnly) await response.OutputStream.WriteAsync(content);
    }
}
