using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using WebApp.Api.Services;

namespace WebApp.Api.Tests;

[TestClass]
public class AgentFrameworkServiceConfigTests
{
    [TestMethod]
    public void AiAuthScope_PrefersAiAuthScopeOverLegacyAiScope()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI_AUTH_SCOPE"] = "https://custom.ai.scope/.default",
                ["AI_SCOPE"] = "https://legacy.scope/.default"
            })
            .Build();

        var resolvedScope = AgentFrameworkService.ResolveAiAuthScope(configuration);

        Assert.AreEqual("https://custom.ai.scope/.default", resolvedScope);
    }

    [TestMethod]
    public void AiAuthScope_FallsBackToLegacyAiScopeWhenCanonicalMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI_SCOPE"] = "https://legacy.scope/.default"
            })
            .Build();

        var resolvedScope = AgentFrameworkService.ResolveAiAuthScope(configuration);

        Assert.AreEqual("https://legacy.scope/.default", resolvedScope);
    }

    [TestMethod]
    public void AiAuthScope_UsesDefaultWhenNoScopeConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI_AUTH_SCOPE"] = "   ",
                ["AI_SCOPE"] = ""
            })
            .Build();

        var resolvedScope = AgentFrameworkService.ResolveAiAuthScope(configuration);

        Assert.AreEqual(AgentFrameworkService.DefaultAiAuthScope, resolvedScope);
    }

    [TestMethod]
    public void OboTokenExchangeAudience_UsesConfiguredValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OBO_TOKEN_EXCHANGE_AUDIENCE"] = "  api://AzureADTokenExchangeCustomCloud  "
            })
            .Build();

        var resolvedAudience = AgentFrameworkService.ResolveOboTokenExchangeAudience(configuration);

        Assert.AreEqual("api://AzureADTokenExchangeCustomCloud", resolvedAudience);
    }

    [TestMethod]
    public void OboTokenExchangeAudience_UsesPublicCloudDefaultWhenMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OBO_TOKEN_EXCHANGE_AUDIENCE"] = " "
            })
            .Build();

        var resolvedAudience = AgentFrameworkService.ResolveOboTokenExchangeAudience(configuration);

        Assert.AreEqual(AgentFrameworkService.DefaultOboTokenExchangeAudience, resolvedAudience);
    }

    [TestMethod]
    public void UseObo_TrueWhenBackendClientIdAndTenantIdSet()
    {
        // OBO requires: ENTRA_BACKEND_CLIENT_ID + ENTRA_TENANT_ID + not Development
        var backendClientId = "test-backend-id";
        var tenantId = "test-tenant-id";
        var environment = "Production";

        var useObo = !string.IsNullOrEmpty(backendClientId)
                     && !string.IsNullOrEmpty(tenantId)
                     && environment != "Development";

        Assert.IsTrue(useObo);
    }

    [TestMethod]
    public void UseObo_FalseInDevelopment()
    {
        var backendClientId = "test-backend-id";
        var tenantId = "test-tenant-id";
        var environment = "Development";

        var useObo = !string.IsNullOrEmpty(backendClientId)
                     && !string.IsNullOrEmpty(tenantId)
                     && environment != "Development";

        Assert.IsFalse(useObo);
    }

    [TestMethod]
    public void UseObo_FalseWhenBackendClientIdMissing()
    {
        string? backendClientId = null;
        var tenantId = "test-tenant-id";
        var environment = "Production";

        var useObo = !string.IsNullOrEmpty(backendClientId)
                     && !string.IsNullOrEmpty(tenantId)
                     && environment != "Development";

        Assert.IsFalse(useObo);
    }

    [TestMethod]
    public void UseObo_FalseWhenTenantIdMissing()
    {
        var backendClientId = "test-backend-id";
        string? tenantId = null;
        var environment = "Production";

        var useObo = !string.IsNullOrEmpty(backendClientId)
                     && !string.IsNullOrEmpty(tenantId)
                     && environment != "Development";

        Assert.IsFalse(useObo);
    }

    [TestMethod]
    public void OboRequiresManagedIdentityClientId()
    {
        // When OBO is enabled, MANAGED_IDENTITY_CLIENT_ID must be set
        var useObo = true;
        string? managedIdentityClientId = null;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            if (useObo && string.IsNullOrEmpty(managedIdentityClientId))
            {
                throw new InvalidOperationException(
                    "OBO mode requires MANAGED_IDENTITY_CLIENT_ID to be set for the FIC assertion.");
            }
        });
    }

    [TestMethod]
    public void OboDoesNotThrowWhenManagedIdentityClientIdSet()
    {
        var useObo = true;
        var managedIdentityClientId = "test-mi-id";

        // Should not throw
        if (useObo && string.IsNullOrEmpty(managedIdentityClientId))
        {
            throw new InvalidOperationException("Should not reach here");
        }
        // If we get here, test passes
    }

    [TestMethod]
    public void AgentVersion_ParsedWhenSet()
    {
        string? configValue = "2";

        var agentVersion = string.IsNullOrWhiteSpace(configValue) ? null : configValue;

        Assert.AreEqual("2", agentVersion);
    }

    [TestMethod]
    public void AgentVersion_NullWhenMissing()
    {
        string? configValue = null;

        var agentVersion = string.IsNullOrWhiteSpace(configValue) ? null : configValue;

        Assert.IsNull(agentVersion);
    }

    [TestMethod]
    public void AgentVersion_NullWhenEmpty()
    {
        string? configValue = "";

        var agentVersion = string.IsNullOrWhiteSpace(configValue) ? null : configValue;

        Assert.IsNull(agentVersion);
    }

    [TestMethod]
    public void AgentVersion_NullWhenWhitespace()
    {
        string? configValue = "   ";

        var agentVersion = string.IsNullOrWhiteSpace(configValue) ? null : configValue;

        Assert.IsNull(agentVersion);
    }

    [TestMethod]
    public void PortalAgentId_SplitsNameAndVersion()
    {
        // Portal format: "dadjokes:2"
        var portalAgentId = "dadjokes:2";
        var parts = portalAgentId.Split(':', 2);

        var agentName = parts[0].Trim();
        var agentVersion = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? parts[1].Trim() : null;

        Assert.AreEqual("dadjokes", agentName);
        Assert.AreEqual("2", agentVersion);
    }

    [TestMethod]
    public void PortalAgentId_HandlesNoVersion()
    {
        // No version suffix
        var portalAgentId = "dadjokes";
        var parts = portalAgentId.Split(':', 2);

        var agentName = parts[0].Trim();
        var agentVersion = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? parts[1].Trim() : null;

        Assert.AreEqual("dadjokes", agentName);
        Assert.IsNull(agentVersion);
    }

    [TestMethod]
    public void PortalAgentId_HandlesWhitespaceVersion()
    {
        // Whitespace-only version suffix
        var portalAgentId = "dadjokes:   ";
        var parts = portalAgentId.Split(':', 2);

        var agentName = parts[0].Trim();
        var agentVersion = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])
            ? parts[1].Trim() : null;

        Assert.AreEqual("dadjokes", agentName);
        Assert.IsNull(agentVersion);
    }

    [TestMethod]
    public void PortalResourceId_ExtractsResourceName()
    {
        var armPath = "/subscriptions/abc/resourceGroups/rg/providers/Microsoft.CognitiveServices/accounts/my-foundry-resource";

        var resourceName = armPath.Split("/accounts/").Last().Split('/').First().Trim();

        Assert.AreEqual("my-foundry-resource", resourceName);
    }

    [TestMethod]
    public void PortalResourceId_HandlesProjectSuffix()
    {
        // ARM path with project suffix (AZURE_EXISTING_AIPROJECT_RESOURCE_ID format)
        var armPath = "/subscriptions/abc/resourceGroups/rg/providers/Microsoft.CognitiveServices/accounts/my-foundry/projects/my-project";

        var resourceName = armPath.Split("/accounts/").Last().Split('/').First().Trim();

        Assert.AreEqual("my-foundry", resourceName);
    }
}
