using MyHttpServer;

var settings = new AppSettings();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

await new HttpServer(settings).RunAsync(cancellation.Token);