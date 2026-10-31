using System.Net;

namespace CustomHttpServer.Framework.HttpResponses;

public interface IHttpResult
{
    Task ExecuteAsync(HttpListenerContext context);
}
