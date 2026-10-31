namespace MyHttpServer.Framework.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PutAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}