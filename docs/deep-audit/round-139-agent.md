# Round 139: the MCP agent (protocol, generated tools, UI adapters, bridge)

Continues round 136 (`round-136-security-updates.md`), which covered the HTTP server, access code, sessions, tool invoker,
document tools and the operation gate.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Agent/Protocol/JsonRpc.cs | 104/104 | clean (depth 64, duplicates refused, id kinds checked) |
| src/PulseOffice.Agent/Protocol/McpEndpoint.cs | 203/203 | minor: internal errors return `ex.Message` (see below) |
| src/PulseOffice.Agent/Tools/ScriptCatalogTools.cs | 183/183 | clean: arguments enter the generated call only as JSON literals |
| src/PulseOffice.Agent/Tools/AgentTool.cs | 151/151 | changed (confirmation carried to the tool) |
| src/PulseOffice.Agent/Tools/DocumentTools.cs | 211/211 | defect fixed (overwrite recheck) |
| src/PulseOffice.Agent/Tools/CommandTools.cs | 158/158 | clean |
| src/PulseOffice.Agent/Tools/ViewTools.cs | 50/50 | clean (width and page bounded) |
| src/PulseOffice.Agent/Tools/AgentDocumentChoice.cs | 18/18 | clean |
| src/PulseOffice.Agent/Hosting/AgentHost.cs | 92/92 | clean |
| src/PulseOffice.Agent/Hosting/AgentModules.cs | 34/34 | clean |
| src/PulseOffice.Agent/Hosting/AgentPictureFrame.cs | 25/25 | clean (height capped at 6000) |
| src/PulseOffice.Agent/Hosting/ModuleAgentScripts.cs | 35/35 | clean |
| src/PulseOffice.Agent/Hosting/AgentClientInstructions.cs | 36/36 | clean |
| src/PulseOffice.Agent/AgentAccessLevel.cs | 8/8 | clean |
| src/PulseOffice.Agent/Security/AgentToolInvoker.cs | 90/90 | changed (records the granted confirmation) |
| src/PulseOffice.Agent.UI/*.cs (13 files) | 812/812 | clean, notes below |
| tools/mcp-client/Program.cs | 107/107 | changed (bridge diagnostics writer) |
| tools/mcp-client/McpClient.cs | 103/103 | defects fixed (bridge) |

## Defects

### Medium
1. **The stdio bridge died on any non-JSON reply** (tools/mcp-client/McpClient.cs, old lines 50-51 and 78): a 401, 403,
   413 or 503 with an empty body threw `InvalidOperationException`, and a timeout `TaskCanceledException`; only
   `HttpRequestException` was caught, so the bridge process ended and Claude Desktop lost the server. A malformed stdin
   line made `Failure` throw `JsonException` as well. Now every request gets a JSON-RPC answer with its id, a malformed
   line a -32700 parse error, notifications are never answered.
2. **The bridge never recovered an expired session** (same file): after the 30-minute idle expiry or a restart of
   PulseOffice every call returned 404 forever, because the stdio client does not know about HTTP sessions. The bridge
   now replays the client's last `initialize`, sends `notifications/initialized` and retries the request once.

### Minor
3. **save_document_as and export_document overwrote a file created after the check** (DocumentTools.cs,
   `ConfirmationAsync` vs `InvokeAsync`): the existence check ran only before the confirmation step. The invoker now
   records the confirmation the user granted on the arguments (`AgentArguments.Confirmed`), and `AgentOverwriteCheck`
   refuses to write over a file that exists at invocation time without an overwrite confirmation; the agent is told to
   call again so the user is asked.

### Noted, not changed
- McpEndpoint line 93 and AgentToolInvoker line 72 return `ex.Message` of unexpected exceptions to the client; they can
  carry a file path, but only paths the agent itself named or that `list_documents` already shows.
- `ScriptMemberAccess` classifies members by name (`_readers`) and by parameter names containing "path"; every reader
  name in the three catalogs was checked and is a getter, and the only file-writing member (`page.ExportPdf(path)`) is
  Files and also demands `FileSystemWrite` at run time. A new member that writes files without a "path" parameter
  would still be held by the sandbox (no file permission below Files).
- `ledger_page_properties` reads `Printers`, which enumerates the system printers on every call: cost only.
- The settings card's "show code" button keeps its label when the code is shown.
- A JSON object argument with a `"__proto__"` key sets the literal's prototype in the generated JavaScript; harmless in
  the sandbox.

## Tests
`tests/PulseOffice.Agent.Tests/Round139AgentTests.cs` (5 tests, all failing before the fixes, verified by reversing the
patch): PulseOffice.Agent.Tests 98/98, PulseOffice.Architecture.Tests 43/43.

## To test live
Claude Desktop through `pulse-mcp bridge`: quit and restart PulseOffice while the client stays connected and call a
tool (it must work again without restarting Claude Desktop); disable the agent and call a tool (an error message, no
disconnection); `export_document` to a new path while creating the same file from Finder between calls.
