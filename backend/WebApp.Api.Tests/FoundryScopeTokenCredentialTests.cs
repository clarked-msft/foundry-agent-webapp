using Azure.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ClientModel.Primitives;
using WebApp.Api.Services;

namespace WebApp.Api.Tests;

[TestClass]
public class FoundryScopeTokenCredentialTests
{
    [TestMethod]
    public void GetToken_UsesConfiguredScopeForTokenRequestContext()
    {
        var innerCredential = new RecordingTokenCredential();
        var credential = new FoundryScopeTokenCredential(innerCredential, "https://configured.scope/.default");
        var requestContext = new TokenRequestContext(
            scopes: ["https://original.scope/.default"],
            parentRequestId: "parent-request-id",
            claims: "claims",
            tenantId: "tenant-id",
            isCaeEnabled: true,
            isProofOfPossessionEnabled: true,
            proofOfPossessionNonce: "nonce",
            requestUri: new Uri("https://example.test"),
            requestMethod: "GET");

        credential.GetToken(requestContext, CancellationToken.None);

        Assert.IsTrue(innerCredential.HasTokenRequestContext);
        var capturedContext = innerCredential.LastTokenRequestContext;
        CollectionAssert.AreEqual(
            new[] { "https://configured.scope/.default" },
            capturedContext.Scopes);
        Assert.AreEqual("tenant-id", capturedContext.TenantId);
        Assert.AreEqual("GET", capturedContext.ResourceRequestMethod);
        Assert.AreEqual(new Uri("https://example.test"), capturedContext.ResourceRequestUri);
    }

    [TestMethod]
    public void GetToken_UsesConfiguredScopeForGetTokenOptions()
    {
        var innerCredential = new RecordingTokenCredential();
        var credential = new FoundryScopeTokenCredential(innerCredential, "https://configured.scope/.default");
        var options = new GetTokenOptions(new Dictionary<string, object>
        {
            [GetTokenOptions.ScopesPropertyName] = new[] { "https://original.scope/.default" },
            ["customProperty"] = "customValue"
        });

        credential.GetToken(options, CancellationToken.None);

        Assert.IsNotNull(innerCredential.LastGetTokenOptions);
        var capturedOptions = innerCredential.LastGetTokenOptions!;
        Assert.IsTrue(capturedOptions.Properties.TryGetValue("customProperty", out var customProperty));
        Assert.AreEqual("customValue", customProperty);
        var scopes = capturedOptions.Properties[GetTokenOptions.ScopesPropertyName] as string[];
        CollectionAssert.AreEqual(new[] { "https://configured.scope/.default" }, scopes);
    }

    private sealed class RecordingTokenCredential : TokenCredential
    {
        public bool HasTokenRequestContext { get; private set; }
        public TokenRequestContext LastTokenRequestContext { get; private set; }
        public GetTokenOptions? LastGetTokenOptions { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            HasTokenRequestContext = true;
            LastTokenRequestContext = requestContext;
            return new AccessToken("token", DateTimeOffset.UtcNow.AddMinutes(30));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            HasTokenRequestContext = true;
            LastTokenRequestContext = requestContext;
            return ValueTask.FromResult(new AccessToken("token", DateTimeOffset.UtcNow.AddMinutes(30)));
        }

        public override AuthenticationToken GetToken(GetTokenOptions options, CancellationToken cancellationToken)
        {
            LastGetTokenOptions = options;
            return new AuthenticationToken("Bearer", "token", DateTimeOffset.UtcNow.AddMinutes(30), refreshOn: null);
        }

        public override ValueTask<AuthenticationToken> GetTokenAsync(GetTokenOptions options, CancellationToken cancellationToken)
        {
            LastGetTokenOptions = options;
            return ValueTask.FromResult(new AuthenticationToken("Bearer", "token", DateTimeOffset.UtcNow.AddMinutes(30), refreshOn: null));
        }
    }
}
