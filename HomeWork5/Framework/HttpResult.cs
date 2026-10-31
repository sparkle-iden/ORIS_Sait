namespace MyHttpServer.Framework;

public sealed record HttpResult(int StatusCode, string Body);