using OccamMcp.Core.Probe;
using OccamMcp.Core.Services;
using OccamMcp.Core.Tools;

namespace OccamMcp.L0Gate;

internal static class L2MapUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        assert("map max_links cap", MapService.MaxLinksCap == 64);
        assert("map default max_links", MapService.DefaultMaxLinks == 32);
        assert("map source homepage", MapService.NormalizeSource("") == "homepage");
        assert("map source sitemap", MapService.NormalizeSource("sitemap") == "sitemap");
        assert("map source robots", MapService.NormalizeSource("robots") == "robots");
        assert("map source browser_nav rejected", MapService.NormalizeSource("browser_nav") == "invalid_source");
        assert("map source all rejected", MapService.NormalizeSource("all") == "invalid_source");

        var html = """
            <html><body>
            <a href="/docs/start">Start</a>
            <a href="https://other.example.com/x">External</a>
            <a href="/static/app.js">Script</a>
            </body></html>
            """;
        var mapped = HtmlLinkExtractor.Extract(html, "https://example.com/home", maxLinks: 10, sameDomainOnly: true);
        assert("map html keeps in-domain", mapped.Any(link => link.Path == "/docs/start"));
        assert("map html drops external", mapped.All(link => link.Url.Contains("example.com", StringComparison.OrdinalIgnoreCase)));
        assert("map html link count", mapped.Count == 2);
        assert("map html start title", mapped.Any(link => link.Path == "/docs/start" && link.Title == "Start"));
        assert("map html script path", mapped.Any(link => link.Path == "/static/app.js" && link.Title == "Script"));

        assert("map html empty", HtmlLinkExtractor.Extract("", "https://example.com/").Count == 0);
        assert("map html whitespace only", HtmlLinkExtractor.Extract("   \n", "https://example.com/").Count == 0);
        assert("map html invalid base", HtmlLinkExtractor.Extract("<a href='/x'>X</a>", "not-a-url").Count == 0);

        var nginxHtml = """
            <html><body>
            <a href="/en/docs/">Documentation</a>
            <a href="/en/docs/ngx_core_module.html">Core module</a>
            <a href="/en/docs/http/ngx_http_core_module.html">HTTP core</a>
            <a href="javascript:void(0)">Skip</a>
            <a href="https://external.example.org/x">External</a>
            <a href="/en/CHANGES">Changes</a>
            </body></html>
            """;
        var nginxLinks = HtmlLinkExtractor.Extract(
            nginxHtml,
            "https://nginx.org/en/docs/",
            maxLinks: 32,
            sameDomainOnly: true);
        assert("map nginx snippet count", nginxLinks.Count == 4);
        assert(
            "map nginx snippet http core",
            nginxLinks.Any(link =>
                link.Path == "/en/docs/http/ngx_http_core_module.html"
                && link.Title == "HTTP core"));
        assert(
            "map nginx snippet drops js",
            nginxLinks.All(link => !link.Url.Contains("javascript:", StringComparison.OrdinalIgnoreCase)));
        assert(
            "map nginx snippet drops external",
            nginxLinks.All(link => link.Url.Contains("nginx.org", StringComparison.OrdinalIgnoreCase)));

        var robotsFixture = """
            User-agent: *
            Sitemap: https://docs.example.com/sitemap.xml
            Sitemap: https://cdn.example.com/other-sitemap.xml
            """;
        var robotsBase = new Uri("https://docs.example.com/");
        var robotsSitemaps = SitemapDiscovery.ParseRobotsSitemapUrls(robotsFixture, robotsBase);
        assert("map robots sitemap parse count", robotsSitemaps.Count == 2);
        assert("map robots sitemap in-domain", robotsSitemaps[0].Contains("docs.example.com", StringComparison.Ordinal));

        var sitemapFixture = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>https://docs.example.com/core/adaptive-crawling/</loc></url>
              <url><loc>https://docs.example.com/advanced/multi-url-crawling/</loc></url>
              <url><loc>https://docs.example.com/static/app.js</loc></url>
              <url><loc>https://docs.example.com/assets/logo.png</loc></url>
              <url><loc>https://other.example.com/external/</loc></url>
            </urlset>
            """;
        var sitemapLinks = SitemapDiscovery.ParseSitemapXml(sitemapFixture, robotsBase, maxLinks: 20, sameDomainOnly: true);
        assert("map sitemap xml parse count", sitemapLinks.Count == 4);
        assert(
            "map sitemap xml keeps docs path",
            sitemapLinks.Any(link => link.Path.Contains("adaptive-crawling", StringComparison.Ordinal)));
        assert(
            "map filter drops js asset",
            MapLinkFilter.IsNonsense("https://docs.example.com/static/app.js", "/static/app.js"));
        assert(
            "map filter drops png asset",
            MapLinkFilter.IsNonsense("https://docs.example.com/assets/logo.png", "/assets/logo.png"));
        assert(
            "map filter keeps docs page",
            !MapLinkFilter.IsNonsense("https://docs.example.com/core/adaptive-crawling/", "/core/adaptive-crawling/"));
        assert(
            "map filter drops nginx CHANGES",
            MapLinkFilter.IsNonsense("http://nginx.org/en/CHANGES", "/en/CHANGES"));
        assert(
            "map filter drops nginx CHANGES version",
            MapLinkFilter.IsNonsense("http://nginx.org/en/CHANGES-1.24", "/en/CHANGES-1.24"));
        assert(
            "map filter keeps nginx docs module",
            !MapLinkFilter.IsNonsense(
                "http://nginx.org/en/docs/http/ngx_http_core_module.html",
                "/en/docs/http/ngx_http_core_module.html"));
        assert(
            "map filter keeps nginx contributing_changes",
            !MapLinkFilter.IsNonsense(
                "http://nginx.org/en/docs/contributing_changes.html",
                "/en/docs/contributing_changes.html"));
        assert("map soft404 title", MapSoft404Filter.LooksLikeSoft404("/missing/", "404 Not Found"));
        assert(
            "map link ranker prefers query",
            MapLinkRanker.Rank(
                [
                    new MappedLink("https://docs.example.com/privacy/", "Privacy", "/privacy/"),
                    new MappedLink("https://docs.example.com/core/adaptive-crawling/", "Adaptive Crawling", "/core/adaptive-crawling/"),
                ],
                "adaptive crawling",
                1)[0].Path.Contains("adaptive", StringComparison.OrdinalIgnoreCase));
        assert(
            "map link ranker always reorders (count ≤ max)",
            MapLinkRanker.Rank(
                [
                    new MappedLink("https://docs.example.com/v/3.12/", "Release 3.12", "/v/3.12/"),
                    new MappedLink("https://docs.example.com/library/asyncio/", "asyncio", "/library/asyncio/"),
                ],
                "asyncio",
                10)[0].Path.Contains("asyncio", StringComparison.OrdinalIgnoreCase));

        var partialResponse = OccamMapResponseMapper.MapSuccess(new MapAnalysis
        {
            Ok = true,
            Url = "https://docs.example.com/",
            FinalUrl = "https://docs.example.com/",
            Source = "sitemap",
            Links = [new MappedLink("https://docs.example.com/a", null, "/a")],
            LinkCount = 1,
            Partial = true,
        });
        assert("map partial success is explicit", partialResponse.Partial);
        assert("map partial success warns agent", partialResponse.AgentHints.Warnings.Length == 1);
    }
}
