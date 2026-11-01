/**
 * MyAIAgent | Gateway · Tests
 *
 * Verifies shared outbound origin, DNS, credential, protocol, and private-address boundaries without external network access.
 */

using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MyAIAgent.Common.Infrastructure.Http;

namespace MyAIAgent.Gateway.Tests.Unit.Infrastructure;

public sealed class OutboundDestinationPolicyTests
{
    [Fact]
    public void Settings_WhenNoExternalOriginsAreConfigured_ShouldDenyExternalEndpoints()
    {
        // Arrange
        var settings = Settings(external: null);

        // Act & Assert
        settings.IsAllowedEndpoint("https://external.example/mcp").Should().BeFalse();
        settings.IsAllowedEndpoint("https://mcp.example/mcp").Should().BeTrue();
    }

    [Fact]
    public void Settings_WhenJsonContainsEmptyAllowlist_ShouldStartWithExternalAccessDenied()
    {
        // Arrange
        using var json = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
            { "McpServer": { "BaseUrl": "https://mcp.example", "AccessKey": "test-key", "AllowedExternalOrigins": [] } }
            """));
        var configuration = new ConfigurationBuilder().AddJsonStream(json).Build();

        // Act
        var settings = McpServiceClientSettings.Load(configuration, false);

        // Assert
        settings.IsAllowedEndpoint("https://external.example/mcp").Should().BeFalse();
        settings.IsAllowedEndpoint("https://mcp.example/mcp").Should().BeTrue();
    }

    private static McpServiceClientSettings Settings(string origin = "https://mcp.example", string? external = "https://external.example:8443", bool development = false) =>
        McpServiceClientSettings.Load(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["McpServer:BaseUrl"] = origin,
            ["McpServer:AccessKey"] = "test-key",
            ["McpServer:AllowedExternalOrigins:0"] = external
        }.Where(pair => pair.Value is not null)).Build(), development);

    [Theory]
    [InlineData("http://external.example")]
    [InlineData("https://external.example/path")]
    [InlineData("https://external.example?query=value")]
    [InlineData("https://external.example#fragment")]
    [InlineData("https://user:password@external.example")]
    [InlineData("https://*.example")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://10.0.0.1")]
    [InlineData("https://168.63.129.16")]
    [InlineData("https://mcp.example")]
    [InlineData("https://[2606:4700:4700::1111%251]")]
    public void Settings_WhenExternalOriginIsInvalid_ShouldFailWithoutIncludingValue(string external)
    {
        // Arrange & Act
        var action = () => Settings(external: external);

        // Assert
        action.Should().Throw<InvalidOperationException>().WithMessage("McpServer:AllowedExternalOrigins*")
            .Which.Message.Should().NotContain(external);
    }

    [Theory]
    [InlineData("https://EXTERNAL.example:8443/sse?session=1", true)]
    [InlineData("https://external.example/mcp", false)]
    [InlineData("https://external.example.evil.test:8443/mcp", false)]
    [InlineData("http://external.example:8443/mcp", false)]
    [InlineData("https://user@external.example:8443/mcp", false)]
    [InlineData("https://external.example:8443/mcp#fragment", false)]
    [InlineData("https://mcp.example/mcp", false)]
    public void IsAllowedExternalEndpoint_WhenOriginChanges_ShouldRequireExactApproval(string endpoint, bool expected)
    {
        // Arrange
        var settings = Settings();

        // Act
        var allowed = settings.IsAllowedExternalEndpoint(new Uri(endpoint));

        // Assert
        allowed.Should().Be(expected);
    }

    [Theory]
    [InlineData("10.0.0.1", true, true)]
    [InlineData("fd00::1", true, true)]
    [InlineData("127.0.0.1", false, true)]
    [InlineData("::1", false, true)]
    [InlineData("169.254.169.254", false, false)]
    [InlineData("168.63.129.16", false, false)]
    [InlineData("100.64.0.1", false, false)]
    [InlineData("fd00:ec2::254", false, false)]
    [InlineData("fd20:ce::254", false, false)]
    public void IsAllowedServiceAddress_WhenPrivateOrSpecial_ShouldKeepExceptionNarrow(string address, bool production, bool development)
    {
        // Arrange
        var ip = IPAddress.Parse(address);

        // Act & Assert
        Settings().IsAllowedServiceAddress(ip).Should().Be(production);
        Settings("http://localhost:5255", development: true).IsAllowedServiceAddress(ip).Should().Be(development);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("127.1")]
    [InlineData("0x7f000001")]
    [InlineData("2130706433")]
    [InlineData("192.0.0.9")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("2001::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:7f00:1::1")]
    [InlineData("3fff::1")]
    [InlineData("2606:4700:4700::1111%1")]
    public void IsPublicAddress_WhenAddressHasSpecialMeaning_ShouldRejectIt(string address)
    {
        // Arrange
        var ip = IPAddress.Parse(address);

        // Act & Assert
        OutboundAddressPolicy.IsPublicAddress(ip).Should().BeFalse();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void IsPublicAddress_WhenPublic_ShouldAllowIt(string address)
    {
        // Arrange & Act
        var result = OutboundAddressPolicy.IsPublicAddress(IPAddress.Parse(address));

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConnectAsync_WhenDnsIsEmptyOrMixed_ShouldRejectBeforeConnect(bool mixed)
    {
        // Arrange
        var connections = 0;
        var policy = new OutboundConnectionPolicy(
            (_, _) => Task.FromResult(mixed ? new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Loopback } : []),
            (_, _, _) => { connections++; return ValueTask.FromResult<Stream>(new MemoryStream()); });
        var context = new DnsEndPoint("external.example", 443);

        // Act
        var action = async () => await policy.ConnectAsync(context, OutboundAddressPolicy.IsPublicAddress, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<OutboundDestinationBlockedException>();
        connections.Should().Be(0);
    }

    [Fact]
    public async Task ConnectAsync_WhenDnsChanges_ShouldPinFirstConnectionAndRecheckNext()
    {
        // Arrange
        var resolutions = 0;
        var connections = 0;
        var publicAnswer = new[] { IPAddress.Parse("8.8.8.8") };
        var policy = new OutboundConnectionPolicy(
            (_, _) => Task.FromResult(++resolutions == 1 ? publicAnswer : [IPAddress.Loopback]),
            (addresses, port, _) =>
            {
                addresses.Should().BeSameAs(publicAnswer);
                port.Should().Be(443);
                connections++;
                return ValueTask.FromResult<Stream>(new MemoryStream());
            });
        var context = new DnsEndPoint("external.example", 443);

        // Act
        using var first = await policy.ConnectAsync(context, OutboundAddressPolicy.IsPublicAddress, CancellationToken.None);
        var second = async () => await policy.ConnectAsync(context, OutboundAddressPolicy.IsPublicAddress, CancellationToken.None);

        // Assert
        await second.Should().ThrowAsync<OutboundDestinationBlockedException>();
        resolutions.Should().Be(2);
        connections.Should().Be(1);
    }

    [Fact]
    public async Task ConnectAsync_WhenCancelledDuringResolution_ShouldPropagateWithoutConnecting()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        var connections = 0;
        var policy = new OutboundConnectionPolicy(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return [];
        }, (_, _, _) => { connections++; return ValueTask.FromResult<Stream>(new MemoryStream()); });
        var context = new DnsEndPoint("external.example", 443);

        // Act
        var pending = policy.ConnectAsync(context, OutboundAddressPolicy.IsPublicAddress, cancellation.Token).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        // Assert
        await ((Func<Task>)(() => pending)).Should().ThrowAsync<OperationCanceledException>();
        connections.Should().Be(0);
    }

    [Fact]
    public async Task ConnectAsync_WhenConnectedStreamIsDisposed_ShouldCloseOwnedSocket()
    {
        // Arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var policy = new OutboundConnectionPolicy((_, _) => Task.FromResult(new[] { IPAddress.Loopback }));

            // Act
            var stream = await policy.ConnectAsync(new DnsEndPoint("test-only.example", port), IPAddress.IsLoopback, cancellation.Token);
            using var accepted = await listener.AcceptTcpClientAsync(cancellation.Token);
            await stream.DisposeAsync();
            var read = await accepted.GetStream().ReadAsync(new byte[1], cancellation.Token);

            // Assert
            read.Should().Be(0);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task ConnectAsync_WhenConnectFails_ShouldPropagateAndResolveOnlyOnce()
    {
        // Arrange
        var resolutions = 0;
        var policy = new OutboundConnectionPolicy(
            (_, _) => { resolutions++; return Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }); },
            (_, _, _) => throw new SocketException((int)SocketError.ConnectionRefused));

        // Act
        var action = async () => await policy.ConnectAsync(new DnsEndPoint("external.example", 443), OutboundAddressPolicy.IsPublicAddress, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<SocketException>();
        resolutions.Should().Be(1);
    }

    [Fact]
    public async Task ConnectAsync_WhenConnectIsCancelled_ShouldPropagateToken()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        var policy = new OutboundConnectionPolicy((_, _) => Task.FromResult(new[] { IPAddress.Parse("8.8.8.8") }),
            async (_, _, token) => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); return new MemoryStream(); });

        // Act
        var pending = policy.ConnectAsync(new DnsEndPoint("external.example", 443), OutboundAddressPolicy.IsPublicAddress, cancellation.Token).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        // Assert
        await ((Func<Task>)(() => pending)).Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("https://external.example:8443/messages?session=1", true)]
    [InlineData("https://mcp.example/mcp", false)]
    [InlineData("http://169.254.169.254/", false)]
    [InlineData("https://unapproved.example/mcp", false)]
    public async Task SendAsync_WhenProtocolChoosesAnotherDestination_ShouldApplyAnonymousPolicy(string endpoint, bool allowed)
    {
        // Arrange
        var sends = 0;
        using var inner = new DelegateHandler(request =>
        {
            sends++;
            request.Headers.Authorization.Should().BeNull();
            request.Version.Should().Be(HttpVersion.Version11);
            request.VersionPolicy.Should().Be(HttpVersionPolicy.RequestVersionExact);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var guard = new OutboundRequestHandler(Settings().IsAllowedExternalEndpoint, anonymous: true) { InnerHandler = inner };
        using var client = new HttpClient(guard);
        client.DefaultRequestHeaders.Add("Authorization", "must-not-leak");

        // Act
        var action = () => client.GetAsync(endpoint);

        // Assert
        if (allowed) using (await action()) { }
        else await action.Should().ThrowAsync<OutboundDestinationBlockedException>();
        sends.Should().Be(allowed ? 1 : 0);
    }

    [Fact]
    public async Task SendAsync_WhenConnectionFailureWrapsDenial_ShouldReturnSafePolicyFailure()
    {
        // Arrange
        using var inner = new DelegateHandler(_ => throw new HttpRequestException("private DNS details", new OutboundDestinationBlockedException("dns-address")));
        using var guard = new OutboundRequestHandler(_ => true) { InnerHandler = inner };
        using var client = new HttpClient(guard);

        // Act
        var action = () => client.GetAsync("https://external.example/");

        // Assert
        var failure = (await action.Should().ThrowAsync<OutboundDestinationBlockedException>()).Which;
        failure.InnerException.Should().BeNull();
        failure.Message.Should().NotContain("private DNS details");
    }

    [Fact]
    public async Task SendAsync_WhenExternalHostIsOverridden_ShouldRejectBeforeNetwork()
    {
        // Arrange
        var sends = 0;
        using var inner = new DelegateHandler(_ => { sends++; return new HttpResponseMessage(HttpStatusCode.OK); });
        using var guard = new OutboundRequestHandler(Settings().IsAllowedExternalEndpoint, anonymous: true) { InnerHandler = inner };
        using var client = new HttpClient(guard);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://external.example:8443/mcp");
        request.Headers.Host = "internal.example";

        // Act
        var action = () => client.SendAsync(request);

        // Assert
        await action.Should().ThrowAsync<OutboundDestinationBlockedException>();
        sends.Should().Be(0);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
    }
}
