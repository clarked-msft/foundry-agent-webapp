using System.Security.Claims;

namespace WebApp.Api.Services;

internal static class ConversationUserFilter
{
    internal const string OwnerMetadataKey = "ownerObjectId";
    private const string ObjectIdClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    internal static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool>("ENABLE_CONVERSATION_USER_FILTERING");

    internal static string? GetUserObjectId(ClaimsPrincipal? user)
    {
        var claimValue = user?.FindFirst("oid")?.Value
            ?? user?.FindFirst(ObjectIdClaimType)?.Value;

        return Guid.TryParseExact(claimValue, "D", out var objectId)
            ? objectId.ToString("D")
            : null;
    }

    internal static string RequireUserObjectId(ClaimsPrincipal? user) =>
        GetUserObjectId(user) ?? throw new ConversationAccessDeniedException();

    internal static bool IsOwnedByUser(IDictionary<string, string>? metadata, string? userObjectId) =>
        !string.IsNullOrWhiteSpace(userObjectId)
        && metadata?.TryGetValue(OwnerMetadataKey, out var ownerObjectId) == true
        && string.Equals(ownerObjectId, userObjectId, StringComparison.Ordinal);

    internal static bool CanAccessConversation(
        IDictionary<string, string>? metadata,
        string? userObjectId,
        bool filteringEnabled) =>
        !filteringEnabled || IsOwnedByUser(metadata, userObjectId);
}

internal sealed class ConversationAccessDeniedException : Exception
{
    internal ConversationAccessDeniedException()
        : base("Conversation was not found or is not accessible.")
    {
    }
}
