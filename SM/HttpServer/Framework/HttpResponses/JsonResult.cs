using System.Net;
using System.Text.Json;

namespace CustomHttpServer.Framework.HttpResponses;

public sealed class JsonResult(object? value, HttpStatusCode statusCode = HttpStatusCode.OK) : IHttpResult
{
    public async Task ExecuteAsync(HttpListenerContext context)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
    }
}
