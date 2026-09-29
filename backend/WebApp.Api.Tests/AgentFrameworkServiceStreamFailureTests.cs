#pragma warning disable OPENAI001
#pragma warning disable SCME0001

using System.ClientModel.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenAI.Responses;
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
    public void StreamErrorDetails_WhenMessageAndNestedMessageAreMissing_CapturesRawPayload()
    {
        const string rawPayload = "{\"type\":\"error\",\"error\":{\"code\":null}}";

        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: null,
            code: null,
            rawPayload: rawPayload);

        Assert.AreEqual("The Responses API returned an error without a message.", details.Message);
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

    [TestMethod]
    public void StreamErrorDetails_WhenOnlyCodeExists_UsesFallbackMessageAndCapturesCode()
    {
        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: null,
            code: "mcp_tool_failed",
            rawPayload: null);

        Assert.AreEqual("The Responses API returned an error without a message.", details.Message);
        Assert.AreEqual("mcp_tool_failed", details.Code);
        Assert.IsNull(details.RawPayload);
    }

    [TestMethod]
    public void ClassifyUnhandledStreamUpdate_TreatsMcpArgumentDeltaAsExpectedLifecycle()
    {
        var handling = AgentFrameworkService.ClassifyUnhandledStreamUpdate(
            new StreamingResponseMcpCallArgumentsDeltaUpdate());

        Assert.AreEqual(AgentFrameworkService.StreamUpdateHandling.ExpectedLifecycle, handling);
    }

    [TestMethod]
    public void ClassifyUnhandledStreamUpdate_TreatsUnhandledUpdateAsUnknown()
    {
        var handling = AgentFrameworkService.ClassifyUnhandledStreamUpdate(
            new StreamingResponseErrorUpdate());

        Assert.AreEqual(AgentFrameworkService.StreamUpdateHandling.Unknown, handling);
    }

    [TestMethod]
    public void StreamErrorDetails_WhenRawPayloadIsEmptyPatchArray_UsesFallbackWithoutThrowing()
    {
        // An empty JsonPatch serializes as "[]", which previously threw InvalidOperationException.
        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: null,
            code: null,
            rawPayload: new JsonPatch().ToBinaryData().ToString());

        Assert.AreEqual("The Responses API returned an error without a message.", details.Message);
        Assert.IsNull(details.Code);
        Assert.AreEqual("[]", details.RawPayload);
    }

    [TestMethod]
    public void StreamErrorDetails_WhenJsonPatchSetsErrorObject_UsesNestedMessageAndCode()
    {
        var patch = new JsonPatch();
        patch.Set("$.error"u8, BinaryData.FromString("""{"code":"mcp_tool_failed","message":"MCP server rejected the tool call"}"""));
        var rawPayload = patch.ToBinaryData().ToString();

        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: null,
            code: null,
            rawPayload: rawPayload);

        Assert.AreEqual("MCP server rejected the tool call", details.Message);
        Assert.AreEqual("mcp_tool_failed", details.Code);
        Assert.AreEqual(rawPayload, details.RawPayload);
    }

    [TestMethod]
    public void ResponseFailedDetails_WhenPatchOperationsTargetErrorFields_UsesNestedMessageAndCode()
    {
        const string rawPayload = """
            [{"op":"add","path":"/error/message","value":"Upstream MCP server failed"},{"op":"replace","path":"/error/code","value":"upstream_mcp_error"}]
            """;

        var details = AgentFrameworkService.CreateResponseFailedDetails(
            message: null,
            code: null,
            rawPayload: rawPayload);

        Assert.AreEqual("Upstream MCP server failed", details.Message);
        Assert.AreEqual("upstream_mcp_error", details.Code);
    }

    [TestMethod]
    public void StreamErrorDetails_WhenPatchOperationReplacesRoot_UsesNestedMessageAndCode()
    {
        const string rawPayload = """
            [{"op":"replace","path":"","value":{"error":{"code":"server_error","message":"Root replaced"}}}]
            """;

        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: null,
            code: null,
            rawPayload: rawPayload);

        Assert.AreEqual("Root replaced", details.Message);
        Assert.AreEqual("server_error", details.Code);
    }

    [TestMethod]
    public void StreamErrorDetails_WhenPatchPathIsNotJsonPointer_IgnoresOperation()
    {
        const string rawPayload = """
            [{"op":"add","path":"/error","value":{"code":"c","message":"m"}},{"op":"remove","path":"error"}]
            """;

        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: null,
            code: null,
            rawPayload: rawPayload);

        Assert.AreEqual("m", details.Message);
        Assert.AreEqual("c", details.Code);
    }

    [TestMethod]
    public void StreamErrorDetails_WhenExplicitMessageAndCodeProvided_PreferThemOverPatchValues()
    {
        const string rawPayload = """
            [{"op":"add","path":"/error","value":{"code":"patch_code","message":"Patch message"}}]
            """;

        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: "Explicit message",
            code: "explicit_code",
            rawPayload: rawPayload);

        Assert.AreEqual("Explicit message", details.Message);
        Assert.AreEqual("explicit_code", details.Code);
    }

    [TestMethod]
    [DataRow("""[{"op":"add","path":"/error","value":{"code":"c","message":"m"}},{"op":"remove","path":"/error"}]""")]
    [DataRow("""[{"op":"add","path":"/error","value":{"code":"c","message":"m"}},{"op":"replace","path":"/error","value":{}}]""")]
    [DataRow("""[{"op":"add","path":"/error/message","value":"m"},{"op":"add","path":"/error/code","value":"c"},{"op":"remove","path":"/error/message"},{"op":"replace","path":"/error/code","value":null}]""")]
    public void StreamErrorDetails_WhenLaterPatchOperationClearsError_UsesFallback(string rawPayload)
    {
        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: null,
            code: null,
            rawPayload: rawPayload);

        Assert.AreEqual("The Responses API returned an error without a message.", details.Message);
        Assert.IsNull(details.Code);
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("{\"error\":")]
    [DataRow("42")]
    [DataRow("\"error\"")]
    [DataRow("null")]
    [DataRow("true")]
    [DataRow("[1,\"x\",null,[]]")]
    [DataRow("""[{"op":"add","path":"/other","value":{"message":"m","code":"c"}}]""")]
    [DataRow("""[{"op":"remove","path":"/error"},{"op":"test","path":"/error/message","value":"m"}]""")]
    [DataRow("""[{"op":"add","path":"/error~1message","value":"m"},{"op":"add","path":"/error/message"}]""")]
    [DataRow("""[{"op":"add","path":"/error","value":null},{"op":"add","path":"/error/code","value":5}]""")]
    [DataRow("""[{"op":5,"path":"/error","value":{"message":"m"}},{"op":"add","path":null,"value":{"message":"m"}}]""")]
    [DataRow("""{"error":"not an object"}""")]
    [DataRow("""{"error":{"message":5,"code":null}}""")]
    public void StreamErrorDetails_WhenRawPayloadHasNoUsableError_UsesFallbackWithoutThrowing(string rawPayload)
    {
        var details = AgentFrameworkService.CreateStreamErrorDetails(
            message: null,
            code: null,
            rawPayload: rawPayload);

        Assert.AreEqual("The Responses API returned an error without a message.", details.Message);
        Assert.IsNull(details.Code);
        Assert.AreEqual(rawPayload, details.RawPayload);
    }
}
