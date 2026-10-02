using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using WebApp.Api.Services;

namespace WebApp.Api.Tests;

[TestClass]
public class ConversationUserFilterTests
{
    private static readonly IDictionary<string, string> OwnedMetadata = new Dictionary<string, string>
    {
        [ConversationUserFilter.OwnerTenantMetadataKey] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        [ConversationUserFilter.OwnerMetadataKey] = "11111111-1111-1111-1111-111111111111"
    };

    [TestMethod]
    public void IsEnabled_DefaultsToFalse()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.IsFalse(ConversationUserFilter.IsEnabled(configuration));
    }

    [TestMethod]
    public void IsEnabled_EnablesWhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ENABLE_CONVERSATION_USER_FILTERING"] = "true"
            })
            .Build();

        Assert.IsTrue(ConversationUserFilter.IsEnabled(configuration));
    }

    [TestMethod]
    public void CanAccessConversation_WhenFilteringDisabled_PreservesExistingBehavior()
    {
        Assert.IsTrue(ConversationUserFilter.CanAccessConversation(null, null, filteringEnabled: false));
        Assert.IsTrue(ConversationUserFilter.CanAccessConversation(OwnedMetadata, new EntraUserIdentity(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "22222222-2222-2222-2222-222222222222"), filteringEnabled: false));
    }

    [TestMethod]
    public void CanAccessConversation_WhenUserOwnsConversation_AllowsAccess()
    {
        Assert.IsTrue(ConversationUserFilter.CanAccessConversation(OwnedMetadata, new EntraUserIdentity(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "11111111-1111-1111-1111-111111111111"), filteringEnabled: true));
    }

    [TestMethod]
    public void CanAccessConversation_WhenAnotherUserOwnsConversation_DeniesAccess()
    {
        Assert.IsFalse(ConversationUserFilter.CanAccessConversation(OwnedMetadata, new EntraUserIdentity(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "22222222-2222-2222-2222-222222222222"), filteringEnabled: true));
    }

    [TestMethod]
    public void CanAccessConversation_WhenObjectIdMatchesButTenantDiffers_DeniesAccess()
    {
        Assert.IsFalse(ConversationUserFilter.CanAccessConversation(OwnedMetadata, new EntraUserIdentity(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "11111111-1111-1111-1111-111111111111"), filteringEnabled: true));
    }

    [TestMethod]
    public void CanAccessConversation_WhenMetadataIsMissing_DeniesAccess()
    {
        Assert.IsFalse(ConversationUserFilter.CanAccessConversation(null, new EntraUserIdentity(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "11111111-1111-1111-1111-111111111111"), filteringEnabled: true));
    }

    [TestMethod]
    public void CanAccessConversation_WhenTenantMetadataIsMissing_DeniesAccess()
    {
        var metadata = new Dictionary<string, string>
        {
            [ConversationUserFilter.OwnerMetadataKey] = "11111111-1111-1111-1111-111111111111"
        };

        Assert.IsFalse(ConversationUserFilter.CanAccessConversation(metadata, new EntraUserIdentity(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "11111111-1111-1111-1111-111111111111"), filteringEnabled: true));
    }

    [TestMethod]
    public void CanAccessConversation_WhenUserIdentityIsMissing_DeniesAccess()
    {
        Assert.IsFalse(ConversationUserFilter.CanAccessConversation(OwnedMetadata, null, filteringEnabled: true));
    }

    [TestMethod]
    public void GetUserIdentity_ReadsTenantAndObjectIdClaims()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("tid", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            new Claim("oid", "11111111-1111-1111-1111-111111111111")
        ]));

        Assert.AreEqual(
            new EntraUserIdentity("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "11111111-1111-1111-1111-111111111111"),
            ConversationUserFilter.GetUserIdentity(user));
    }

    [TestMethod]
    public void GetUserIdentity_ReadsMappedTenantAndObjectIdClaims()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("http://schemas.microsoft.com/identity/claims/tenantid", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "11111111-1111-1111-1111-111111111111")
        ]));

        Assert.AreEqual(
            new EntraUserIdentity("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "11111111-1111-1111-1111-111111111111"),
            ConversationUserFilter.GetUserIdentity(user));
    }

    [TestMethod]
    public void GetUserIdentity_IgnoresSubjectClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "user-1"),
            new Claim("tid", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
        ]));

        Assert.IsNull(ConversationUserFilter.GetUserIdentity(user));
    }

    [TestMethod]
    public void GetUserIdentity_RejectsInvalidObjectId()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("tid", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            new Claim("oid", "not-a-guid")
        ]));

        Assert.IsNull(ConversationUserFilter.GetUserIdentity(user));
    }

    [TestMethod]
    public void GetUserIdentity_RejectsMissingTenantId()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("oid", "11111111-1111-1111-1111-111111111111")
        ]));

        Assert.IsNull(ConversationUserFilter.GetUserIdentity(user));
    }

    [TestMethod]
    public void RequireUserIdentity_ThrowsWhenIdentityIsMissing()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")]));

        Assert.ThrowsExactly<ConversationAccessDeniedException>(
            () => ConversationUserFilter.RequireUserIdentity(user));
    }

    [TestMethod]
    public void RequireUserIdentity_ThrowsWhenIdentityIsInvalid()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("tid", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            new Claim("oid", "not-a-guid")
        ]));

        Assert.ThrowsExactly<ConversationAccessDeniedException>(
            () => ConversationUserFilter.RequireUserIdentity(user));
    }
}
