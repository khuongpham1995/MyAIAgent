using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MyAIAgent.Gateway.Presentation.PublicApi;

// JwtBearer only knows OIDC discovery, so a bare JWKS endpoint is adapted into the shape it expects.
internal sealed class JwksConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
{
    private readonly ConfigurationManager<JsonWebKeySet> keySets;
    private readonly string issuer;

    public JwksConfigurationManager(string jwksUri, string issuer, bool requireHttps)
    {
        this.issuer = issuer;
        keySets = new ConfigurationManager<JsonWebKeySet>(
            jwksUri,
            new JwksRetriever(),
            new HttpDocumentRetriever { RequireHttps = requireHttps });
    }

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        var keySet = await keySets.GetConfigurationAsync(cancel);
        var configuration = new OpenIdConnectConfiguration { Issuer = issuer };
        foreach (var key in keySet.GetSigningKeys())
            configuration.SigningKeys.Add(key);

        return configuration;
    }

    public void RequestRefresh() => keySets.RequestRefresh();

    private sealed class JwksRetriever : IConfigurationRetriever<JsonWebKeySet>
    {
        public async Task<JsonWebKeySet> GetConfigurationAsync(
            string address,
            IDocumentRetriever retriever,
            CancellationToken cancel) =>
            new(await retriever.GetDocumentAsync(address, cancel));
    }
}
