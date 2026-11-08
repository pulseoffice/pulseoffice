# Round 84: the agent server, its security and lifecycle, localized tool titles

Scope: what round 81 left unread (`AgentHttpServer`, `AgentSessions`, `AgentPreferences`, `AgentAccessCode`,
`AgentOperationLog`, `JsonRpc`, `AgentClientInstructions`, `AgentServerController`, the Agent.UI settings editor,
indicator, page renderer and composition, `FolioScripting` beyond the catalog, the other Ledger facades) and round 81's
limit: undo labels and confirmations showing technical tool names.

## Coverage
| File | Lines read | Result |
|---|---|---|
| Agent/Transport/AgentHttpServer.cs | 261/261 | see defects (lifecycle) |
| Agent/Security/AgentAccessCode.cs | 81/81 | see defects (length timing, platform checks) |
| Agent/Security/AgentSessions.cs | 145/145 | ok |
| Agent/Security/AgentPreferences.cs | 92/92 | ok |
| Agent/Security/AgentOperationLog.cs | 85/85 | see defects (document text in the log) |
| Agent/Protocol/JsonRpc.cs | 101/101 | see defects |
| Agent/Hosting/AgentClientInstructions.cs | 36/36 | ok |
| Agent/Transport/AgentServerController.cs | 47/47 | see defects |
| Agent.UI/AgentServiceCollectionExtensions.cs | 66/66 | ok |
| Agent.UI/AgentStatusIndicator.cs | 46/46 | ok |
| Agent.UI/AgentPageRenderer.cs | 36/36 | see defects |
| Agent.UI/ShellAgentWorkspace.cs | 208/208 (re-read) | undo label |
| Agent/Tools/ScriptCatalogTools.cs | 179/179 (re-read) | undo label |
| Scripting/ScriptApi.cs, Scripting.Editor/ScriptApiComposition.cs | 88/88, 135/135 | title added |
| Agent.UI/AgentConnectionSettingEditor.cs | 224/224 | operations list showed tool names (title now) |
| Agent.UI/AgentUiThread.cs | 17/17 | ok |
| Agent.UI/AgentDocumentIds.cs | 18/18 | ok |
| Sheets.UI/Scripting/DocumentFacade.cs | 259/259 | ok |
| Sheets.UI/Scripting/FilterFacade.cs | 109/109 | ok |
| Sheets.UI/Scripting/ScenarioFacade.cs | 64/64 | ok |
| Sheets.UI/Scripting/PivotFacade.cs | 169/169 | ok |
| Sheets.UI/Scripting/ScriptGlobals.cs | 42/42 | ok |
| Sheets.UI/Scripting/LedgerScriptHost.cs | 19/19 | ok |
| Sheets.UI/Scripting/PrintFacade.cs | 207/207 | ok |
| Sheets.UI/Scripting/ScriptProtection.cs | 51/51 | ok |
| Sheets.UI/Scripting/ScriptReferences.cs | 44/44 | ok |
| Sheets.UI/Scripting/ScriptSheetEdits.cs | 53/53 | ok |
| Sheets.UI/Scripting/FormatRuleFacade.cs | 186/186 | ok |
| Sheets.UI/Scripting/ValidationFacade.cs | 153/153 | ok |
| Documents.UI/Scripting/FolioScripting.cs | 500/500 | ok (title keys added to the catalog) |
| Documents.UI/Scripting/FolioMacros.cs | 164/164 | ok |

## Checks that held
- Authorization: Bearer code compared in fixed time; loopback-only remote end point, Host and Origin checks against DNS
  rebinding; 404 before authorization only for other paths; bodies capped at 4 MB both by header and while reading.
- Sessions: 16 at most, idle expiry 10 minutes, 4 requests in flight per session and 16 in all; `Clear` on stop.
- JSON-RPC: parse errors, non-object bodies (batches are refused as MCP 2025-06 dropped them), depth over 64, duplicate
  members, non-string methods, non-object params all give a JSON-RPC error; huge numeric ids are echoed unchanged.
