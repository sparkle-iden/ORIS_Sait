using System.Net;

namespace CustomHttpServer.Framework.Handlers;

public sealed class ControllerHandler : Handler
{
    protected override Task<bool> TryHandleAsync(HttpListenerContext context)
    {
        return Task.FromResult(false);
    }
}
