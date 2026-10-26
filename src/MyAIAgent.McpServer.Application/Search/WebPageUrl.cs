/**
 * MyAIAgent | McpServer · Application
 *
 * Defines the reader's public HTTPS URL syntax and removes client-side fragments before extraction.
 */

namespace MyAIAgent.McpServer.Application.Search;

public static class WebPageUrl
{
    private static readonly string[] sourceArray = ["localhost", "local", "internal", "home", "lan", "test", "invalid"];

    public static bool TryNormalize(string? value, out Uri? url)
    {
        url = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.Any(char.IsWhiteSpace)
            || value.Any(char.IsControl) || value.Contains('\\')
            || !Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            || parsed.Scheme != "https" || !parsed.IsDefaultPort || parsed.UserInfo.Length != 0
            || parsed.Query.Length != 0 || parsed.HostNameType != UriHostNameType.Dns)
            return false;

        var host = parsed.IdnHost.TrimEnd('.');
        if (!host.Contains('.') || host.Split('.').Any(part => part.Length == 0)
            || sourceArray.Any(suffix => host.Equals(suffix, StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith('.' + suffix, StringComparison.OrdinalIgnoreCase)))
            return false;

        url = new UriBuilder(parsed) { Fragment = string.Empty }.Uri;
        return true;
    }
}
