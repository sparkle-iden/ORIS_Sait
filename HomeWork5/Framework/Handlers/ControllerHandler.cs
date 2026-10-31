using System.Net;
using System.Reflection;
using System.Text;
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

            if (segments.Length != 2 || context.Request.HttpMethod != "GET")
            {
                await WriteResponseAsync(context.Response, 404, "Not found");
                return;
            }

            var controllerType = Assembly.GetExecutingAssembly().GetTypes()
                .FirstOrDefault(type =>
                    type.GetCustomAttribute<HttpControllerAttribute>()?.Route
                        .Equals(segments[0], StringComparison.OrdinalIgnoreCase) == true);
            var method = controllerType?.GetMethods()
                .FirstOrDefault(candidate =>
                    candidate.GetCustomAttribute<GetAttribute>()?.Route
                        .Equals(segments[1], StringComparison.OrdinalIgnoreCase) == true);

            if (controllerType is null || method is null)
            {
                await WriteResponseAsync(context.Response, 404, "Not found");
                return;
            }

            var arguments = method.GetParameters().Select(parameter =>
            {
                if (parameter.GetCustomAttribute<FromQueryAttribute>() is null)
                    throw new InvalidOperationException("Controller parameters must use FromQuery.");

                var value = context.Request.QueryString[parameter.Name!];
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException($"Missing query parameter: {parameter.Name}");
                return (object)value;
            }).ToArray();

            var controller = Activator.CreateInstance(controllerType, settings)!;
            var invocation = method.Invoke(controller, arguments);
            if (invocation is not Task task)
                throw new InvalidOperationException("Controller action must return a Task.");

            await task;
            var result = task.GetType().GetProperty("Result")?.GetValue(task) as HttpResult
                ?? throw new InvalidOperationException("Controller action returned no HTTP result.");
            await WriteResponseAsync(context.Response, result.StatusCode, result.Body);
        }
        catch (ArgumentException exception)
        {
            await WriteResponseAsync(context.Response, 400, exception.Message);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            await WriteResponseAsync(context.Response, 500, "Failed to send email.");
        }
    }

    private static async Task WriteResponseAsync(
        HttpListenerResponse response,
        int statusCode,
        string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = statusCode;
        response.ContentType = "text/plain; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }
}