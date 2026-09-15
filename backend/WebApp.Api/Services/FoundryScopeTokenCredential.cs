using Azure.Core;
using System.ClientModel.Primitives;

namespace WebApp.Api.Services;

/// <summary>
/// Wraps a credential and enforces a single configured Foundry auth scope for all token requests.
/// </summary>
internal sealed class FoundryScopeTokenCredential : TokenCredential
{
    private readonly TokenCredential _innerCredential;
    private readonly string[] _scope;

    public FoundryScopeTokenCredential(TokenCredential innerCredential, string scope)
    {
        _innerCredential = innerCredential ?? throw new ArgumentNullException(nameof(innerCredential));

        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Auth scope must be configured.", nameof(scope));
        }

        _scope = [scope];
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return _innerCredential.GetToken(CreateScopedContext(requestContext), cancellationToken);
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return _innerCredential.GetTokenAsync(CreateScopedContext(requestContext), cancellationToken);
    }

    public override AuthenticationToken GetToken(GetTokenOptions options, CancellationToken cancellationToken)
    {
        return _innerCredential.GetToken(CreateScopedOptions(options), cancellationToken);
    }

    public override ValueTask<AuthenticationToken> GetTokenAsync(GetTokenOptions options, CancellationToken cancellationToken)
    {
        return _innerCredential.GetTokenAsync(CreateScopedOptions(options), cancellationToken);
    }

    private TokenRequestContext CreateScopedContext(TokenRequestContext requestContext)
    {
        return new TokenRequestContext(
            _scope,
            requestContext.ParentRequestId,
            requestContext.Claims,
            requestContext.TenantId,
            requestContext.IsCaeEnabled,
            requestContext.IsProofOfPossessionEnabled,
            requestContext.ProofOfPossessionNonce,
            requestContext.ResourceRequestUri,
            requestContext.ResourceRequestMethod);
    }

    private GetTokenOptions CreateScopedOptions(GetTokenOptions options)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var property in options.Properties)
        {
            properties[property.Key] = property.Value;
        }

        properties[GetTokenOptions.ScopesPropertyName] = _scope;
        return new GetTokenOptions(properties);
    }
}
