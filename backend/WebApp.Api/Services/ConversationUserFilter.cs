using System.Security.Claims;

namespace WebApp.Api.Services;

internal static class ConversationUserFilter
{
    internal const string OwnerTenantMetadataKey = "ownerTenantId";
    internal const string OwnerMetadataKey = "ownerObjectId";
    private const string ObjectIdClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string TenantIdClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";

    internal static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool>("ENABLE_CONVERSATION_USER_FILTERING");

    internal static EntraUserIdentity? GetUserIdentity(ClaimsPrincipal? user)
    {
        var objectIdClaim = user?.FindFirst("oid")?.Value
            ?? user?.FindFirst(ObjectIdClaimType)?.Value;
        var tenantIdClaim = user?.FindFirst("tid")?.Value
            ?? user?.FindFirst(TenantIdClaimType)?.Value;

        return Guid.TryParseExact(objectIdClaim, "D", out var objectId)
            && Guid.TryParseExact(tenantIdClaim, "D", out var tenantId)
            ? new EntraUserIdentity(tenantId.ToString("D"), objectId.ToString("D"))
            : null;
    }

    internal static EntraUserIdentity RequireUserIdentity(ClaimsPrincipal? user) =>
        GetUserIdentity(user) ?? throw new ConversationAccessDeniedException();

    internal static bool IsOwnedByUser(IDictionary<string, string>? metadata, EntraUserIdentity? identity) =>
        identity is { } userIdentity
        && metadata?.TryGetValue(OwnerTenantMetadataKey, out var ownerTenantId) == true
        && metadata.TryGetValue(OwnerMetadataKey, out var ownerObjectId)
        && string.Equals(ownerTenantId, userIdentity.TenantId, StringComparison.Ordinal)
        && string.Equals(ownerObjectId, userIdentity.ObjectId, StringComparison.Ordinal);

    internal static bool CanAccessConversation(
        IDictionary<string, string>? metadata,
        EntraUserIdentity? identity,
        bool filteringEnabled) =>
        !filteringEnabled || IsOwnedByUser(metadata, identity);
}

internal readonly record struct EntraUserIdentity(string TenantId, string ObjectId);

internal sealed class ConversationAccessDeniedException : Exception
{
    internal ConversationAccessDeniedException()
        : base("Conversation was not found or is not accessible.")
    {
    }
}
