using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using MyHttpServer.Framework.Attributes;

namespace MyHttpServer.Framework.Handlers;

public sealed class ControllerHandler(AppSettings settings)
{
    public async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            var segments = context.Request.Url!.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length is < 1 or > 2)
            {
                await WriteResponseAsync(context.Response, 404, "Not found");
                return;
            }

            var controllerType = Assembly.GetExecutingAssembly().GetTypes()
                .FirstOrDefault(type =>
                    type.GetCustomAttribute<HttpControllerAttribute>()?.Route
                        .Equals(segments[0], StringComparison.OrdinalIgnoreCase) == true);
            var route = segments.Length == 1 ? string.Empty : segments[1];
            var method = controllerType?.GetMethods().FirstOrDefault(candidate =>
                GetRoute(candidate, context.Request.HttpMethod)?.Equals(
                    route,
                    StringComparison.OrdinalIgnoreCase) == true);

            if (controllerType is null || method is null)
            {
                await WriteResponseAsync(context.Response, 404, "Not found");
                return;
            }

            var parameters = method.GetParameters();
            var form = parameters.Any(parameter => parameter.GetCustomAttribute<FromFormAttribute>() is not null)
                ? await ReadFormAsync(context.Request)
                : null;
            var arguments = parameters.Select(parameter => BindParameter(parameter, context.Request, form)).ToArray();

            var controller = CreateController(controllerType);
            var invocation = method.Invoke(controller, arguments);
            if (invocation is not Task task)
                throw new InvalidOperationException("Controller action must return a Task.");

            await task;
            var result = task.GetType().GetProperty("Result")?.GetValue(task) as HttpResult
                ?? throw new InvalidOperationException("Controller action returned no HTTP result.");
            await WriteResponseAsync(
                context.Response,
                result.StatusCode,
                result.Body,
                result.ContentType,
                result.Location);
        }
        catch (ArgumentException exception)
        {
            await WriteResponseAsync(context.Response, 400, exception.Message);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is ArgumentException inner)
        {
            await WriteResponseAsync(context.Response, 400, inner.Message);
        }
        catch (KeyNotFoundException exception)
        {
            await WriteResponseAsync(context.Response, 404, exception.Message);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is KeyNotFoundException inner)
        {
            await WriteResponseAsync(context.Response, 404, inner.Message);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            await WriteResponseAsync(context.Response, 500, "Internal server error.");
        }
    }

    private static string? GetRoute(MethodInfo method, string httpMethod) => httpMethod.ToUpperInvariant() switch
    {
        "GET" => method.GetCustomAttribute<GetAttribute>()?.Route,
        "POST" => method.GetCustomAttribute<PostAttribute>()?.Route,
        "PUT" => method.GetCustomAttribute<PutAttribute>()?.Route,
        _ => null
    };

    private object CreateController(Type controllerType) =>
        controllerType.GetConstructor([typeof(AppSettings)]) is null
            ? Activator.CreateInstance(controllerType)!
            : Activator.CreateInstance(controllerType, settings)!;

    private static object? BindParameter(
        ParameterInfo parameter,
        HttpListenerRequest request,
        IReadOnlyDictionary<string, string>? form)
    {
        string? value;
        if (parameter.GetCustomAttribute<FromQueryAttribute>() is not null)
            value = request.QueryString[parameter.Name!];
        else if (parameter.GetCustomAttribute<FromFormAttribute>() is not null)
            form!.TryGetValue(parameter.Name!, out value);
        else
            throw new InvalidOperationException("Controller parameters must use FromQuery or FromForm.");

        if (string.IsNullOrWhiteSpace(value))
        {
            if (parameter.HasDefaultValue)
                return parameter.DefaultValue;
            throw new ArgumentException($"Missing parameter: {parameter.Name}");
        }

        if (parameter.ParameterType == typeof(string))
            return value;
        if (parameter.ParameterType == typeof(int) && int.TryParse(value, out var integer))
            return integer;
        throw new ArgumentException($"Invalid parameter: {parameter.Name}");
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadFormAsync(HttpListenerRequest request)
    {
        if (request.ContentLength64 > 65536 ||
            request.ContentType?.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) != true)
            throw new ArgumentException("Expected a URL-encoded form under 64 KB.");

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        if (body.Length > 65536)
            throw new ArgumentException("Form body is too large.");

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = field.IndexOf('=');
            var key = DecodeFormValue(separator < 0 ? field : field[..separator]);
            var value = DecodeFormValue(separator < 0 ? string.Empty : field[(separator + 1)..]);
            if (!values.TryAdd(key, value))
                throw new ArgumentException($"Duplicate form field: {key}");
        }
        return values;
    }

    private static string DecodeFormValue(string value) => HttpUtility.UrlDecode(value, Encoding.UTF8);

    private static async Task WriteResponseAsync(
        HttpListenerResponse response,
        int statusCode,
        string body,
        string contentType = "text/plain; charset=utf-8",
        string? location = null)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = statusCode;
        response.ContentType = contentType;
        if (location is not null)
            response.RedirectLocation = location;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }
}