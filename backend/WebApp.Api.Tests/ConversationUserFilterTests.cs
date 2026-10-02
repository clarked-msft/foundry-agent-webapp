using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using WebApp.Api.Services;

namespace WebApp.Api.Tests;

[TestClass]
public class ConversationUserFilterTests
{
    private static readonly IDictionary<string, string> OwnedMetadata = new Dictionary<string, string>
    {
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
        Assert.IsTrue(ConversationUserFilter.CanAccessConversation(OwnedMetadata, "22222222-2222-2222-2222-222222222222", filteringEnabled: false));
    }

    [TestMethod]
    public void CanAccessConversation_WhenUserOwnsConversation_AllowsAccess()
    {
        Assert.IsTrue(ConversationUserFilter.CanAccessConversation(OwnedMetadata, "11111111-1111-1111-1111-111111111111", filteringEnabled: true));
    }

    [TestMethod]
    public void CanAccessConversation_WhenAnotherUserOwnsConversation_DeniesAccess()
    {
        Assert.IsFalse(ConversationUserFilter.CanAccessConversation(OwnedMetadata, "22222222-2222-2222-2222-222222222222", filteringEnabled: true));
    }

    [TestMethod]
    public void CanAccessConversation_WhenMetadataIsMissing_DeniesAccess()
    {
        Assert.IsFalse(ConversationUserFilter.CanAccessConversation(null, "11111111-1111-1111-1111-111111111111", filteringEnabled: true));
    }

    [TestMethod]
    public void CanAccessConversation_WhenUserIdentityIsMissing_DeniesAccess()
    {
        Assert.IsFalse(ConversationUserFilter.CanAccessConversation(OwnedMetadata, null, filteringEnabled: true));
        Assert.IsFalse(ConversationUserFilter.CanAccessConversation(OwnedMetadata, " ", filteringEnabled: true));
    }

    [TestMethod]
    public void GetUserObjectId_ReadsEntraObjectIdClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("oid", "11111111-1111-1111-1111-111111111111")]));

        Assert.AreEqual("11111111-1111-1111-1111-111111111111", ConversationUserFilter.GetUserObjectId(user));
    }

    [TestMethod]
    public void GetUserObjectId_ReadsMappedEntraObjectIdClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "11111111-1111-1111-1111-111111111111")]));

        Assert.AreEqual("11111111-1111-1111-1111-111111111111", ConversationUserFilter.GetUserObjectId(user));
    }

    [TestMethod]
    public void GetUserObjectId_IgnoresSubjectClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")]));

        Assert.IsNull(ConversationUserFilter.GetUserObjectId(user));
    }

    [TestMethod]
    public void GetUserObjectId_RejectsInvalidObjectId()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", "not-a-guid")]));

        Assert.IsNull(ConversationUserFilter.GetUserObjectId(user));
    }

    [TestMethod]
    public void RequireUserObjectId_ThrowsWhenIdentityIsMissing()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-1")]));

        Assert.ThrowsExactly<ConversationAccessDeniedException>(
            () => ConversationUserFilter.RequireUserObjectId(user));
    }

    [TestMethod]
    public void RequireUserObjectId_ThrowsWhenIdentityIsInvalid()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", "not-a-guid")]));

        Assert.ThrowsExactly<ConversationAccessDeniedException>(
            () => ConversationUserFilter.RequireUserObjectId(user));
    }
}
