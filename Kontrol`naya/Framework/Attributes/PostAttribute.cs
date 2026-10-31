namespace MyHttpServer.Framework.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PostAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}