namespace MyHttpServer.Framework.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class GetAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}