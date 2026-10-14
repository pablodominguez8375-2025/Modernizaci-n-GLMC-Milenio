using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PMGM.Api.Infrastructure;
using PMGM.Api.Modules.Audit;
using Xunit;

namespace PMGM.Api.Tests.Audit;

public sealed class TrustedProxyForwardingTests
{
    [Theory]
    [InlineData("10.40.0.11", "10.40.0.10", "198.51.100.37, 10.40.0.10", "198.51.100.37")]
    [InlineData("10.40.0.11", "10.40.0.10", "203.0.113.88, 198.51.100.37, 10.40.0.10", "198.51.100.37")]
    public async Task Two_explicit_trusted_hops_produce_real_client_audit_ip(
        string nginx, string caddy, string forwarded, string expected)
    {
        var actual = await AuditIpAsync(nginx + "," + caddy, nginx, forwarded);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task IPv4_mapped_socket_address_is_still_the_same_exact_trusted_proxy()
    {
        var actual = await AuditIpAsync(
            "10.40.0.11,10.40.0.10",
            "::ffff:10.40.0.11",
            "198.51.100.37, 10.40.0.10");
        Assert.Equal("198.51.100.37", actual);
    }

    [Fact]
    public async Task Unknown_peer_cannot_spoof_audit_ip_or_proxy_headers()
    {
        var actual = await AuditIpAsync(
            "10.40.0.11,10.40.0.10",
            "10.40.0.99",
            "198.51.100.77, 10.40.0.10");
        Assert.Equal("10.40.0.99", actual);
    }

    [Fact]
    public async Task No_trusted_proxies_means_forwarded_headers_are_ignored_even_for_loopback()
    {
        var actual = await AuditIpAsync(null, "127.0.0.1", "198.51.100.77");
        Assert.Equal("127.0.0.1", actual);
    }

    [Fact]
    public async Task Single_explicit_proxy_supports_first_implementation_without_caddy()
    {
        var actual = await AuditIpAsync("10.40.0.11", "10.40.0.11", "198.51.100.44");
        Assert.Equal("198.51.100.44", actual);
    }

    [Fact]
    public async Task Unknown_second_hop_stops_trust_chain_instead_of_consuming_another_header()
    {
        var actual = await AuditIpAsync(
            "10.40.0.11,10.40.0.10",
            "10.40.0.11",
            "198.51.100.37, 10.40.0.88");
        Assert.Equal("10.40.0.88", actual);
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("0.0.0.0")]
    [InlineData("127.0.0.1")]
    [InlineData("::")]
    [InlineData("10.40.0.11/24")]
    [InlineData("10.40.0.11,10.40.0.11")]
    [InlineData("10.40.0.11,::ffff:10.40.0.11")]
    [InlineData("10.40.0.11,")]
    [InlineData("10.40.0.11,10.40.0.10,10.40.0.12")]
    public void Invalid_or_overly_broad_proxy_config_fails_closed_at_startup(string configured)
    {
        var options = new ForwardedHeadersOptions();
        Assert.Throws<InvalidOperationException>(() =>
            TrustedProxyForwarding.Configure(options, Configuration(configured)));
    }

    [Fact]
    public void Only_client_ip_is_consumed_and_not_host_or_scheme()
    {
        var options = new ForwardedHeadersOptions();
        TrustedProxyForwarding.Configure(options, Configuration("10.40.0.11,10.40.0.10"));
        Assert.Equal(ForwardedHeaders.XForwardedFor, options.ForwardedHeaders);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Equal(4, options.KnownProxies.Count);
        Assert.Equal(2, options.ForwardLimit);
    }

    private static IConfiguration Configuration(string? known)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:KnownProxies"] = known
        }).Build();

    private static async Task<string?> AuditIpAsync(string? configured, string remote, string forwarded)
    {
        var options = new ForwardedHeadersOptions();
        TrustedProxyForwarding.Configure(options, Configuration(configured));

        string? result = null;
        var middleware = new ForwardedHeadersMiddleware(context =>
        {
            var audit = AuditEventFactory.Create(
                context, "security.proxy.test", "TrustedProxy", "1", null, AuditResults.Success);
            result = audit.IpAddress;
            return Task.CompletedTask;
        }, NullLoggerFactory.Instance, Options.Create(options));

        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        // DefaultHttpContext aislado no define esquema inicial; conservarlo prueba
        // que solo se procesa X-Forwarded-For, no X-Forwarded-Proto.
        http.Request.Scheme = "http";
        http.Request.Headers["X-Forwarded-For"] = forwarded;
        http.Request.Headers["X-Forwarded-Host"] = "fake-client.example";
        http.Request.Headers["X-Forwarded-Proto"] = "https";
        await middleware.Invoke(http);
        Assert.Equal("http", http.Request.Scheme);
        Assert.NotEqual("fake-client.example", http.Request.Host.Host);
        return result;
    }
}
