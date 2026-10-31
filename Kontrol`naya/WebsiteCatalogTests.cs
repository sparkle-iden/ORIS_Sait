using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyHttpServer.Websites;

namespace MyHttpServer.Tests;

[TestClass]
public sealed class WebsiteCatalogTests
{
    [TestMethod]
    public void CreateAssignsUniqueIdsAndUnverifiedStatus()
    {
        var catalog = new WebsiteCatalog();

        var first = catalog.Create("Первый сайт", "https://example.com", "blog");
        var second = catalog.Create("Второй сайт", "http://example.org", "store");

        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreEqual("unverified", first.Status);
    }

    [TestMethod]
    public void ListFiltersByTypeAndUrlScheme()
    {
        var catalog = new WebsiteCatalog();
        catalog.Create("HTTPS блог", "https://example.com", "blog");
        catalog.Create("HTTP магазин", "http://example.org", "store");
        catalog.Create("Другой блог", "http://example.net", "blog");

        var results = catalog.List("blog", "https");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("HTTPS блог", results[0].Name);
    }

    [TestMethod]
    public void CreateRejectsNonHttpUrl()
    {
        var catalog = new WebsiteCatalog();

        Assert.Throws<ArgumentException>(() =>
            catalog.Create("Неверный URL", "ftp://example.com", "portal"));
    }

    [TestMethod]
    public void CreateRejectsDuplicateUrl()
    {
        var catalog = new WebsiteCatalog();
        catalog.Create("Первый сайт", "https://example.com", "blog");

        Assert.Throws<WebsiteUrlConflictException>(() =>
            catalog.Create("Второй сайт", "https://example.com", "store"));
    }

    [TestMethod]
    public void UpdateRejectsUrlUsedByAnotherWebsite()
    {
        var catalog = new WebsiteCatalog();
        catalog.Create("Первый сайт", "https://example.com", "blog");
        var second = catalog.Create("Второй сайт", "https://example.org", "store");

        Assert.Throws<WebsiteUrlConflictException>(() =>
            catalog.Update(second.Id, "Второй сайт", "https://example.com", "store", "verified"));
    }
}