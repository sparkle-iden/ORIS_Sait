namespace CustomHttpServer.Framework.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RouteAttribute(string template) : Attribute
{
    public string Template { get; } = template;
}
