using System.Net;
using MyHttpServer.Framework.Handlers;

namespace MyHttpServer;

public sealed class HttpServer(AppSettings settings)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://{settings.Host}:{settings.Port}/");
        listener.Start();
        Console.WriteLine($"Server started at http://{settings.Host}:{settings.Port}/");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync()
                    .WaitAsync(cancellationToken);
                _ = new ControllerHandler(settings).HandleRequestAsync(context);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Stop();
        }
    }
}