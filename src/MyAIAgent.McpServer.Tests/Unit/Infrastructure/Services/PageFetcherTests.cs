using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MyAIAgent.McpServer.Infrastructure.Services;

namespace MyAIAgent.McpServer.Tests.Unit.Infrastructure.Services;

public sealed class PageFetcherTests
{
    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[::ffff:127.0.0.1]/")]
    [InlineData("https://example.com:8443/")]
    [InlineData("file:///etc/passwd")]
    public async Task GetContentAsync_WhenDestinationIsRestricted_ShouldNotSendRequest(string url)
    {
        var handler = new QueueHandler();
        using var client = new HttpClient(handler);
        var fetcher = new PageFetcher(NullLogger<PageFetcher>.Instance, client);

        var result = await fetcher.GetContentAsync(url);

        result.Success.Should().BeFalse();
        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task GetContentAsync_WhenRedirectTargetsPrivateAddress_ShouldStopBeforeSecondRequest()
    {
        var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
        redirect.Headers.Location = new Uri("https://169.254.169.254/latest/meta-data/");
        var handler = new QueueHandler(redirect);
        using var client = new HttpClient(handler);
        var fetcher = new PageFetcher(NullLogger<PageFetcher>.Instance, client);

        var result = await fetcher.GetContentAsync("https://example.com/page");

        result.Success.Should().BeFalse();
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetContentAsync_WhenPageExceedsLimit_ShouldRejectContent()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('a', 1024 * 1024 + 1), Encoding.UTF8, "text/plain")
        };
        var handler = new QueueHandler(response);
        using var client = new HttpClient(handler);
        var fetcher = new PageFetcher(NullLogger<PageFetcher>.Instance, client);

        var result = await fetcher.GetContentAsync("https://example.com/page");

        result.Success.Should().BeFalse();
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetContentAsync_WhenUnknownLengthBodyExceedsLimit_ShouldRejectContent()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(new byte[1024 * 1024 + 1])
        };
        var handler = new QueueHandler(response);
        using var client = new HttpClient(handler);
        var fetcher = new PageFetcher(NullLogger<PageFetcher>.Instance, client);

        var result = await fetcher.GetContentAsync("https://example.com/page");

        result.Success.Should().BeFalse();
        handler.RequestCount.Should().Be(1);
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.2.3.4", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("192.168.0.1", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("2606:4700:4700::1111", true)]
    public void IsPublicAddress_WhenAddressIsChecked_ShouldApplyNetworkBoundary(
        string text,
        bool expected)
    {
        var address = IPAddress.Parse(text);

        var result = PublicPageAddressPolicy.IsPublicAddress(address);

        result.Should().Be(expected);
    }

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
