# Round 48: agent limits and race-free macro file writes

Closes the two minor defects left open by round 46 (`round-46-sandbox-agent.md`).

## 1. MCP agent server without limits (minor)

`AgentSessions` created sessions without a cap and expired idle ones only on lookup, so a flood of `initialize`
calls grew the table without bound; `McpEndpoint` ran any number of requests in parallel.

Fix:
- `AgentLimits` (record, registered in DI): 16 sessions, 4 requests in flight per session, 16 overall, 10-minute idle
  expiry (was a private constant).
- `IAgentSessions.TryCreate` (replaces `Create`) expires idle sessions first and returns null at the cap; the endpoint
  answers HTTP 503 with JSON-RPC error `-32001` (`JsonRpcCodes.TooManySessions`) and logs a warning in English.
- `IAgentRequestGate` / `AgentRequestGate`: per-session and overall counters under a lock; the endpoint holds a ticket
  for the duration of the method (released on success, failure and cancellation); over the limit it answers HTTP 429
  with `-32002` (`TooManyRequests`) and logs a warning.

Tests: `tests/PulseOffice.Agent.Tests/AgentLimitsTests.cs` (session cap and expiry, per-session cap, overall cap,
place given back after a failure). Agent tests 71/71.

## 2. TOCTOU in macro file writes (minor)

`MacroFileFolder.Resolve` checked links, then `ScriptFilesFacade` wrote a temporary file and moved it: a local process
could swap a subfolder for a link in between and the move would land outside the macro folder.

Fix, in `PulseOffice.Platform` (no `#if`): `IConfinedFileWriter` / `ConfinedFileWriter` over `IFileSystemLinks`
(symlinks, junctions and reparse points on all three systems). It creates and checks every folder of the chain from the
root, writes the temporary file in the final folder, fingerprints it (SHA-256), re-checks the whole chain, the temporary
file and the target just before the move, moves, then re-checks the chain and the target again: if a link appeared it
deletes the moved file when its fingerprint is ours (roll back) and refuses. `ScriptFilesFacade` takes the writer by
injection; `PulseOffice.Scripting` now references `PulseOffice.Platform`. `ConfinedPathException` becomes
`MacroFilePathException` for the macro.

Residual (declared): .NET has no portable `openat`/`O_NOFOLLOW` directory handles, so a swap landing in the few
instructions between the last pre-move check and `rename` still moves the file outside for an instant; the post-move
check removes it, but a foreign file with the same name at the link target would already have been replaced. This needs
a hostile local process already running as the user.

Tests: `tests/PulseOffice.Scripting.Tests/MacroFolderSwapTests.cs` simulates the swap through a wrapping
`IFileSystemLinks` (while writing, and right before the move, also relocating the temporary file as an attacker would):
nothing stays outside. Scripting tests 156/156, Architecture 40/40; Desktop and Documents.UI.Tests build.

## Files

- `src/PulseOffice.Agent/Security/AgentSessions.cs`, `src/PulseOffice.Agent/Protocol/McpEndpoint.cs`,
  `src/PulseOffice.Agent/Protocol/JsonRpc.cs`, `src/PulseOffice.Agent.UI/AgentServiceCollectionExtensions.cs`
- `src/PulseOffice.Platform/ConfinedFiles.cs` (new), `src/PulseOffice.Scripting/MacroFiles.cs`,
  `src/PulseOffice.Scripting/PulseOffice.Scripting.csproj`, `src/PulseOffice.Desktop/Composition/DocumentFormatsComposition.cs`
- Tests: `AgentLimitsTests.cs` (new), `MacroFolderSwapTests.cs` (new), `ProtocolTests.cs`, `HttpServerTests.cs`,
  `MacroWritePermissionAgentTests.cs`, `MacroWritePermissionTests.cs`, `FolioScene.cs`
