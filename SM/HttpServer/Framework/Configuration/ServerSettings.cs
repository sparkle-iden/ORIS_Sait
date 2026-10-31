using System.Text.Json;

namespace CustomHttpServer.Framework.Configuration;

public sealed class ServerSettings
{
    public string UrlPrefix { get; init; } = "http://127.0.0.1:8888/";
    public string StaticFilesDirectory { get; init; } = "static";

    public static ServerSettings Load(string path)
    {
        if (!File.Exists(path))
            return new ServerSettings();

        return JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(path)) ?? new ServerSettings();
    }
}
