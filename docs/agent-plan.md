# Plan: PulseOffice controllable by an AI agent

State: **done** (2026-10-24). The five steps of the work order are closed; the report with the evidence is in the
[log](worklog.md), entry «Agente AI: server MCP».
User decision of 2026-10-24: "Put it in the plan, but don't do it right away"; started on request the same day.

## Goal

An AI agent (Claude or others) must be able to use Folio, Ledger and Deck as a user does: open, read, edit,
save and export documents, and run any command of the app, in a safe, undoable and traced way.

## Choice: an integrated MCP server

MCP (Model Context Protocol) is the standard protocol through which agents use external tools. The server lives in two
new projects, outside the core:

| Project | Responsibility |
|---|---|
| `PulseOffice.Agent` | protocol (JSON-RPC 2.0, MCP methods), local HTTP transport, tools, permissions, access code, sessions, operations log. **No Avalonia**: everything is tested without an interface |
| `PulseOffice.Agent.UI` | the adapters towards the app: workspace, command registry, macro objects, page rendering, confirmation modal, status bar indicator, settings section |

The protocol is **written by us**: the official SDK (`ModelContextProtocol`) is not in the local NuGet cache and nothing
is downloaded. Three methods are needed (`initialize`, `tools/list`, `tools/call`) plus `ping` and notifications: a few hundred
lines behind interfaces (`IJsonRpcCodec`, `IMcpMethod`, `IMcpEndpoint`), each method a Strategy.

| Already exists | Use for the agent |
|---|---|
| Single command registry (ribbon, menus, palette are projections of it) | `list_commands` and `run_command` |
| `ScriptApiCatalog` (macro API as metadata) | generates the content tools, without writing them twice |
| Macro sandbox (`ScriptPermissions.Sandboxed`, time and memory caps, `ReadOnlyGuardedDispatcher`) | every content tool runs inside it, like a macro |
| Undoable command stack (`UndoStack.BeginTransaction`) | every agent action is one ⌘Z step, «Agente: …» |
| `IDocumentOperations`, `IPdfExporter`, `AvaloniaPageCanvas` | export and page images with our own engine |

Discarded: driving the interface with simulated clicks and keys (fragile, slow, breaks at every graphics change).

## Transport

**Local "streamable" HTTP**: `POST http://127.0.0.1:<port>/mcp` with `Authorization: Bearer <code>`, responses
`application/json` (never SSE streams: not needed), session in `Mcp-Session-Id` after `initialize`, `DELETE` closes the
session, `GET` answers 405. The server listens **only on 127.0.0.1**, rejects requests that do not come from loopback and
those with an `Origin` other than localhost (defence against DNS rebinding: a web page cannot talk to it). Default port
47820, changeable in the advanced settings. Sessions idle for ten minutes expire.

For clients that start MCP servers as stdio commands (Claude Desktop) there is a bridge: `tools/mcp-client`
(`pulse-mcp bridge`) reads JSON-RPC from stdin and forwards it to the HTTP server. Since round 139 the bridge never
stops on a bad exchange: a malformed line gets a parse error, a refused or unreachable request (401, 403, 413, 503,
timeout, connection refused) gets a JSON-RPC error with the request's id, and when the server answers 404 because the
session expired or PulseOffice restarted, the bridge replays the client's last `initialize`, opens a new session and
retries the request once. The same program acts as a test client
(`pulse-mcp tools`, `pulse-mcp call <tool> '<JSON arguments>' [--image file.png]`).

## The tools (139 with the three modules)

1. **Documents**: `list_documents`, `activate_document`, `create_document` (folio, ledger, deck), `open_document`,
   `save_document`, `save_document_as` (the extension chooses the format between native and Office; an unknown extension is rejected), `export_document` (`.pdf` or the module's formats).
   Documents are identified by a short id stable for the app session (`IAgentDocumentIds`).
2. **Content**, generated from `ScriptApiCatalog` (`ScriptCatalogToolSource`): for every macro object of a module,
   a method becomes `<module>_<object>_<Member>` (`ledger_sheet_Write`, `folio_doc_SetStyle`, `deck_deck_AddSlide`…) and
   properties are read together with `<module>_<object>_properties`. The tool builds the call
   `object.Member(arguments)` with the arguments as **JSON literals** (never concatenated text: an argument cannot
   become code) and runs it in the macro JavaScript engine, sandboxed, with the same global objects. It acts on the
   module's active document or on the one passed in `document`. Left out are `Undo`, `Redo` and `Log` (undo
   goes through `run_command shell.edit.undo`) and the `app` object, which the commands cover.
   A cell reference, paragraph or slide that does not exist gives an **error** with the actual size
   ("There is no paragraph 0: the document has 126 paragraphs…"), not empty text nor "Done."; a read that has no
   value returns `null`, "Done." remains only for `void` methods; an argument error reports the expected signature and the
   meaning of each argument. Every change is a labelled step (`UndoStack.BeginLabelledStep`) that does not merge
   with neighbouring ones, even when it contains a single operation.
   Ledger's facades respect sheet and structure protection like the interface does (`IScriptProtection`):
   a locked cell, a format or a row on a protected sheet give the agent an error.
