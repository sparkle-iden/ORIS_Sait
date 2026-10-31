using System.Net;
using System.Text;

namespace CustomHttpServer.Framework.HttpResponses;

public sealed class HtmlResult(string content, HttpStatusCode statusCode = HttpStatusCode.OK) : IHttpResult
{
    public async Task ExecuteAsync(HttpListenerContext context)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
    }
}
