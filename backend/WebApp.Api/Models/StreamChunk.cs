namespace WebApp.Api.Models;

/// <summary>
/// Represents a chunk of streaming response data.
/// Can contain text content, annotations (citations), MCP tool approval requests,
/// or OAuth consent requests.
/// </summary>
public record StreamChunk
{
    /// <summary>
    /// Text content chunk (delta). Null if this chunk contains annotations or approval request.
    /// </summary>
    public string? TextDelta { get; init; }
    
    /// <summary>
    /// Annotations/citations extracted from the response. Null if this chunk contains text or approval request.
    /// </summary>
    public List<AnnotationInfo>? Annotations { get; init; }
    
    /// <summary>
    /// MCP tool approval request. Null if this chunk contains text or annotations.
    /// </summary>
    public McpApprovalRequest? McpApprovalRequest { get; init; }

    /// <summary>
    /// OAuth consent request for an MCP connection. Null for other chunk types.
    /// </summary>
    public OAuthConsentRequest? OAuthConsentRequest { get; init; }
    
    /// <summary>
    /// Whether this chunk signals a tool-use step (e.g. file_search, code_interpreter).
    /// </summary>
    public bool IsToolUse { get; init; }
    
    /// <summary>
    /// Name of the tool being invoked (set when IsToolUse is true).
    /// </summary>
    public string? ToolName { get; init; }
    
    /// <summary>
    /// Creates a text delta chunk.
    /// </summary>
    public static StreamChunk Text(string delta) => new() { TextDelta = delta };
    
    /// <summary>
    /// Creates an annotations chunk.
    /// </summary>
    public static StreamChunk WithAnnotations(List<AnnotationInfo> annotations) => new() { Annotations = annotations };
    
    /// <summary>
    /// Creates an MCP approval request chunk.
    /// </summary>
    public static StreamChunk McpApproval(McpApprovalRequest request) => new() { McpApprovalRequest = request };

    /// <summary>
    /// Creates an OAuth consent request chunk.
    /// </summary>
    public static StreamChunk OAuthConsent(OAuthConsentRequest request) => new() { OAuthConsentRequest = request };
    
    /// <summary>
    /// Creates a tool-use indicator chunk.
    /// </summary>
    public static StreamChunk ToolUse(string toolName) => new() { IsToolUse = true, ToolName = toolName };
    
    /// <summary>
    /// Whether this chunk contains text content.
    /// </summary>
    public bool IsText => TextDelta != null;
    
    /// <summary>
    /// Whether this chunk contains annotations.
    /// </summary>
    public bool HasAnnotations => Annotations != null && Annotations.Count > 0;
    
    /// <summary>
    /// Whether this chunk contains an MCP approval request.
    /// </summary>
    public bool IsMcpApprovalRequest => McpApprovalRequest != null;

    /// <summary>
    /// Whether this chunk contains an OAuth consent request.
    /// </summary>
    public bool IsOAuthConsentRequest => OAuthConsentRequest != null;
}

/// <summary>
/// Represents an MCP tool call requiring user approval.
/// </summary>
public record McpApprovalRequest
{
    public required string Id { get; init; }
    public required string ToolName { get; init; }
    public required string ServerLabel { get; init; }
    public string? Arguments { get; init; }
    public string? PreviousResponseId { get; init; }
}

/// <summary>
/// Represents an MCP connection that requires the user to complete OAuth authorization.
/// </summary>
public record OAuthConsentRequest
{
    public required string Id { get; init; }
    public required string ConsentLink { get; init; }
    public required string ServerLabel { get; init; }
    public required string PreviousResponseId { get; init; }
    public required string ContinuationMessage { get; init; }
}
