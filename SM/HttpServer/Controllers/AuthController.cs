using System.Net;
using CustomHttpServer.Framework;
using CustomHttpServer.Framework.Attributes;

namespace CustomHttpServer.Controllers;

[Controller]
public sealed class AuthController(HttpListenerContext context) : ControllerBase(context)
{
}
