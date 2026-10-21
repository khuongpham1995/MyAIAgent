/**
 * MyAIAgent | McpServer · Tests
 *
 * Preserves public-address boundary coverage used by outbound search connections.
 */

using System.Net;
using FluentAssertions;
using MyAIAgent.Common.Infrastructure.Http;

namespace MyAIAgent.McpServer.Tests.Unit.Infrastructure.Services;

public sealed class OutboundAddressPolicyTests
{
    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("168.63.129.16", false)]
    [InlineData("192.88.99.1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2002:a00:1::1", false)]
    [InlineData("3fff::1", false)]
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
        // Arrange
        var address = IPAddress.Parse(text);

        // Act
        var result = OutboundAddressPolicy.IsPublicAddress(address);

        // Assert
        result.Should().Be(expected);
    }

}
