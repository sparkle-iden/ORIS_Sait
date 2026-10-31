using System.Net;

namespace CustomHttpServer.Framework.Http;

public sealed class HttpRequestData(HttpListenerRequest request)
{
    public string Method { get; } = request.HttpMethod;
    public string Path { get; } = request.Url?.AbsolutePath ?? "/";
    public string? QueryString { get; } = request.Url?.Query;
}
