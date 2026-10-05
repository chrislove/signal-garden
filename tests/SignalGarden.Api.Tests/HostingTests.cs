using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SignalGarden.Api.Tests;

/// <summary>
/// The container serves the Angular build and the API from one origin. These pin
/// down the routing that once sent main.js back as index.html (a blank page).
/// </summary>
public class HostingTests : IClassFixture<HostingTests.SiteFactory>
{
    public sealed class SiteFactory : WebApplicationFactory<Program>
    {
        public string WebRoot { get; } = Directory.CreateTempSubdirectory("sg-wwwroot").FullName;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            File.WriteAllText(Path.Combine(WebRoot, "index.html"), "<html>dashboard</html>");
            File.WriteAllText(Path.Combine(WebRoot, "main-ABC123.js"), "console.log('app');");
            builder.UseWebRoot(WebRoot);
            builder.UseSetting("Api:Adx:QueryUri", ""); // no cluster in tests
        }
    }

    private readonly HttpClient _client;

    public HostingTests(SiteFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Script_files_are_served_as_javascript_not_as_the_app_shell()
    {
        var response = await _client.GetAsync("/main-ABC123.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/events/some-client-route")]
    public async Task Pages_and_client_routes_get_the_app_shell(string path)
    {
        var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("dashboard", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_api_paths_are_404_not_the_app_shell()
    {
        var response = await _client.GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Data_endpoints_say_when_adx_is_not_configured()
    {
        var response = await _client.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
