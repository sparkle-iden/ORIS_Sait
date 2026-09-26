using System.Net;
using System.Text;

namespace CustomHttpServer.Core;

public sealed class HttpServer
{
    private readonly HttpListener _listener = new();
    private readonly string _webRoot;

    public HttpServer()
    {
        _webRoot = FindSearchDirectory();
        _listener.Prefixes.Add("http://127.0.0.1:8888/");
    }

    public async Task Start()
    {
        if (!File.Exists(Path.Combine(_webRoot, "index.html")))
        {
            throw new FileNotFoundException("В папке Search не найден index.html.", _webRoot);
        }

        _listener.Start();
        Console.WriteLine("Сервер запущен: http://127.0.0.1:8888/");

        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (HttpListenerException) when (!_listener.IsListening)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = HandleRequest(context);
        }
    }

    public void Stop()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
            Console.WriteLine("Сервер остановлен.");
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            var requestPath = Uri.UnescapeDataString(context.Request.Url?.AbsolutePath ?? "/");
            var relativePath = requestPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(relativePath))
            {
                relativePath = "index.html";
            }

            var filePath = Path.GetFullPath(Path.Combine(_webRoot, relativePath));
            var rootPrefix = _webRoot + Path.DirectorySeparatorChar;
            if (!filePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.ContentType = "text/plain; charset=utf-8";
                var notFound = Encoding.UTF8.GetBytes("404 Not Found");
                response.ContentLength64 = notFound.Length;
                await response.OutputStream.WriteAsync(notFound);
                return;
            }

            response.ContentType = GetContentType(filePath);
            var content = await File.ReadAllBytesAsync(filePath);
            response.ContentLength64 = content.Length;
            await response.OutputStream.WriteAsync(content);
            Console.WriteLine($"{context.Request.HttpMethod} {requestPath} -> 200");
        }
        catch (IOException)
        {
            Console.WriteLine("Клиент закрыл соединение во время ответа.");
        }
        catch (HttpListenerException)
        {
            Console.WriteLine("Соединение с клиентом было прервано.");
        }
        finally
        {
            response.Close();
        }
    }

    private static string GetContentType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream"
    };

    private static string FindSearchDirectory()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "Search");
                if (File.Exists(Path.Combine(candidate, "index.html")))
                {
                    return candidate;
                }
            }
        }

        throw new DirectoryNotFoundException("Не удалось найти папку Search с файлом index.html.");
    }
}