- Lifecycle: a port in use leaves the server off with the listener's reason shown in the settings card (verified on
  macOS); stop then start on the same port works; app quit disposes the server through the service provider.
- The general log (`ILogger`) records client, tool and outcome only; the access code is never logged; the settings card
  masks the code and the Claude Code command until «Show».

## Defects
1. Medium, privacy: the operation log (`operations.jsonl` and the settings card's recent list) stored the first 200
   characters of each call's arguments, so cell values, paragraph texts and slide notes written by the agent landed on
   disk; a declined confirmation stored its subject (the argument JSON). Now the detail records only the shape
   (`reference: text(2), value: text(27)`). Test `AgentServerHardeningTests.The_operation_log_records_the_shape_of_the_arguments_never_the_document_text` (failed before).
2. Medium, round 81's limit: undo steps and confirmations named the technical tool (`Agente: ledger_sheet_Write`,
   «Strumento "ledger_sheet_SetTracking"»). Every catalog member now has a short title `api.{type}.{Member}.title` in the
   five languages (186 members), carried by `ApiSymbol.Title`; `AgentTool.Title` is used for the undo label, the
   confirmation and the operation log; the fixed tools have `agent.tool.{name}` titles resolved by `IAgentToolTitles`.
   Tests `The_undo_step_and_the_confirmation_name_the_member_by_its_title_not_by_the_tool_name` (failed before) and
   `ScriptApiCatalogTests.Every_member_has_a_short_title_in_the_five_languages_for_undo_labels_and_confirmations`.
3. Medium, rule 15 and Windows security: the access code and the operation log were made private with
   `OperatingSystem.IsWindows()` branches inside `PulseOffice.Agent`, and on Windows nothing restricted them (inherited
   ACL). New `IPrivateFiles` in `PulseOffice.Platform` with `UnixPrivateFiles` (0700 folders, 0600 files) and
   `WindowsPrivateFiles` (protected DACL, owner only), chosen by `PrivateFilesFactory`. Test
   `The_access_code_and_the_operation_log_are_written_through_the_private_file_store`; the Unix mode tests still hold.
   Windows ACL not run on a real Windows.
4. Minor, JSON-RPC: a request with `"id": null` was taken for a notification and answered with 202 and no body, so
   the client waited forever. Now Invalid Request. Test `A_request_whose_id_is_null_is_refused_instead_of_being_taken_for_a_notification` (failed before).
5. Minor, `AgentPageRenderer`: the picture height was page height × scale without a bound, so a page with an extreme
   proportion (or zero width read from a file) asked for a bitmap of millions of pixels or a division by zero. Now
   `IAgentPictureFrame` fits the page in the requested width and at most 6000 pixels high. Tests
   `A_degenerate_page_gives_a_bounded_picture`, `An_ordinary_page_keeps_the_requested_width_and_its_proportions`.
6. Minor, `AgentServerController`: `Start` twice subscribed twice, and an exception while applying the preferences
   was lost in an unobserved task. Now it subscribes once, logs failures and exposes `Settled` (tests await it instead of
   sleeping). Test `The_controller_reports_when_the_preferences_have_been_applied_and_subscribes_once`.
7. Minor, timing: `FixedTimeEquals` returns at once when lengths differ, revealing the code's length; both sides are
   now SHA-256 digests. Test `Two_codes_of_different_length_are_compared_without_an_early_exit_on_length`.

## Remaining
- `FileConnectionDirectory` (Data) and `DocumentRecovery` (Shell) still restrict their files with `OperatingSystem`
  branches; they can move to `IPrivateFiles` (outside this round's area).
- `AgentHttpServer.StopAsync` does not wait for requests already being served; they finish against a closed listener
  (errors are caught). Harmless at quit, noted.
- The `*_properties` read tools keep their technical name in the operations list.
- Live checks (coordinator): undo menu after an agent `ledger_sheet_Write` shows «Agente: Scrivi cella»; a
  `folio_doc_SetTrackChanges` confirmation names «Registra modifiche»; on Windows the access-code file's ACL lists only
  the user.
