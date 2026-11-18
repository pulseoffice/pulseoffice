# Round 185: macro scripting API and MCP agent tools

Area: the macro API (`ScriptApiCatalog` and the Folio, Ledger and Deck facades), the macro sandbox (decision 13),
the MCP agent tools and server (decisions 18 and 26, `docs/agent-plan.md`).

## What was checked

- HTTP server: loopback and origin checks, bearer code compared in fixed time, 4 MB body cap (header and streamed),
  405/404/413/401/403 replies, drain on stop. Access code file written through `IPrivateFiles` (0600 on Unix, owner-only
  ACL on Windows) and restricted again on load. Nothing to fix.
- JSON-RPC codec: parse errors, non-object messages, wrong `jsonrpc`, id kinds, null id, non-object params, depth cap,
  duplicate properties. Session and request limits. Nothing to fix.
- Tool invoker: permission level, confirmation before irreversible, file and overwrite tools, refusal logging.
- Workspace: edits go through `IAgentOperationGate.EditAsync` (refused under an open modal), one labelled «Agente: …»
  step, effect guards (protected content undone and reported), read-only documents refused; the target document is held
  and activated, so facades acting on the active context act on the chosen document.
- Facades: Folio (protected sections, forms, read-only, tracked-change protection), Ledger (cell, format, structure and
  filter protection, read-only through `ReadOnlyGuardedDispatcher`), Deck (read-only). `FormulaLocal` and
  `SetFormulaLocal` use the interface language, the same as cell entry and the formula bar.
- Sandbox: Jint limits (time, memory, recursion, array size, no string compilation, no CLR access); IronPython and CPython
  refused unless unsandboxed engines are allowed; the `files` root demands `MacroFolderWrite`; the per-macro write grant
  keyed by the SHA-256 of language and code.

## Defects fixed

1. **Medium — an explicit JSON `null` argument was treated as missing.** `ScriptCallSource` read arguments with the
   indexer, which returns null both for an absent member and for `null`; a call such as
   `rules.AddCondition(reference, comparison, value, backgroundHex: null, colorHex: "#FF0000")` was refused with
   "backgroundHex is required before colorHex", so optional colours and clearing values could not be passed. Present
   members are now told apart with `TryGetPropertyValue` and `null` is written into the call. Test:
   `Round185AgentTests`.
2. **Medium — a macro could run the document's database queries.** `app.Run("ledger.data.pivotRefresh")` passed
   `ScriptableCommandPolicy` (its id matches none of the refused fragments), and that command fetches external pivot
   sources, i.e. network or database access from a sandboxed macro. `Allows(CommandDefinition)` now also refuses every
   command marked `RunsDocumentQueries`, the same flag the agent already treats as needing confirmation. Refreshing a
   pivot from cell ranges stays available through `pivot.Refresh()`. Test: `Round185ScriptableCommandTests`.

## Open items (minor, not fixed)

- Ledger `sheet.Find` matches ignoring case while `sheet.Replace` matches case-sensitively; Folio documents its own
  difference, Ledger's summaries do not say it.
- Ledger `sheet.Freeze` runs through `Show`, so it is not refused on a read-only workbook (view state only).
- A script that stops at its time cap while blocked inside a host call may still finish that call after the agent step
  has closed; Jint's own timeout normally prevents it.
