using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PickleBall.Tests.Infrastructure;

namespace PickleBall.Tests;

public class PlatformSecurityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public PlatformSecurityTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Responses_carry_security_headers_and_are_not_cacheable()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.False(response.Headers.Contains("Server"));
        Assert.False(response.Headers.Contains("X-Powered-By"));
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    public async Task Swagger_is_not_exposed_in_production(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cors_only_allows_the_configured_frontend_origin()
    {
        var client = _factory.CreateClient();

        var evil = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        evil.Headers.Add("Origin", "https://evil.example");
        evil.Headers.Add("Access-Control-Request-Method", "POST");
        var evilResponse = await client.SendAsync(evil);
        Assert.False(evilResponse.Headers.Contains("Access-Control-Allow-Origin"));

        var good = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        good.Headers.Add("Origin", "https://pickleball-shop-frontend.vercel.app");
        good.Headers.Add("Access-Control-Request-Method", "POST");
        good.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        var goodResponse = await client.SendAsync(good);
        Assert.Equal("https://pickleball-shop-frontend.vercel.app",
            goodResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.NotEqual("*", goodResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        // "null" origin (sandboxed iframes, file://) must not be reflected
        var nullOrigin = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        nullOrigin.Headers.Add("Origin", "null");
        nullOrigin.Headers.Add("Access-Control-Request-Method", "POST");
        Assert.False((await client.SendAsync(nullOrigin)).Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Unhandled_exceptions_do_not_leak_messages_or_stack_traces()
    {
        var response = await _factory.CreateClient().GetAsync("/api/__test/boom");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("hunter2", body);
        Assert.DoesNotContain("Npgsql", body);
        Assert.DoesNotContain("Host=", body);
        Assert.DoesNotContain("   at ", body);
        using var doc = JsonDocument.Parse(body);
        Assert.False(doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null);
        Assert.True(doc.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Framework_exception_messages_are_not_echoed_to_clients()
    {
        var response = await _factory.CreateClient().GetAsync("/api/__test/boom-framework");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Sequence contains no elements", body);
    }

    [Fact]
    public async Task Malformed_json_returns_a_clean_400()
    {
        var response = await _factory.CreateClient().PostAsync("/api/auth/login",
            new StringContent("{\"email\": ", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("System.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Chat_rejects_oversized_messages_and_does_not_reveal_configuration()
    {
        var client = _factory.CreateClient();

        var tooLong = await client.PostAsJsonAsync("/api/chat/message", new { message = new string('a', 1001) }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        var hugeHistory = await client.PostAsJsonAsync("/api/chat/message", new
        {
            message = "hi",
            history = Enumerable.Range(0, 200).Select(_ => new { role = "user", content = "x" })
        }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, hugeHistory.StatusCode);

        // Without a configured key the endpoint fails gracefully and without internals
        var unavailable = await client.PostAsJsonAsync("/api/chat/message", new { message = "hello" }, TestHelpers.Json);
        var body = await unavailable.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ApiKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API key", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unsupported_http_methods_are_rejected()
    {
        var client = _factory.CreateClient();
        var trace = await client.SendAsync(new HttpRequestMessage(HttpMethod.Trace, "/api/health"));
        Assert.True(trace.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound or HttpStatusCode.BadRequest);

        var post = await client.PostAsync("/api/health", null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }
}

public class RateLimitTests : IDisposable
{
    private readonly ApiFactory _factory = new(rateLimiting: true);

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Forgot_password_is_rate_limited_per_client()
    {
        var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 8; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/forgot-password",
                new { email = $"flood{i}@example.test" }, TestHelpers.Json);
            statuses.Add(last.StatusCode);
        }

        Assert.Equal(5, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
        Assert.True(last!.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Login_is_rate_limited_against_password_guessing()
    {
        var client = _factory.CreateClient();
        var results = new List<HttpStatusCode>();
        for (var i = 0; i < 14; i++)
        {
            var r = await client.PostAsJsonAsync("/api/auth/login",
                new { email = "victim@example.test", password = "Guess" + i }, TestHelpers.Json);
            results.Add(r.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, results);
        Assert.Equal(10, results.Count(s => s == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Spoofed_forwarded_for_header_does_not_bypass_the_limit()
    {
        var client = _factory.CreateClient();
        var limited = false;
        for (var i = 0; i < 10 && !limited; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
            {
                Content = JsonContent.Create(new { email = "x" }, options: TestHelpers.Json)
            };
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{i}");
            limited = (await client.SendAsync(request)).StatusCode == HttpStatusCode.TooManyRequests;
        }
        Assert.True(limited);
    }
}