using System.Net;

namespace CustomHttpServer.Framework.Handlers;

public abstract class Handler
{
    public Handler? Next { get; set; }

    public async Task HandleAsync(HttpListenerContext context)
    {
        if (await TryHandleAsync(context))
            return;

        if (Next is not null)
        {
            await Next.HandleAsync(context);
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
    }

    protected abstract Task<bool> TryHandleAsync(HttpListenerContext context);
}
