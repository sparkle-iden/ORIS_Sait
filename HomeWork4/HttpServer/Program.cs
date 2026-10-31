using CustomHttpServer.Core;

var server = new HttpServer();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    server.Stop();
};

await server.Start();