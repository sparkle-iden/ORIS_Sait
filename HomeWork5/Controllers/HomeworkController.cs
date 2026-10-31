using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using MyHttpServer.Framework;
using MyHttpServer.Framework.Attributes;

namespace MyHttpServer.Controllers;

[HttpController("home-work")]
public sealed class HomeworkController(AppSettings settings)
{
    [Get("send")]
    public async Task<HttpResult> Send([FromQuery] string to)
    {
        if (string.IsNullOrWhiteSpace(settings.GroupNumber) ||
            string.IsNullOrWhiteSpace(settings.StudentName))
            throw new InvalidOperationException("Student details are incomplete.");
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) ||
            string.IsNullOrWhiteSpace(settings.SmtpUser) ||
            string.IsNullOrWhiteSpace(settings.SmtpPassword))
            throw new InvalidOperationException("SMTP settings are incomplete.");

        var projectDirectory = FindProjectDirectory();
        var archivePath = Path.Combine(Path.GetTempPath(), $"homework-5-{Guid.NewGuid():N}.zip");

        try
        {
            CreateProjectArchive(projectDirectory, archivePath);

            using var message = new MailMessage
            {
                From = new MailAddress(
                    string.IsNullOrWhiteSpace(settings.SmtpFrom)
                        ? settings.SmtpUser
                        : settings.SmtpFrom,
                    "Лопаткин Антон Сергеевич"),
                Subject = "11-541",
                Body = "",
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(to));
            message.Attachments.Add(new Attachment(archivePath));

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using var smtp = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                EnableSsl = true,
                Credentials = new NetworkCredential(settings.SmtpUser, settings.SmtpPassword),
                Timeout = 50000
            };
            await smtp.SendMailAsync(message);
            return new HttpResult(200, "Email sent.");
        }
        finally
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
        }
    }

    private static string FindProjectDirectory()
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        for (var current = start; current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "MyHttpServer.csproj")))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate MyHttpServer.csproj.");
    }

    private static void CreateProjectArchive(string projectDirectory, string archivePath)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        var excludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", ".vs"
        };

        foreach (var file in Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(projectDirectory, file);
            var parts = relativePath.Split(Path.DirectorySeparatorChar);
            if (parts.Any(excludedDirectories.Contains) ||
                Path.GetFileName(file).Equals(".env", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".user", StringComparison.OrdinalIgnoreCase))
                continue;

            archive.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);
        }
    }
}