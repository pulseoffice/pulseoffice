# Deep audit round 46: macro sandbox and MCP agent

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Scripting.JavaScript/JavaScriptEngine.cs | 206/206 | probes added (regex, delegates) |
| src/PulseOffice.Agent/Transport/AgentHttpServer.cs | 261/261 | Host/Origin checked, body capped |
| src/PulseOffice.Agent/Security/AgentAccessCode.cs | 81/81 | 0600 file, fixed-time compare |
| src/PulseOffice.Scripting.Python/IronPythonScriptEngine.cs | 180/180 | gated by AllowsUnsandboxedEngines |
| src/PulseOffice.Scripting/MacroFiles.cs | 139/139 | traversal, links, extensions checked |
| src/PulseOffice.Scripting/MacroWritePermission.cs | 148/148 | fingerprint over language + normalised source, well-formed grants only |
| src/PulseOffice.Scripting/ScriptExecutionPermissions.cs | 75/75 | AsyncLocal scope; FileSystemWrite implies MacroFolderWrite (agent "files" level only) |
| src/PulseOffice.Scripting.Editor/MacroEditorLauncher.cs | 160/160 | unsandboxed engines only from the editor, labelled `macro.runtime.unsandboxed` |
| src/PulseOffice.Agent/Hosting/ModuleAgentScripts.cs | 35/35 | JavaScript only, sandboxed below "files" |
| src/PulseOffice.Data/ExternalQueryGate.cs, src/PulseOffice.Sheets.Queries/QueryRefresher.cs | full / 25-165 | non read-only SQL refused before execution; no script facade reaches the refresher |
| src/PulseOffice.Agent/Tools/CommandTools.cs | 158/158 | level checked before confirmation; RunsDocumentQueries -> files + confirmation |
| src/PulseOffice.Agent/Security/AgentToolInvoker.cs | 86/86 | level enforced on every tool call |
| src/PulseOffice.Agent/Tools/ScriptCatalogTools.cs | 179/179 | arguments serialised as JSON literals (no source injection) |
| src/PulseOffice.Agent/Protocol/McpEndpoint.cs, Security/AgentSessions.cs | 191/191, 87/87 | random 128-bit sessions, idle expiry |
| src/PulseOffice.Agent/Tools/DocumentTools.cs | 211/211 | save/export/open at "files", overwrite confirmed |

## Probes added (tests/PulseOffice.Scripting.Tests/JavaScriptSandboxTests.cs)

- catastrophic regex backtracking stops within the time cap;
- host delegates (`print`, `console.log`) expose no `Method`, `Target`, `DynamicInvoke`;
- pollution of `Object.prototype` does not reach host object members.

All pass (153/153 in the project): no escape found.

## Defects

None blocking, serious or medium. Minor, not fixed (authenticated local clients only): the agent server does not cap
the number of sessions or of requests in flight; `MacroFileFolder.Resolve` checks links before the write, so a
concurrent local process could swap a subfolder for a link between check and move (needs a hostile local process
already running as the user).
