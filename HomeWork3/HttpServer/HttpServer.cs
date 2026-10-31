using System.Net;

namespace CustomHttpServer.Core;

public sealed class HttpServer
{
    private readonly HttpListener _listener = new();
    private readonly string _webRoot = FindWebRoot();
    private readonly HandlersChain _handlers;

    public HttpServer()
    {
        _listener.Prefixes.Add("http://127.0.0.1:8888/");
        _handlers = new HandlersChain(
        [
            new StaticFilesHandler(_webRoot)
        ]);
    }

    public async Task Start()
    {
        _listener.Start();
        Console.WriteLine($"Сервер запущен: http://127.0.0.1:8888/ (папка: {_webRoot})");
        Console.WriteLine("Для остановки нажмите Ctrl+C.");

        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                break;
            }

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
        try
        {
            await _handlers.HandleAsync(context);
            Console.WriteLine($"{context.Request.HttpMethod} {context.Request.Url?.AbsolutePath} -> {context.Response.StatusCode}");
        }
        catch (Exception exception) when (exception is IOException or HttpListenerException or ObjectDisposedException)
        {
            Console.WriteLine("Соединение с клиентом было прервано.");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Ошибка обработки запроса: {exception.Message}");
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            catch (HttpListenerException)
            {
            }
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static string FindWebRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (directory.Name.Equals("HomeWork3", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(Path.Combine(directory.FullName, "HttpServer", "HttpServer.csproj")))
                    return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Не удалось найти папку HomeWork3 с проектом HttpServer.");
    }
}