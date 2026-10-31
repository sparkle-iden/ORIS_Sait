using System.Net;
using CustomHttpServer.Framework.Configuration;
using CustomHttpServer.Framework.Handlers;

namespace CustomHttpServer.Framework.Http;

public sealed class HttpServer
{
    private readonly HttpListener _listener = new();
    private readonly Handler _handlers;

    public HttpServer()
    {
        var settings = ServerSettings.Load(Path.Combine(AppContext.BaseDirectory, "settings.json"));
        var staticRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, settings.StaticFilesDirectory));
        Directory.CreateDirectory(staticRoot);

        _listener.Prefixes.Add(settings.UrlPrefix);
        var controllerHandler = new ControllerHandler
        {
            Next = new StaticFileHandler(staticRoot)
        };
        _handlers = controllerHandler;
    }

    public async Task Start()
    {
        _listener.Start();
        Console.WriteLine($"Сервер запущен: {_listener.Prefixes.First()}");
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
        if (!_listener.IsListening)
            return;

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
}
