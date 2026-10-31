using System.Net;
using CustomHttpServer.Framework.Http;

namespace CustomHttpServer.Framework;

public abstract class ControllerBase(HttpListenerContext context)
{
    protected HttpListenerContext Context { get; } = context;
    protected HttpRequestData Request { get; } = new(context.Request);
}
