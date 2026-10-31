namespace MyHttpServer.Framework;

public sealed record HttpResult(
	int StatusCode,
	string Body,
	string ContentType = "text/plain; charset=utf-8",
	string? Location = null);