3. **Commands**: `list_commands` (enabled in the context, with id, title, description, category, module, required
   permission and whether it will ask for confirmation; search by words) and `run_command` by id, executed by `CommandExecutor` as a
   user choice. Since the twenty-fifth round `CommandExecutor.ExecuteAsync` returns a `CommandOutcome`
   (`Ran`, `NotApplicable`, `Refused`, `Cancelled`, `Failed`, with reason and messages): every warning or error published
   during the command (for example «Il foglio protetto non consente di formattare le celle», «Nessun duplicato») is a
   refusal, and the agent receives "did not run: <reason>"; an exception is "failed: <reason>"; informational messages
   accompany "Ran …: <message>". A refused command no longer becomes the last one to repeat nor enters the recorded
   macro. `render_page` rejects a width outside 200–3000 instead of silently clamping it; `open_document`
   on a folder says it is a folder. The contract of the hand-written tools (documents, commands, view) is
   `AgentToolContractTests`, which fails if a tool appears without its case.
4. **View**: `render_page` (pages numbered from 1; in Deck **every** slide, hidden ones included, so the number is
   that of `deck_deck_Title`) returns a PNG of a Folio page, a Deck slide or a printed Ledger page, or
   (`range`) a range of the active sheet, drawn by `IPrintableDocument` on `AvaloniaPageCanvas`
   (the same engine as print preview and PDF), with fonts resolved by `IDisplayFonts`.

## Security

- **Off by default**: it is turned on from Settings › AI agent (`agent.enabled`); turning it off closes the
  server and the sessions.
- **Local only, with an access code**: 24 random bytes in `config/agent/access-code` inside the app's data
  folder, permissions 0600, constant-time comparison, regenerable from the section (regenerating it disconnects the clients). It
  does not go into the keychain: the choice can be revisited with the user.
- **Permissions by level** (`agent.level`): *Read only*, *Read and edit*, *Edit, files and network*. Every tool
  declares its level; `tools/list` shows only the permitted ones and `tools/call` rejects the others with a message that
  explains where to raise it. For commands `AgentCommandPolicy` decides: those of the File menu or that export, print, send,
  open or save require the file level; options, macros, security, passwords and signatures never
  belong to the agent (it cannot raise its own permissions). For macro members `ScriptMemberAccess` decides: properties and
  readers are read, members with a path are file, the rest edit.
- **Visible confirmation** in a modal with backdrop (`ChoicePromptView` in the modal service) for what ⌘Z does not bring
  back: overwriting a file (save, save as, export onto an existing file), commands that delete, close,
  send, share or print, those that leave the undo history (`CommandDefinition.OutsideHistory`: protecting, recording
  changes) and macro members that write to disk. Refusal stops everything and the agent
  receives "The user declined".
- **Undoable and marked**: every tool that edits opens a transaction on the document history with the
  description «Agente: <tool>», hence a single ⌘Z step (verified live in Ledger, Folio and Deck).
- **Visible indicator**: in the status bar, shared by the three modules, «○ Agente: in attesa» with the server on and
  «● Agente collegato: <client>» with an open session; the settings section shows state, address and client.
- **Operations log**: every call (done, failed, not permitted, declined) goes into the app log in
  English and into `config/agent/operations.jsonl`; the last six are shown in the settings section.

## Connecting a client

Turn the agent on in Settings › AI agent, choose the level, press "Show" and copy the command.

**Claude Code** (direct HTTP transport):

```bash
claude mcp add --transport http pulseoffice http://127.0.0.1:47820/mcp --header "Authorization: Bearer <codice>"
```

or, in the project's `.mcp.json`:

```json
{
  "mcpServers": {
    "pulseoffice": {
      "type": "http",
      "url": "http://127.0.0.1:47820/mcp",
      "headers": { "Authorization": "Bearer <codice>" }
    }
  }
}
```

**Claude Desktop** (starts servers as stdio commands: the bridge is used, which reads the code from the file and does not copy it
into the configuration). With `tools/mcp-client` built (`dotnet build tools/mcp-client`), in
`~/Library/Application Support/Claude/claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "pulseoffice": {
      "command": "dotnet",
      "args": [
        "/percorso/del/repo/tools/mcp-client/bin/Debug/net10.0/pulse-mcp.dll",
        "bridge",
        "--url", "http://127.0.0.1:47820/mcp",
        "--token-file", "/Users/<utente>/Library/Application Support/PulseOffice/agent/access-code"
      ]
    }
  }
}
```

On Linux the folder is `~/.config/PulseOffice`, on Windows `%APPDATA%\PulseOffice`; with `PULSEOFFICE_DATA_HOME` it is
`<folder>/config`.

**Manual test** with the `tools/mcp-client` client:

```bash
pulse-mcp call --url http://127.0.0.1:47820/mcp --token-file <file> create_document '{"module":"ledger"}'
pulse-mcp call ... ledger_sheet_Write '{"reference":"A1","value":21}'
pulse-mcp call ... render_page '{"module":"ledger","range":"A1:C5"}' --image foglio.png
```

## Work order (done)

1. MCP server with documents and commands.
2. Content tools generated from `ScriptApiCatalog`.
3. Permissions, confirmations, indicator, settings section and log.
4. View tool (page images).
5. Tests (unit tests on protocol and tools, integration with a real HTTP client against the running server), live
   test with `pulse-mcp` in the three modules, documentation, log entry.

## Limits and follow-ups

- Responses are JSON only, without SSE streams or server notifications (`listChanged` is false): enough for synchronous
  tools. A call that opens a modal waits for the user's answer.
- Commands that open a dialog show it to the user: the agent does not fill it in.
- Content tool descriptions come from the macro texts, hence in the interface language.
- Tested only on macOS; the server is `HttpListener` on loopback, with no platform code, but Windows and Linux
  must be seen live.
