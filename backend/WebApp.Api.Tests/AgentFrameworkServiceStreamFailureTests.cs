using Microsoft.VisualStudio.TestTools.UnitTesting;
using WebApp.Api.Services;

namespace WebApp.Api.Tests;

[TestClass]
public class AgentFrameworkServiceStreamFailureTests
{
    [TestMethod]
    public void StreamErrorDetails_WhenMessageIsNull_UsesNestedRawErrorMessage()
    {
        const string rawPayload = """
            {"type":"error","error":{"type":"invalid_request_error","code":"mcp_tool_failed","message":"MCP server rejected the tool call","param":null}}
            """;

        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: null,
            code: null,
            rawPayload: rawPayload);

        Assert.AreEqual("MCP server rejected the tool call", details.Message);
        Assert.AreEqual("mcp_tool_failed", details.Code);
        Assert.AreEqual(rawPayload, details.RawPayload);
    }

    [TestMethod]
    public void StreamErrorDetails_WhenMessageAndNestedMessageAreMissing_UsesRawPayload()
    {
        const string rawPayload = "{\"type\":\"error\",\"error\":{\"code\":null}}";

        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: null,
            code: null,
            rawPayload: rawPayload);

        Assert.AreEqual(rawPayload, details.Message);
        Assert.IsNull(details.Code);
        Assert.AreEqual(rawPayload, details.RawPayload);
    }

    [TestMethod]
    public void ResponseFailedDetails_UsesFailureMessageAndCode()
    {
        var details = AgentFrameworkService.CreateResponseFailedDetails(
            message: "Foundry response failed",
            code: "server_error",
            rawPayload: null);

        Assert.AreEqual("Foundry response failed", details.Message);
        Assert.AreEqual("server_error", details.Code);
        Assert.IsNull(details.RawPayload);
    }

    [TestMethod]
    public void ResponseFailedDetails_WhenMessageIsMissing_UsesNestedRawErrorMessage()
    {
        const string rawPayload = """
            {"response":{"status":"failed"},"error":{"code":"upstream_mcp_error","message":"Upstream MCP server failed"}}
            """;

        var details = AgentFrameworkService.CreateResponseFailedDetails(
            message: null,
            code: null,
            rawPayload: rawPayload);

        Assert.AreEqual("Upstream MCP server failed", details.Message);
        Assert.AreEqual("upstream_mcp_error", details.Code);
        Assert.AreEqual(rawPayload, details.RawPayload);
    }
}
