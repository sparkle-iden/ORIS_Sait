namespace MyHttpServer;

public sealed class AppSettings
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 8888;
    public string GroupNumber { get; init; } = "11-541";
    public string StudentName { get; init; } = "lopatkin_anton_sergeevich";
    public string SmtpHost { get; init; } = "smtp.gmail.com";
    public int SmtpPort { get; init; } = 587;
    public string SmtpUser { get; init; } = "lopatkin.anton201@gmail.com";
    public string SmtpPassword { get; init; } = "yrifpyxnhpomtzvc";
    public string SmtpFrom { get; init; } = "lopatkin.anton201@gmail.com";
}