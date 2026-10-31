namespace MyHttpServer.Framework.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class HttpControllerAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}