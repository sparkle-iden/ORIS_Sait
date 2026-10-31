namespace MyHttpServer.Websites;

public sealed class WebsiteCatalog
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
    {
        "blog", "store", "portal"
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "unverified", "verified"
    };

    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.Ordinal)
    {
        "http", "https"
    };

    private readonly object sync = new();
    private readonly Dictionary<int, Website> websites = [];
    private int nextId = 1;

    public IReadOnlyList<Website> List(string? type = null, string? scheme = null)
    {
        var normalizedType = NormalizeFilter(type, AllowedTypes, "type");
        var normalizedScheme = NormalizeFilter(scheme, AllowedSchemes, "scheme");

        lock (sync)
        {
            return websites.Values
                .Where(website => normalizedType is null || website.Type == normalizedType)
                .Where(website => normalizedScheme is null ||
                    new Uri(website.Url).Scheme == normalizedScheme)
                .OrderBy(website => website.Id)
                .ToArray();
        }
    }

    public Website Get(int id)
    {
        lock (sync)
        {
            return websites.TryGetValue(id, out var website)
                ? website
                : throw new KeyNotFoundException("Website was not found.");
        }
    }

    public Website Create(string name, string url, string type)
    {
        var normalized = Validate(name, url, type);
        lock (sync)
        {
            EnsureUrlAvailable(normalized.Url);
            var website = new Website(nextId++, normalized.Name, normalized.Url, normalized.Type, "unverified");
            websites.Add(website.Id, website);
            return website;
        }
    }

    public Website Update(int id, string name, string url, string type, string status)
    {
        var normalized = Validate(name, url, type);
        var normalizedStatus = status.Trim().ToLowerInvariant();
        if (!AllowedStatuses.Contains(normalizedStatus))
            throw new ArgumentException("Status must be unverified or verified.");

        lock (sync)
        {
            if (!websites.ContainsKey(id))
                throw new KeyNotFoundException("Website was not found.");

            EnsureUrlAvailable(normalized.Url, id);
            var website = new Website(id, normalized.Name, normalized.Url, normalized.Type, normalizedStatus);
            websites[id] = website;
            return website;
        }
    }

    private void EnsureUrlAvailable(string url, int? excludedId = null)
    {
        if (websites.Values.Any(website =>
                website.Id != excludedId && string.Equals(website.Url, url, StringComparison.Ordinal)))
            throw new WebsiteUrlConflictException("A website with this URL already exists.");
    }

    private static (string Name, string Url, string Type) Validate(string name, string url, string type)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 3 or > 80)
            throw new ArgumentException("Name must contain from 3 to 80 characters.");

        var normalizedType = type.Trim().ToLowerInvariant();
        if (!AllowedTypes.Contains(normalizedType))
            throw new ArgumentException("Type must be blog, store, or portal.");

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsedUrl) ||
            (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("URL must be an absolute HTTP or HTTPS address.");

        return (normalizedName, parsedUrl.AbsoluteUri, normalizedType);
    }

    private static string? NormalizeFilter(string? value, IReadOnlySet<string> allowed, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToLowerInvariant();
        if (!allowed.Contains(normalized))
            throw new ArgumentException($"Invalid {field} filter.");
        return normalized;
    }
}

public sealed class WebsiteUrlConflictException(string message) : Exception(message);