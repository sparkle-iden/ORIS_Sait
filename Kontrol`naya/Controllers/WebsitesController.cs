using System.Net;
using System.Text;
using MyHttpServer.Framework;
using MyHttpServer.Framework.Attributes;
using MyHttpServer.Websites;

namespace MyHttpServer.Controllers;

[HttpController("websites")]
public sealed class WebsitesController
{
    private static readonly WebsiteCatalog Catalog = new();

    [Get("")]
    public Task<HttpResult> List([FromQuery] string? type = null, [FromQuery] string? scheme = null)
    {
        var websites = Catalog.List(type, scheme);
        var html = new StringBuilder("<!doctype html><html lang=\"ru\"><meta charset=\"utf-8\"><title>Каталог сайтов</title><body>");
        html.Append("<h1>Каталог Web-сайтов</h1>");
        html.Append("<form method=\"get\" action=\"/websites\"><label>Тип <select name=\"type\"><option value=\"\">Все</option><option>blog</option><option>store</option><option>portal</option></select></label> ");
        html.Append("<label>Схема <select name=\"scheme\"><option value=\"\">Все</option><option>http</option><option>https</option></select></label> <button>Фильтровать</button></form>");
        html.Append("<h2>Сайты</h2><ul>");
        foreach (var website in websites)
        {
            html.Append("<li><a href=\"/websites/details?id=")
                .Append(website.Id)
                .Append("\">")
                .Append(Encode(website.Name))
                .Append("</a> — <a href=\"")
                .Append(Encode(website.Url))
                .Append("\">")
                .Append(Encode(website.Url))
                .Append("</a> ( ")
                .Append(Encode(website.Type))
                .Append(", ")
                .Append(Encode(website.Status))
                .Append(" )</li>");
        }
        html.Append("</ul><h2>Добавить сайт</h2><form method=\"post\" action=\"/websites/create\">");
        html.Append("<label>Название <input name=\"name\" minlength=\"3\" maxlength=\"80\" required></label> ");
        html.Append("<label>URL <input name=\"url\" type=\"url\" required></label> ");
        html.Append("<label>Тип <select name=\"type\" required><option value=\"blog\">blog</option><option value=\"store\">store</option><option value=\"portal\">portal</option></select></label> ");
        html.Append("<button type=\"submit\">Создать</button></form></body></html>");
        return Task.FromResult(new HttpResult(200, html.ToString(), "text/html; charset=utf-8"));
    }

    [Get("details")]
    public Task<HttpResult> Details([FromQuery] int id)
    {
        var website = Catalog.Get(id);
        var html = $"<!doctype html><html lang=\"ru\"><meta charset=\"utf-8\"><title>{Encode(website.Name)}</title><body>" +
            $"<h1>{Encode(website.Name)}</h1><dl><dt>URL</dt><dd><a href=\"{Encode(website.Url)}\">{Encode(website.Url)}</a></dd>" +
            $"<dt>Тип</dt><dd>{Encode(website.Type)}</dd><dt>Статус проверки</dt><dd>{Encode(website.Status)}</dd></dl>" +
            "<a href=\"/websites\">К списку</a></body></html>";
        return Task.FromResult(new HttpResult(200, html, "text/html; charset=utf-8"));
    }

    [Post("create")]
    public Task<HttpResult> Create(
        [FromForm] string name,
        [FromForm] string url,
        [FromForm] string type)
    {
        try
        {
            var website = Catalog.Create(name, url, type);
            return Task.FromResult(new HttpResult(
                303,
                string.Empty,
                Location: $"/websites/details?id={website.Id}"));
        }
        catch (WebsiteUrlConflictException exception)
        {
            return Task.FromResult(new HttpResult(409, exception.Message));
        }
    }

    [Put("update")]
    public Task<HttpResult> Update(
        [FromQuery] int id,
        [FromForm] string name,
        [FromForm] string url,
        [FromForm] string type,
        [FromForm] string status)
    {
        try
        {
            Catalog.Update(id, name, url, type, status);
            return Task.FromResult(new HttpResult(204, string.Empty));
        }
        catch (WebsiteUrlConflictException exception)
        {
            return Task.FromResult(new HttpResult(409, exception.Message));
        }
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}