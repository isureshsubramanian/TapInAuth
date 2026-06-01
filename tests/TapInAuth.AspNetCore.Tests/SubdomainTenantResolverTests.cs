using FluentAssertions;
using Microsoft.AspNetCore.Http;
using TapInAuth.AspNetCore.Tenancy;
using Xunit;

namespace TapInAuth.AspNetCore.Tests;

public class SubdomainTenantResolverTests
{
    private static SubdomainTenantResolver CreateResolver(string host)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString(host);
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        return new SubdomainTenantResolver(accessor);
    }

    [Fact]
    public async Task Extracts_slug_from_first_subdomain()
    {
        var r = CreateResolver("acme.example.com");
        var t = await r.ResolveAsync();
        t!.Id.Should().Be("acme");
    }

    [Theory]
    [InlineData("localhost", "default")]
    [InlineData("127.0.0.1",  "default")]
    [InlineData("example.com",  "default")]   // only 2 labels → default
    [InlineData("www.example.com", "default")]
    [InlineData("app.example.com", "default")]
    public async Task Special_hosts_fall_back_to_default(string host, string expected)
    {
        var r = CreateResolver(host);
        var t = await r.ResolveAsync();
        t!.Id.Should().Be(expected);
    }

    [Fact]
    public async Task Slug_is_lowercased()
    {
        var r = CreateResolver("ACME.example.com");
        var t = await r.ResolveAsync();
        t!.Id.Should().Be("acme");
    }
}
