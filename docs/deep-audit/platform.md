# Deep audit: platform, security, macros, agent, data

## Defects

### Severe — `page.ExportPdf` wrote files from a sandboxed macro (fixed)
`PrintFacade.ExportPdf` (src/PulseOffice.Sheets.UI/Scripting/PrintFacade.cs) wrote a PDF into any folder even
with `FileSystemWrite` false, even from a form control macro of a received document: the facade did not
know the permissions of the execution in progress.

Fix at the root: `IScriptExecutionPermissions` (src/PulseOffice.Scripting/ScriptExecutionPermissions.cs), a
singleton service with the permissions of the current execution in an `AsyncLocal` (two simultaneous executions, macro and
agent, do not see each other). Outside any execution the sandbox applies. `Enter(permissions)` opens the scope, `Demand(capability)`
throws `ScriptPermissionException`. The `PermissionScopedScriptEngine` decorator opens the scope with the permissions of the
`ScriptContext`: it is used by the macro editor (`MacroEditorLauncher`) and the agent (`ModuleAgentScripts`); the form
controls of Ledger (`FormEventRunner`) and Folio (`FolioFormEvents`) open the sandbox explicitly.
The agent now passes the tool's level to `IAgentScripts.RunAsync`: at the «files» level (with modal confirmation)
the script receives file read and write, otherwise the sandbox.

Facades that touch files, network or processes: surveyed in Sheets.UI/Scripting, Documents.UI/Scripting,
Presentations.UI/Scripting and in the PulseOffice.Scripting* projects: the only one is `PrintFacade.ExportPdf`. Every future
facade that touches files, network or processes must call `Demand`.

Declared consequence: from the macro editor (sandbox) `page.ExportPdf` no longer writes; there is not yet
a user-chosen widening of permissions (CLAUDE.md, decision 13).

Tests: `ScriptExecutionPermissionsTests` (Scripting.Tests), `page.ExportPdf` contract in `LedgerFacadeContractTests`
(before the fix the write in sandbox succeeded), `ScriptPermissionTests` (Agent.Tests).

### Medium D2 — JavaScript: a call to a facade that does not return held the caller (fixed)
`JavaScriptEngine.ExecuteAsync`: Jint's time cap is checked only between statements; a blocked CLR call
(facade stuck on the interface thread, printers) held editor, agent and form controls beyond the cap.
There is now the same watchdog as IronPython (cap + 2 s), and the time source lives inside the work.
Test `JavaScriptWatchdogTests` (before 9 s, now about 3 s).

### Medium D3 — CPython: an `except BaseException` in the loop cancelled the time cap and blocked the caller forever (fixed)
`CPythonScriptEngine.Run`: the interruption came from the trace function, which CPython **uninstalls** when it raises:
a loop swallowing `KeyboardInterrupt` ran forever and `ExecuteAsync` never returned. Now, once the time has expired,
`RunInterrupter` raises `KeyboardInterrupt` asynchronously (`PythonEngine.Interrupt`) every 50 ms until the execution
finishes (with a lock so as not to leave a pending interruption after the end; releasing the capture is repeated
if it reaches it), and the watchdog returns the expired time to the caller after cap + 2 s. The registration
on the token launches the interrupter in `Task.Run`: called inline it blocked on the GIL while the registration's `Dispose`
waited for it (deadlock found during the fix). Test `..._ferma_anche_chi_inghiotte_l_interruzione` (before: never finished).

### Medium D4 — `app.Run` reached external data, network and browser from a sandboxed macro (fixed)
`ScriptableCommandPolicy` (src/PulseOffice.Shell/CommandRecorder.cs) excluded file, send, print, but let through
`ledger.data.refresh`, `ledger.database.refresh`, `ledger.data.providerRefresh`, `ledger.data.provider`,
`ledger.data.xmlSource`, `folio.tools.updateLinks`, `folio.tools.updateAll`, `folio.mailMerge.*`, `folio.hyperlink.open`:
a macro of a received document could query databases and external URLs or open the browser. Added to the list.
A structural limit remains: the policy is an exclusion list by substring; a new command that leaves the
document must be added there (an attribute of `CommandDefinition` would be better). Tests in `CommandRecorderTests`.

### Severe D5 — standalone Python installable without checksum (fixed)
`StandalonePythonCatalog.Pick` / `StandalonePythonStore.InstallAsync` (src/PulseOffice.Scripting.CPython/StandalonePython.cs):
if `SHA256SUMS` was missing or did not list the archive, verification was skipped and an unverified native runtime was
installed (and then loaded into the process). Now an archive without a checksum is not offered and the store refuses an asset without a checksum;
only https addresses are accepted; a name without `+` (before, an unhandled `ArgumentOutOfRangeException`) or missing fields
are discarded; the download has a cap (announced size or 512 MB) and the partial file is deleted on error.
Tests in `StandalonePythonCatalogTests`.

### Medium D6 — the interpreter probe could hang without a cap (fixed)
`PythonInterpreterProbe.Run` (PythonDiscovery.cs) read stdout to the end before `WaitForExit(10 s)` and did not
read stderr: a `python3` in the PATH that does not terminate (or that fills stderr) blocked the search forever.
Now both reads are asynchronous and the 10 s cap really applies; an unreadable description (extra lines from
`sitecustomize`, broken JSON) comes back as an error instead of an exception. Test `Un_interprete_che_non_risponde_...`
(with the old code it waited for the script's 40 s; reasoned, not re-run on the old code).

### Minor D7 — the Python download progress stayed open on error (fixed)
`StandalonePythonStore.InstallAsync`: with a wrong checksum, network down or failed extraction the activity in the status
bar (`PythonDownloadConsent`) never reached 1 and stayed visible. The progress now always closes.
Test `StandalonePythonStoreTests`.

### Minor D8 — macro editor: output list touched by two threads (fixed)
`MacroEditorView.RunOnceAsync`: with the watchdogs an abandoned execution may still write to the output while
the interface thread composes the result; the list is now copied under the lock.

### Remaining (declared, not fixed)
- Minor: `MacroEditorView.Save` with a name already used by another macro overwrites it without asking.
- Minor: closing the editor during an execution does not stop it (it ends by itself within the cap).
- Minor: `ScriptOutput` routes output to the last open capture: with macro and agent together the output may end up
  in the wrong panel.
- Structural limit: IronPython and CPython are not sandboxed (decision 12/13): `IScriptExecutionPermissions` does not
  stop them if they write files directly with `System.IO` or `open()`; that is why they start only from the editor.
- `ScriptableCommandPolicy` is an exclusion list (see D4).

## Coverage (full reading of the PulseOffice.Scripting* projects)

| File | Lines read/total | Outcome |
|---|---|---|
| src/PulseOffice.Scripting.JavaScript/JavaScriptEngine.cs | 201/201 | medium defect fixed (D2) |
| src/PulseOffice.Scripting.Python/IronPythonScriptEngine.cs | 180/180 | declared limits (no sandbox nor memory cap; output without cap) |
| src/PulseOffice.Scripting.CPython/CPythonScriptEngine.cs | 178/178 | medium defect fixed (D3) |
| src/PulseOffice.Scripting.Editor/Facades/ScriptFacades.cs | 137/137 | `app.Run` leads to defect D4 (fixed in Shell) |
| src/PulseOffice.Scripting/ScriptEngine.cs | 101/101 | ok |
| src/PulseOffice.Scripting.CPython/PythonDiscovery.cs | 324/324 | D6 fixed |
| src/PulseOffice.Scripting.CPython/StandalonePython.cs | 211/211 | D5 fixed |
| src/PulseOffice.Scripting.CPython/CPythonHost.cs | 44/44 | ok |
| src/PulseOffice.Scripting.CPython/PythonRuntimeDescriptor.cs | 44/44 | ok |
| src/PulseOffice.Scripting.CPython/PythonProvisioning.cs | 17/17 | ok |
| src/PulseOffice.Scripting.CPython/PythonRuntimeLocator.cs | 7/7 | ok |
| src/PulseOffice.Scripting.Editor/PythonDownloadConsent.cs | 58/58 | D7 (fixed in the store) |
| src/PulseOffice.Scripting/ScriptOutput.cs | 47/47 | minor remaining |
| src/PulseOffice.Scripting/ScriptMessages.cs | 57/57 | ok |
| src/PulseOffice.Scripting/SourcePositions.cs | 23/23 | ok |
| src/PulseOffice.Scripting/PythonLanguage.cs | 36/36 | ok |
| src/PulseOffice.Scripting/CompletionSites.cs | 70/70 | ok |
| src/PulseOffice.Scripting/ScriptApi.cs | 86/86 | ok |
| src/PulseOffice.Scripting/ScriptTokenizer.cs | 233/233 | ok |
| src/PulseOffice.Scripting.Editor/ScriptCompletionData.cs | 32/32 | ok |
| src/PulseOffice.Scripting.Editor/ScriptDocumentation.cs | 69/69 | ok |
| src/PulseOffice.Scripting.Editor/ScriptApiComposition.cs | 113/113 | ok |
| src/PulseOffice.Scripting.Editor/MacroEditorLauncher.cs | 135/135 | wired to `PermissionScopedScriptEngine` |
| src/PulseOffice.Scripting.Editor/LoggedScriptEngine.cs | 41/41 | ok |
| src/PulseOffice.Scripting.Editor/MacroEditorView.cs | 690/690 | D8 fixed, two minor remaining |
| src/PulseOffice.Scripting.Editor/DocumentationPanel.cs | 192/192 | ok |

All files of the PulseOffice.Scripting* projects are read in full (plus the two new ones of the round).

## Live checks to do after the merge
- Ledger: form control macro with `page.ExportPdf('/tmp/x.pdf')`: it must fail without writing; from the macro
  editor the same (sandbox); from the agent at the «files» level, with confirmation, it must write.
- Macro editor, CPython: `while True:` with `try/except BaseException: pass` must stop with «time expired».
- `app.Run("ledger.database.refresh")` from a macro: refused.

## Round Security, Agent, Remote, Data (2026-11-01)

### Defects found
- **A1 severe** `src/PulseOffice.Presentations/Remote/RemoteServer.cs:130-200` (Deck remote control): after ten wrong PINs
  from *any* address the server answered «Locked» to everyone, even to the presenter with the right PIN: anyone
  on the same network turned off the remote control with ten requests. Fixed with `IRemoteAttemptThrottle`
  (`RemoteAttemptThrottle`, injected with `TimeProvider`): three free attempts per address, then exponential
  delay up to 5 minutes, only for that address, reset by the right PIN; response 429 with `Retry-After`.
  The access page now says «wrong PIN» and «too many attempts, try again in N seconds» (`remote.page.wrongPin`,
  `remote.page.wait`, five languages). Tests in `RemoteServerTests`.
- **A2 medium** `src/PulseOffice.Agent/Security/AgentAccessCode.cs:49-54`: an already existing code file with wide
  permissions (0644) was read and used without restricting it; it is now brought back to 0600 at every read and before every write.
  On Windows the file lives in the user's profile and inherits its ACLs (declared, not changed).
- **A3 medium** `src/PulseOffice.Agent/Tools/CommandTools.cs:26`: for `ledger.data.refresh`, `providerRefresh`, `provider`,
  `streams`, `externalLinks`, `xmlSource`, `folio.tools.updateLinks` (databases with keychain credentials,
  network, external files) the «edit» level was enough; they now require the «files» level.
- **A4 minor** `src/PulseOffice.Agent/Transport/AgentHttpServer.cs:253`: the `Host` was not checked explicitly
  (`HttpListener` already refused it for the 127.0.0.1 prefix); it is now refused in the code too, with a DNS rebinding test.
- **A5 medium** `src/PulseOffice.Agent/Security/AgentOperationLog.cs:38,63`: the summary of the arguments (up to 200
  characters of document content) also ended up in the application log, and `operations.jsonl` was created with the
  umask permissions (0644). The app log now reports only client, tool and outcome; the operations file is 0600.
- **A6 minor** `src/PulseOffice.Agent/Protocol/McpEndpoint.cs:94`: the client name (`clientInfo.name`) had neither a
  cap nor a filter for control characters and ends up in the status bar, settings and log; now at most 64 printable
  characters.
- **A7 medium** `src/PulseOffice.Security/OoxmlSigning.cs:468-476` and `XmlSignatures.cs:213-219`: the signature date was
  taken from the first `xd:SigningTime`/`mdssi:Value` *anywhere* in the signature (OOXML) or from references of a nested
  `SignedInfo` (ODF): whoever receives the file could add an unsigned `Object` with an arbitrary date, which was
  shown and used to judge the certificate. Now only from the nodes referenced by the real `SignedInfo`. Tests in
  `ForgedSignatureTests` and `OoxmlSignatureServiceTests`, failing before the fix.
- **A8 minor** `src/PulseOffice.Security/CertificateJudge.cs:32`: revocation never checked; now `Offline` (CRL and OCSP
  already in the system cache) with the «unknown» status ignored, so no network and no false negative.
- **A9 minor** `src/PulseOffice.Security/MacroSigning.cs:33`: with two macros of the same name the fingerprint depended
  on the reading order (valid signature seen as broken); full ordering by name, language, source. Fingerprints
  already signed with distinct names do not change.
- **A10 medium** `src/PulseOffice.Data/ReadOnlySqlPolicy.cs:12`: the policy for queries launched by a document did not
  know functions that write or execute arbitrary SQL inside a SELECT (`query_to_xml('DELETE … RETURNING …')`,
  `lo_from_bytea`, `pg_file_write`, `pg_reload_conf`, `dbms_sql`, `dbms_xmlgen`, `dbms_lob`, `fts3_tokenizer`…);
  added to the list, with tests.
- **A11 medium** `src/PulseOffice.Data.UI/ConnectionManagerView.cs:264,323`: the identifier of a new connection was
  the «slug» of the name: «Clienti 1» and «clienti-1» gave the same id and the new one *overwrote* the other, also inheriting
  its keychain key. `IConnectionIds` (`ConnectionIds`) now adds a free suffix.
- **A12 medium** `ConnectionManagerView.cs:141,214,215` and `DataImportView.cs:115,232,245`: save, test, remove and
  the table list ran «fire-and-forget» or in an `async void` handler that caught only `DbException`: a
  malformed connection string (`ArgumentException` from the builder) or a keychain error closed the app or
  disappeared silently. The error is now shown in the outcome box (`data.connections.error`, five languages).
- **A13 medium** `src/PulseOffice.Remote.Cloud/ICloudDriveStorage.cs:125-136`: containment in the iCloud
  Drive folder refused only `..` segments; on Windows a segment with `\` (`a\..\..`) or a rooted segment escaped
  the folder. The full path must now stay under the root.
- **A14 medium** `OneDriveStorage.cs:96`, `DropboxStorage.cs:75`, `GoogleDriveStorage.cs:237`: the list pages
  were followed as long as the service announced more, without a cap (a faulty service or a proxy blocked the
  window forever). Cap of 1000 pages, diagnostic `CLOUD032` in five languages, test.
- **A15 medium** `src/PulseOffice.Remote.UI/RemoteBrowserView.cs:408-449`: «Save» with a relative or malformed address
  (`new Uri(url, RelativeOrAbsolute).Host`) threw an exception in the click handler (app closed); renaming a
  store, the password stayed under the old name in the keychain (lost for the new one, orphaned for the old one); the
  password write was «fire-and-forget» and a keychain error disappeared. Now `Uri.TryCreate`, the password
  and the token follow the name, and errors reach the outcome box.
- **Remaining (declared, outside the area or not security)**: `src/PulseOffice.Document.Data/DataTableLoader.cs:51` catches
  only `DbException` (other driver errors go up to the caller; in the import window they are now
  intercepted, A12); `ConnectionSecretSplitter` removes to the keychain only `Password`/`Pwd`, not `SSL Password` or
  `Proxy Password` (they stay in the 0600 connections file); `TableEditorView` creates a box per cell (slow beyond
  a few thousand cells) and on confirmation turns all columns into text; `DataTableRenderer.RenderOwnLook` does not
  stop at the edge and looks up formats and merges linearly per cell.
- **Remaining, recorded decision (medium)** `CertificateJudge.cs:12`: the certificate is judged at the date *declared
  by the signer* (work log, ODF signature cross-checked with the reference suite). Without a trusted timestamp whoever owns an
  expired key can backdate. Not changed because it is a recorded choice covered by tests; it must be decided by the user.
  *Resolved afterwards by decision 22 (2026-11-03): without a trusted timestamp the certificate is judged at today's
  date (`SignatureStatus.UnverifiableInTime`).*

### Coverage
| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Presentations/Remote/RemoteServer.cs | 240/240 | A1 fixed |
| src/PulseOffice.Agent/Transport/AgentHttpServer.cs | 260/260 | A4 fixed; Origin and loopback ok, 4 MB cap ok |
| src/PulseOffice.Agent/Security/AgentAccessCode.cs | 74/74 | A2 fixed; constant-time comparison ok |
| src/PulseOffice.Agent/Protocol/McpEndpoint.cs | 183/183 | ok (JSON depth: limit 64 of `JsonNode.Parse`) |
| src/PulseOffice.Agent/Protocol/JsonRpc.cs | 97/97 | ok |
| src/PulseOffice.Agent/Security/AgentToolInvoker.cs | 86/86 | ok: level before confirmation, confirmation before execution |
| src/PulseOffice.Agent/Security/AgentSessions.cs | 87/87 | minor: sessions not limited in number (authenticated clients only) |
| src/PulseOffice.Agent/Tools/AgentTool.cs | 149/149 | ok |
| src/PulseOffice.Agent/Tools/CommandTools.cs | 150/150 | A3 fixed |
| src/PulseOffice.Agent/Tools/ScriptCatalogTools.cs | 173/173 | ok: arguments as JSON literals, sandbox with the tool's level |
| src/PulseOffice.Agent/Tools/DocumentTools.cs | 211/211 | ok |
| src/PulseOffice.Agent/Security/AgentPreferences.cs | 92/92 | ok (off and «read» by default) |
| src/PulseOffice.Agent/Security/AgentOperationLog.cs | 70/70 | A5 fixed; minor remaining: the file grows without rotation |
| src/PulseOffice.Agent/Hosting/AgentHost.cs | 86/86 | ok |
| src/PulseOffice.Agent/Transport/AgentServerController.cs | 47/47 | ok |
| src/PulseOffice.Agent/Tools/ViewTools.cs | 50/50 | minor remaining: width limited but not the height of very narrow pages |
| src/PulseOffice.Agent/Tools/AgentDocumentChoice.cs | 18/18 | ok |
| src/PulseOffice.Agent/Hosting/AgentModules.cs | 34/34 | ok |
| src/PulseOffice.Agent/Hosting/ModuleAgentScripts.cs | 35/35 | ok: always sandbox, files only at the «files» level |
| src/PulseOffice.Agent/Hosting/AgentClientInstructions.cs | 36/36 | ok |
| src/PulseOffice.Agent/AgentAccessLevel.cs | 8/8 | ok |
| src/PulseOffice.Agent.UI/ShellAgentWorkspace.cs | 198/198 | ok |
| src/PulseOffice.Agent.UI/DialogAgentConfirmation.cs | 31/31 | ok (modal with the window service's veil) |
| src/PulseOffice.Agent.UI/ShellAgentCommands.cs | 60/60 | ok |
| src/PulseOffice.Agent.UI/AgentServiceCollectionExtensions.cs | 63/63 | ok |
| src/PulseOffice.Agent.UI/AgentDocumentIds.cs | 18/18 | ok |
| src/PulseOffice.Agent.UI/AgentUiThread.cs | 17/17 | ok |
| src/PulseOffice.Agent.UI/AgentPageRenderer.cs | 36/36 | ok |
| src/PulseOffice.Agent.UI/ScriptApiAgentCatalog.cs | 10/10 | ok |
| src/PulseOffice.Agent.UI/AgentStatusIndicator.cs | 46/46 | ok (client name now shortened, A6) |
| src/PulseOffice.Agent.UI/AgentConnectionSettingEditor.cs | 224/224 | ok: code masked until one asks to see it |
| src/PulseOffice.Security/CertificateJudge.cs | 47/47 | A8 fixed; declared date: resolved by decision 22 |
| src/PulseOffice.Security/Signatures.cs | 94/94 | ok |
| src/PulseOffice.Security/MacroSigning.cs | 103/103 | A9 fixed |
| src/PulseOffice.Security/XmlSignatures.cs | 344/344 | A7 fixed; XXE and DTD closed, references only canonicalizations |
| src/PulseOffice.Security/PackageSigning.cs | 267/267 | ok; 512 MB cap per part |
| src/PulseOffice.Security/OpcPackage.cs | 190/190 | minor: `Load` does not limit the sum of the parts (only each one) |
| src/PulseOffice.Security/PdfSigning.cs | 439/439 | ok: revisions after the signature make it broken |
| src/PulseOffice.Security/OoxmlSigning.cs | 751/751 | A7 fixed |
| src/PulseOffice.Data/Connections.cs | 97/97 | ok |
| src/PulseOffice.Data/ConnectionSecrets.cs | 50/50 | minor remaining (other secret keys) |
| src/PulseOffice.Data/VaultCredentialStore.cs | 19/19 | ok |
| src/PulseOffice.Data/ReadOnlySqlPolicy.cs | 46/46 | A10 fixed; three dialects together, robust against comments and quotes |
| src/PulseOffice.Data/SqlWordReader.cs | 135/135 | ok |
| src/PulseOffice.Data/ConnectionDirectory.cs | 182/182 | ok: file 0600, secret in the keychain unless explicitly chosen |
| src/PulseOffice.Data/AdoDatabaseProvider.cs | 122/122 | ok: parameters as `DbParameter`, never concatenated |
| src/PulseOffice.Data/ConnectionIds.cs | 28/28 | new (A11) |
| src/PulseOffice.Data.MySql/MySqlDatabaseProvider.cs | 26/26 | ok: credentials with the driver's builder |
| src/PulseOffice.Data.Odbc/OdbcDatabaseProvider.cs | 44/44 | ok: `UID`/`PWD` with the ODBC builder (braces) |
| src/PulseOffice.Data.Oracle/OracleDatabaseProvider.cs | 37/37 | ok |
| src/PulseOffice.Data.PostgreSql/PostgreSqlDatabaseProvider.cs | 26/26 | ok |
| src/PulseOffice.Data.SqlServer/SqlServerDatabaseProvider.cs | 27/27 | ok |
| src/PulseOffice.Data.Sqlite/SqliteDatabaseProvider.cs | 26/26 | ok |
| src/PulseOffice.Data.UI/ConnectionManagerView.cs | 325/325 | A11, A12 fixed |
| src/PulseOffice.Data.UI/DataImportView.cs | 276/276 | A12 fixed |
| src/PulseOffice.Data.UI/DataImportFlow.cs | 59/59 | ok |
| src/PulseOffice.Data.UI/TableEditorView.cs | 527/527 | medium remaining (performance, column types) |
| src/PulseOffice.Data.UI/TabularPreview.cs | 206/206 | ok |
| src/PulseOffice.Data.UI/DataTableRenderer.cs | 277/277 | medium remaining (performance) |
| src/PulseOffice.Remote/BoundedResponse.cs | 39/39 | ok |
| src/PulseOffice.Remote/RemoteConnections.cs | 85/85 | ok |
| src/PulseOffice.Remote/FileRemoteConnectionDirectory.cs | 76/76 | minor: write errors swallowed (`IOException`), `UnauthorizedAccessException` not caught |
| src/PulseOffice.Remote/RemoteDocuments.cs | 51/51 | ok |
| src/PulseOffice.Remote/WebDavStorage.cs | 345/345 | ok: no cleartext Basic over http, XML without DTD, responses with a cap, lock token filtered; minor: «..» segments coming from the server not filtered (same host) |
| src/PulseOffice.Remote.Cloud/OAuthFlow.cs | 317/317 | ok: PKCE S256, `state` compared in constant time, only 127.0.0.1; minor: closing page in the browser only in English |
| src/PulseOffice.Remote.Cloud/CloudSessions.cs | 235/235 | ok (refresh token in the keychain); minor: `_live` read outside the semaphore |
| src/PulseOffice.Remote.Cloud/CloudApplications.cs | 82/82 | ok |
| src/PulseOffice.Remote.Cloud/ICloudDriveStorage.cs | 137/137 | A13 fixed |
| src/PulseOffice.Remote.Cloud/OneDriveStorage.cs | 149/149 | A14 fixed; `nextLink` only towards graph.microsoft.com |
| src/PulseOffice.Remote.Cloud/GoogleDriveStorage.cs | 278/278 | A14 fixed |
| src/PulseOffice.Remote.Cloud/DropboxStorage.cs | 143/143 | A14 fixed; JSON argument encoded |
| src/PulseOffice.Remote.UI/RemoteBrowserView.cs | 661/661 | A15 fixed |

### Outcome of the Security, Agent, Remote, Data round
All files of `PulseOffice.Security`, `PulseOffice.Agent`, `PulseOffice.Agent.UI`, `PulseOffice.Remote`,
`PulseOffice.Remote.Cloud`, `PulseOffice.Remote.UI` and `PulseOffice.Data*` (drivers and UI) read in full, plus
`PulseOffice.Presentations/Remote/RemoteServer.cs` and its page for the Deck remote control. Defects: 1 severe (A1),
11 medium (A2, A3, A5, A7, A10–A15), 4 minor (A4, A6, A8, A9), all fixed; remaining ones declared above. Tests run:
Agent, Security, Data, Remote, Presentations (only `RemoteServerTests`), Localization, Architecture.

### Live checks to do after the merge
- Deck › Remote control: from the phone get the PIN wrong four times (message «wrong PIN», then «try again in N
  seconds»); from a second phone with the right PIN one gets in immediately; the presenter is never locked out.
- Settings › Agent: with `operations.jsonl` and `access-code` set to 0644 by hand, reopen the app: they go back to 0600.
- Agent at the «edit» level: `run_command ledger.data.refresh` is refused and asks for the «files» level.
- Ledger › Data › Connections: save «Clienti 1» and then «clienti-1» (two distinct entries); malformed connection
  string + «Test» shows the error instead of closing the app; Import data with a malformed connection likewise.
- Open remote: new WebDAV store with address «server/dav» (relative) and «Save»: no closing; rename a
  store with a password and reopen it: the password is still there.
- A DOCX signed by us opened in Word and in the reference suite must stay valid (the date check changed only
  on reading).

## Round Platform, Text, Settings, Foundation, Localization, UI, Diagnostics
Surveyed perimeter (.cs files / lines): Platform 25/2160, Platform.Linux 4/161, Platform.MacOS 6/237,
Platform.Windows 5/466, Text 7/637, Text.Rich 21/2262, Text.Spelling 23/2736, Text.Spelling.Hunspell 14/1703,
Settings 7/740, Foundation 12/790, Localization 4/206, UI 4/374, UI.Commands 25/2198, UI.Icons 20/1463,
Diagnostics 2/164. Total about 16,300 lines.

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Platform/ExternalProcess.cs | 75/75 | ok: arguments in `ArgumentList`, stdout and stderr read in parallel (no deadlock), tree stopped on cancellation; the time cap is the caller's |
| src/PulseOffice.Platform/ShellOpening.cs | 62/62 | ok: only http/https; `xdg-open`/`open` with `ArgumentList` |
| src/PulseOffice.Platform/ExecutableFileTypes.cs | 30/30 | ok |
| src/PulseOffice.Platform/AtomicFileWriter.cs | 136/136 | B1 fixed |
| src/PulseOffice.Platform/TemporaryFiles.cs | 71/71 | B2 fixed |
| src/PulseOffice.Platform.Linux/SecretToolVault.cs | 36/36 | ok: secret on stdin, never in the arguments |
| src/PulseOffice.Platform.Linux/XdgMailComposer.cs | 46/46 | ok: recipients starting with «-» discarded |
| src/PulseOffice.Platform.MacOS/KeychainSecretVault.cs | 71/71 | ok: secret in hexadecimal on stdin (`security -i`), names with control characters refused |

### Defects
- **B1 (medium)** `AtomicFileWriter.cs:124`: the temporary file added 34 characters to the name; a name close to the
  255-byte limit of the file system opened but no longer saved («File name too long»). The temporary now keeps
  at most 64 characters of the name, without splitting a surrogate. Test `A_name_near_the_file_system_limit_still_saves`.
- **B2 (medium)** `TemporaryFiles.cs:37`: the shared root in `/tmp` was accepted by looking only at the permissions; a
  same-named folder created by another user (0700, not ours) or unreadable made `UnauthorizedAccessException` throw
  already in the constructor, i.e. at startup. The root is now tried (list and write) and, if it does not work, a new one is used.
  Test `UnaRadiceIlleggibileNonFaCadereLAvvio`.

| src/PulseOffice.Settings/JsonSettingsStore.cs | 148/148 | B3 fixed; corrupt, unreadable or forbidden file: falls back to factory values without crashing |
| src/PulseOffice.Settings/SettingsService.cs | 205/205 | ok: a value out of domain or of the wrong type goes back to the default (never null, `DefaultValue` is `required`) |
| src/PulseOffice.Settings/SettingDefinition.cs | 102/102 | ok: NaN and infinities refused, integers checked |

- **B3 (medium)** `JsonSettingsStore.cs:69`: the temporary was renamed over the settings without forcing it to
  disk; after a power failure the file could stay empty and the preferences all went back to factory. The
  temporary is now written with `Flush(flushToDisk: true)` before the `Move`. Not proven with a test (it needs cutting the
  power); declared.
- **B4 (minor, fixed)** `JsonSettingsStore.cs:119`: an unreadable settings file was overwritten at the
  first change without a backup copy. It is now kept as `settings.json.corrupt` before restarting from factory
  values. Test `UnFileIllegibileSiConservaPrimaDiEssereSovrascritto`.

## Round 2

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Settings/UserDataPaths.cs | 74/74 | B5 fixed |
| src/PulseOffice.Settings/Navigation/SettingsPages.cs | 108/108 | ok |
| src/PulseOffice.Settings/Navigation/SettingStates.cs | 79/79 | ok: numeric comparison between long and double |
| src/PulseOffice.Settings/Navigation/SettingOptions.cs | 24/24 | ok: duplicate names make `ToDictionary` fail at startup, but they are our data |

- **B5 (minor)** `UserDataPaths.cs:35,55`: relative `XDG_CONFIG_HOME`/`XDG_CACHE_HOME` were accepted and the
  configuration ended up in the current folder; the XDG specification requires ignoring them. Only absolute paths
  are now accepted.
| src/PulseOffice.Platform/VideoToolbox.cs | 279/279 | B6 fixed; session invalidated before unlocking the pinned frames, callback kept alive by `GCHandle` |
| src/PulseOffice.Platform/VideoEncoding.cs | 174/174 | ok: ffmpeg with `ArgumentList`, stderr read in parallel, tree stopped on cancellation, partial file discarded; the output path is the absolute temporary of `AtomicFileWriter`, so it cannot start with «-» |
| src/PulseOffice.Platform/Mp4Writer.cs | 191/191 | ok: cap at 4 GB of `mdat` and at `Array.MaxLength` |
| src/PulseOffice.Platform/MediaFoundationVideo.cs | 105/105 | B6 fixed (same defect); not testable on this Mac |
| src/PulseOffice.Platform/VideoFrameFitter.cs | 36/36 | ok: lengths in `long`, no overflow |
| src/PulseOffice.Platform/SampleTiming.cs | 28/28 | ok |
| src/PulseOffice.Platform/NativeConstants.cs | 41/41 | B7 fixed |

- **B6 (medium)** `VideoToolbox.cs:34`, `MediaFoundationVideo.cs:32`: encoding was synchronous inside a method
  `async` in name only, so it ran on the interface thread (video export froze the window for the whole
  encoding), ignored cancellation, and a write error (`IOException`, `UnauthorizedAccessException`) came out
  as a synchronous exception instead of as a failed result. Encoding now goes on `Task.Run`, checks the token at every
  frame and returns I/O errors as `VIDEO002`/`VIDEO001`. Test
  `UnaDestinazioneNonScrivibileDaUnErroreENonUnEccezione` (Shell.Tests).
- **B7 (minor)** `NativeConstants.cs:16`: symbol cache in a `Dictionary` shared by static instances; with
  encoding off the interface thread two concurrent exports could corrupt it. Now `ConcurrentDictionary`.
| src/PulseOffice.Platform/Printing.cs | 176/176 | B9 fixed; `lp`/`lpstat` with `ArgumentList` and `LC_ALL=C`; Windows `printto` with quotes removed from the name |
| src/PulseOffice.Platform/Sharing.cs | 149/149 | B8 fixed; `mailto:` with addresses and parameters encoded |
| src/PulseOffice.Platform/Secrets.cs | 133/133 | B8 fixed; secret on stdin; on cancellation the tree is now stopped and `Win32Exception` does not escape |
| src/PulseOffice.Platform/Scanning.cs | 114/114 | ok: `--output-file` as a separate argument |
| src/PulseOffice.Platform/ProcessSharing.cs | 32/32 | ok |

- **B8 (medium, security)** `Sharing.cs:65`, `Secrets.cs:127`: the search for executables in the `PATH` accepted relative
  entries (even empty ones, in `Secrets`), i.e. the current folder: a `security`, `secret-tool` or `ffmpeg` placed in the
  folder from which the app is started was executed, and secrets reached it instead of the keychain. Now a single search
  (`PathExecutableLocator.Resolve`) used by both, only absolute folders. Test `ExecutableLocatorTests` (Shell.Tests).
- **B9 (minor)** `Printing.cs:101`: the file to print went to `lp` as it was; a relative path starting with
  «-» would have been read as an option. The absolute path is now passed.
| src/PulseOffice.Platform.Windows/MapiMailComposer.cs | 193/193 | B10 fixed; limit: `MAPISendMail` ANSI, characters outside the system code page are lost (`MAPISendMailW` would be needed); `fsquirt` sends only the first file |
| src/PulseOffice.Platform.Windows/CredentialManagerVault.cs | 105/105 | B11 fixed; `CredFree` always in `finally`; limit: `service:account` is ambiguous if the service contains «:» (our names, they contain none) |
| src/PulseOffice.Platform.Windows/WiaScannerService.cs | 100/100 | B12 fixed; values quoted with `''` |
| src/PulseOffice.Platform.Windows/WindowsClipboardFormats.cs | 48/48 | B13 fixed |
| src/PulseOffice.Platform.Windows/WindowsWindowChrome.cs | 20/20 | ok |
| src/PulseOffice.Platform.MacOS/MailAppComposer.cs | 82/82 | ok: AppleScript with quotes and backslashes escaped, script as `-e` argument |
| src/PulseOffice.Platform.MacOS/MacActiveDisplayProbe.cs | 23/23 | ok: `CFRelease` of the display link |
| src/PulseOffice.Platform.MacOS/MacApplicationVisibility.cs | 36/36 | ok |
| src/PulseOffice.Platform.MacOS/MacClipboardFormats.cs | 5/5 | ok |
| src/PulseOffice.Platform.MacOS/MacWindowChrome.cs | 20/20 | ok |
| src/PulseOffice.Platform.Linux/LinuxClipboardFormats.cs | 5/5 | ok |
| src/PulseOffice.Platform.Linux/LinuxWindowChrome.cs | 74/74 | B14 fixed |

- **B10 (severe, Windows only)** `MapiMailComposer.cs:95`: after sending, the attachment descriptors were released with
  `DestroyStructure<MapiRecipDesc>`, i.e. with the recipients' layout: instead of the path, the `Position` field (−1)
  with the padding was released as a pointer, and the attachment name stayed allocated. A mail with an attachment
  could corrupt the native heap and bring the app down. `Release<T>` now uses the right type for each block. Not
  testable on this Mac: to be verified on Windows (Send by mail with an attachment).
- **B11 (minor, credentials)** `CredentialManagerVault.cs:29,64`: the secret stayed in cleartext in the array read and in the
  released native block. Both are now zeroed.
- **B12 (minor, Windows only)** `WiaScannerService.cs:87`: the script was passed as the text of `-Command`, where the
  command-line quoting rules of `powershell.exe` 5 for quotes and backslashes are not those of `ArgumentList`.
  Now `-EncodedCommand` (UTF-16LE in Base64), which interprets nothing.
- **B13 (medium)** `WindowsClipboardFormats.cs:16`: a copied HTML containing `<!--StartFragment-->` without
  `<!--EndFragment-->` made `ArgumentOutOfRangeException` (`document[..-1]`) throw on copy. The user's markers
  now count only if both are present, in order. Test `WindowsClipboardFormatsTests` (Shell.Tests, which now
  references `Platform.Windows`).
- **B14 (medium, Linux only)** `LinuxWindowChrome.cs:59`: synchronous `ReadToEnd` before `WaitForExit(2000)` made
  the two-second cap useless: a stuck `gsettings` (D-Bus not responding) blocked the window startup. Now
  stdout and stderr are read in parallel and the cap really applies.
| src/PulseOffice.Platform/ApplicationVisibility.cs | 29/29 | ok |
| src/PulseOffice.Platform/ClipboardFormats.cs | 54/54 | ok: UTF-16 BOM in both orders and UTF-8 recognized |
| src/PulseOffice.Platform/CloudFolders.cs | 27/27 | ok |
| src/PulseOffice.Platform/DictionaryDirectories.cs | 40/40 | B15 fixed |
| src/PulseOffice.Platform/DisplayReadiness.cs | 50/50 | ok: the clock starts with the gate, created in `Program.Main` right before the wait |
| src/PulseOffice.Platform/FileDialogs.cs | 56/56 | ok |
| src/PulseOffice.Platform/FontDirectories.cs | 40/40 | B15 fixed |
| src/PulseOffice.Platform/WindowChrome.cs | 32/32 | ok |

- **B15 (minor, Linux only)** `DictionaryDirectories.cs:38`, `FontDirectories.cs:37`: like B5, a relative `XDG_DATA_HOME`
  was used starting from the current folder. Now only absolute paths. Without test (it would touch a
  global environment variable between parallel tests).

With this `PulseOffice.Platform*` and `PulseOffice.Settings` are read in full.
| src/PulseOffice.Diagnostics/LogConfiguration.cs | 117/117 | B16 fixed; daily rotation and at 32 MB, 14 files and 30 days; no sensitive data written from here (depends on the callers) |
| src/PulseOffice.Diagnostics/LogPaths.cs | 47/47 | B17 fixed |
| src/PulseOffice.Foundation/Colors/ArgbColor.cs | 36/36 | B18 fixed |
| src/PulseOffice.Foundation/Diagnostics/OperationResult.cs | 48/48 | ok |
| src/PulseOffice.Foundation/Identity/UserIdentity.cs | 31/31 | B19 fixed |
| src/PulseOffice.Foundation/Modules/ModuleKind.cs | 45/45 | ok |
| src/PulseOffice.Foundation/Pages/PageSetup.cs | 75/75 | ok |
| src/PulseOffice.Foundation/Pages/PaperSize.cs | 45/45 | ok |
| src/PulseOffice.Foundation/Text/DelimitedText.cs | 238/238 | ok: trailing separator, merged separators, double quotes, CR LF inside fields, field left open until the end as in the reference suite |
| src/PulseOffice.Foundation/Review/AuthorPalette.cs | 25/25 | ok |
| src/PulseOffice.Foundation/Units/Geometry.cs | 35/35 | ok |
| src/PulseOffice.Foundation/Units/Measurements.cs | 52/52 | ok: since .NET 9 the conversion from double to integer saturates, so huge lengths or NaN do not wrap around |
| src/PulseOffice.Foundation/Windowing/WindowPlacement.cs | 102/102 | B20 fixed |
| src/PulseOffice.Foundation/Zoom/ViewZoom.cs | 58/58 | ok |

- **B16 (medium)** `LogConfiguration.cs:104`: writing the crash report caught only `IOException`; with the
  folder not writable an `UnauthorizedAccessException` escaped from the `UnobservedTaskException` handler (finalizer
  thread), turning an already observed exception into a process crash. It is now caught too and a warning is
  logged. Without test: `LogPaths` is static and reads a global environment variable.
- **B17 (minor, Linux only)** `LogPaths.cs:26`: relative `XDG_STATE_HOME` accepted (like B5).
- **B18 (minor)** `ArgbColor.cs:21`: `NumberStyles.HexNumber` allows leading and trailing spaces, so `" 12345"` or
  `"#12345 "` passed for six digits and gave a wrong colour. Now digits only. Test `GliSpaziNonSonoCifre`
  (Drawing.Tests).
- **B19 (minor)** `UserIdentity.cs:27`: the initial was taken as the first `char`, splitting letters outside the basic
  plane (emoji, mathematical alphabets) into a lone surrogate, then written into comments and revisions. Now the first
  text element. Test `UnaLetteraFuoriDalPianoBaseNonSiSpezza` (Shell.Tests).
- **B20 (minor)** `WindowPlacement.cs:80`: an infinite saved width or height passed as sane; without
  known screens the window opened like that. They must now be finite. Test
  `UnaLarghezzaInfinitaSenzaSchermiTornaAllaCollocazioneDiRiserva`.

With this `PulseOffice.Diagnostics` and `PulseOffice.Foundation` are also read in full.

### Round 3: text, language, dictionaries

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Text/TextCase.cs | 203/203 | B21 fixed; `TitleCase` and `ToUpper` follow the current culture, as in the reference word processor |
| src/PulseOffice.Text/AssemblyMarker.cs | 3/3 | ok |
| src/PulseOffice.Text/Input/PreeditComposition.cs | 26/26 | ok: selection and cursor limited to the text |
| src/PulseOffice.Text/Scripts/WidthConversion.cs | 135/135 | ok: half-width kana with (han)dakuten recomposed and decomposed |
| src/PulseOffice.Text/Scripts/KanaConversion.cs | 47/47 | ok |
| src/PulseOffice.Text/Scripts/TextConversionTables.cs | 161/161 | ok: surrogate pairs kept together, malformed rows skipped |
| src/PulseOffice.Text/Redaction/TextRedactor.cs | 62/62 | ok: expressions with a 1 s cap; on expiry everything is masked (errs on the side of excess, never leaving data) |
| src/PulseOffice.Localization/TextCatalog.cs | 94/94 | B22, B23 fixed |
| src/PulseOffice.Localization/UiLanguage.cs | 60/60 | ok |
| src/PulseOffice.Localization/DiagnosticTexts.cs | 36/36 | B22 fixed (the singular choice was duplicated: now only in `TextCatalog.Format`) |
| src/PulseOffice.Localization/LocalizationRegistration.cs | 16/16 | ok |
| src/PulseOffice.Localization/Resources/*.json | checked by script | ok: 7912 keys in each of the five languages, no duplicate, no empty value, equal placeholders; values equal to English or Italian are acronyms, units, proper names or truly identical words |
| src/PulseOffice.Text.Spelling.Hunspell/UserDictionaryFile.cs | 92/92 | ok |
| src/PulseOffice.Text.Spelling.Hunspell/HunspellDictionaries.cs | 121/121 | ok: scan with depth 8 and without links; an unreadable .aff is logged and not retried |
| src/PulseOffice.Text.Spelling.Hunspell/FileResourceProviders.cs | 129/129 | ok: proven that a looping link does not make the search run forever (guard test) |
| src/PulseOffice.Text.Spelling.Hunspell/DictionaryLocations.cs | 21/21 | ok |
| src/PulseOffice.Text.Spelling.Hunspell/DeclaredEncodingText.cs | 60/60 | ok |
| src/PulseOffice.Text.Spelling.Hunspell/Catalog/DictionaryArchive.cs | 106/106 | ok: caps on entries, entry and total; names with `..`, absolute or with `:` discarded |
| src/PulseOffice.Text.Spelling.Hunspell/Catalog/CachedDictionarySource.cs | 108/108 | ok |
| src/PulseOffice.Text.Spelling.Hunspell/Catalog/DictionaryHttp.cs | 63/63 | ok: HTTPS only, responses with a cap |
| src/PulseOffice.Text.Spelling.Hunspell/Catalog/MozillaDictionarySource.cs | 177/177 | ok: at most 20 pages, next page only on the same host |
| src/PulseOffice.Text.Spelling.Hunspell/Catalog/GitHubDictionarySource.cs | 255/255 | ok |
| src/PulseOffice.Text.Spelling.Hunspell/Catalog/DictionaryManager.cs | 131/131 | ok |
| src/PulseOffice.Text.Spelling.Hunspell/Catalog/DictionaryServiceOptions.cs | 85/85 | ok: replacement addresses HTTPS only |
| src/PulseOffice.Text.Spelling.Hunspell/Catalog/InstalledDictionaries.cs | 175/175 | ok: file names with folders refused, deletions only inside the user folder |
| src/PulseOffice.Text.Spelling.Hunspell/Catalog/ThesaurusSources.cs | 180/180 | ok: WordNet decompressed with a 512 MB cap |

- **B21 (minor)** `TextCase.cs:47,100`: sentence case and toggle case worked per `char`. A letter outside the
  basic plane (Deseret, mathematical alphabets, Adlam, Osage) was neither recognized nor changed, and sentence case
  skipped it, capitalizing the next letter («𐐨BC» → «𐐨Bc»). It now works per `Rune`. Tests
  `LaMaiuscolaDiFraseNonSaltaUnaLetteraFuoriDalPianoBase`, `IlRovesciamentoCambiaAncheLeLettereFuoriDalPianoBase`.
- **B22 (medium)** `TextCatalog.cs:69`, `DiagnosticTexts.cs:35`: to choose the singular a `ulong` counter went
  through `Convert.ToInt64`, which with a value beyond `long.MaxValue` throws `OverflowException`: formatting a
  text crashed. It now compares with 1 without conversions. Test `UnContatoreSenzaSegnoEnormeNonFaCadereLaFormattazione`.
- **B23 (minor)** `TextCatalog.cs:90`: exception message in Italian (rule 8). Now in English.

#### Text.Spelling

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Text.Spelling/Hyphenation.cs | 203/203 | B24, B25 fixed; exceptions and hyphenation points never inside a surrogate pair or before a combining mark |
| src/PulseOffice.Text.Spelling/Dictionaries/MyThesFiles.cs | 194/194 | ok: XML without DTD or resolver |
| src/PulseOffice.Text.Spelling/Thesaurus.cs | 90/90 | B26 fixed |
| src/PulseOffice.Text.Spelling/WordTokenizer.cs | 94/94 | ok: surrogate pairs and combining marks inside the word, links and addresses excluded |
| src/PulseOffice.Text.Spelling/Spelling.cs | 150/150 | ok |
| src/PulseOffice.Text.Spelling/Dictionaries/TexHyphenation.cs | 118/118 | ok |
| src/PulseOffice.Text.Spelling/Grammar.cs | 96/96 | ok: generated expressions without catastrophic backtracking (every repetition has a fixed boundary) |
| src/PulseOffice.Text.Spelling/AutoCorrect.cs | 451/451 | ok: letters outside the basic plane are not capitalized (almost none has an uppercase) but are not split |
| src/PulseOffice.Text.Spelling/TextLanguageResolver.cs | 19/19 | ok |
| src/PulseOffice.Text.Spelling/ShiftedReplacements.cs | 24/24 | ok |
| src/PulseOffice.Text.Spelling/WordBoundaryCorrection.cs | 63/63 | ok |
| src/PulseOffice.Text.Spelling/WordCompletion.cs | 78/78 | ok: at most 1000 words |
| src/PulseOffice.Text.Spelling/Dictionaries/DictionaryCodes.cs | 68/68 | ok |
| src/PulseOffice.Text.Spelling/Dictionaries/DictionaryLicenseClassifier.cs | 75/75 | ok: OR takes the freest, AND the most restrictive, according to the enumeration order |
| src/PulseOffice.Text.Spelling/Dictionaries/DictionaryModel.cs | 120/120 | ok |
| src/PulseOffice.Text.Spelling/AutoCorrection/AutoCorrectProfile.cs | 48/48 | ok |
| src/PulseOffice.Text.Spelling/AutoCorrection/AutoCorrectListImport.cs | 61/61 | ok: file, part and entries with a cap, DTD forbidden |
| src/PulseOffice.Text.Spelling/AutoCorrection/AutoCorrectDefaults.cs | 91/91 | ok |
| src/PulseOffice.Text.Spelling/AutoCorrection/AutoCorrectSources.cs | 194/194 | ok: JSON depth 8, keys and values with a cap |
| src/PulseOffice.Text.Spelling/AutoCorrection/AutoCorrectProfileCodec.cs | 134/134 | ok: duplicate keys and broken JSON caught by `AutoCorrectProfileStore`, which sets the file aside |
| src/PulseOffice.Text.Spelling/AutoCorrection/AutoCorrectDraft.cs | 138/138 | ok |
| src/PulseOffice.Text.Spelling/AutoCorrection/WordListDraft.cs | 109/109 | ok |
| src/PulseOffice.Text.Spelling/AutoCorrection/ReplacementListDraft.cs | 118/118 | ok |

- **B24 (severe)** `Hyphenation.cs:78`: for every position all substrings up to the end of the word were tried,
  allocating them: quadratic time. A long «word» without spaces (an address, a DNA sequence, pasted data)
  blocked the layout thread: 12,000 characters, 81 s. The tried length now stops at the longest pattern and the
  search uses a span without allocating: 60,000 characters in a few milliseconds. Test
  `UnaParolaLunghissimaSiSillabaInTempoLineare`.
- **B25 (minor)** `Hyphenation.cs:174`: `char.IsDigit` accepted non-Latin digits (Arabic-Indic, Devanagari…) as levels,
  with values like 1587; a pattern file with those characters invented hyphenation points. Now ASCII digits only. Test
  `UnaCifraNonLatinaNonDiventaUnLivelloDiSillabazione`.
- **B26 (minor)** `Thesaurus.cs:53`: a negative meaning count in a malformed `th_*.dat` never went back to zero
  and swallowed all following entries as meanings. A non-positive count now discards the entry. Test
  `UnConteggioNegativoNelDizionarioDeiSinonimiNonInghiotteIlResto`.

#### Text.Rich

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Text.Rich/RichText.cs | 316/316 | ok |
| src/PulseOffice.Text.Rich/TextClusters.cs | 97/97 | B29 (remaining, minor) |
| src/PulseOffice.Text.Rich/ParagraphLines.cs | 157/157 | ok: a line always advances by at least one character; positions never inside a surrogate pair |
| src/PulseOffice.Text.Rich/TextEditSplice.cs | 145/145 | ok: a field touched by the edit goes back to plain text |
| src/PulseOffice.Text.Rich/FirstLineBreaker.cs | 56/56 | B27 fixed |
| src/PulseOffice.Text.Rich/CaretRows.cs | 86/86 | ok |
| src/PulseOffice.Text.Rich/CaretWord.cs | 55/55 | ok |
| src/PulseOffice.Text.Rich/TextOffsetMap.cs | 33/33 | ok |
| src/PulseOffice.Text.Rich/ParagraphBreaks.cs | 19/19 | ok |
| src/PulseOffice.Text.Rich/AsianScriptRanges.cs | 56/56 | ok: spaces and tabs follow the neighbouring script |
| src/PulseOffice.Text.Rich/TabLayout.cs | 66/66 | ok: interval at least 1 point |
| src/PulseOffice.Text.Rich/TextLanguages.cs | 67/67 | ok |
| src/PulseOffice.Text.Rich/ListLabels.cs | 111/111 | ok: levels limited to 0–8 |
| src/PulseOffice.Text.Rich/RichTextInsertion.cs | 91/91 | ok |
| src/PulseOffice.Text.Rich/RichTextFormatter.cs | 506/506 | B28 fixed; sizes 6–300 pt, levels 0–8 |
| src/PulseOffice.Text.Rich/TextFields.cs | 122/122 | ok: invalid date pattern goes back to the short format |
| src/PulseOffice.Text.Rich/ColorRole.cs | 26/26 | ok |
| src/PulseOffice.Text.Rich/ParagraphFlowProperties.cs | 50/50 | ok |
| src/PulseOffice.Text.Rich/TextAttributes.cs | 52/52 | ok |
| src/PulseOffice.Text.Rich/TextFrame.cs | 67/67 | ok |
| src/PulseOffice.Text.Rich/BoxBorders.cs | 84/84 | ok |

- **B27 (medium)** `FirstLineBreaker.cs:40`: the only break points were after a space. A Chinese or Japanese paragraph
  (which does not use spaces) justified or with tabs, i.e. laid out by `ParagraphLineBreaker`, stayed a single line that
  overflowed the frame. A line can now break between two CJK characters (also outside the basic plane), with the minimal
  kinsoku rules: no closing punctuation, small kana or prolonged sound mark at line start, no opening bracket at
  line end, never before a combining mark or a variation selector. Tests `IlTestoCineseSenzaSpaziSiSpezzaFraGliIdeogrammi`,
  `LaPunteggiaturaDiChiusuraNonApreLaRigaGiapponese`, `UnIdeogrammaFuoriDalPianoBaseNonSiSpezza`,
  `IlSelettoreDiVarianteRestaConLIdeogramma`. It remains, by a choice already fixed by the tests, that a Latin word longer than the line
  does not break.
- **B28 (medium)** `RichTextFormatter.cs:277`: upper and lower case were applied run by run. With a format change mid
  sentence, sentence case restarted at every run («ciao **MONDO**» → «Ciao **Mondo**»), and capitalize-each-word split
  a word with two formats in two («pa*rola*» → «Pa*Rola*»). The conversion now sees the whole range (paragraphs separated by
  line breaks) and the result is redistributed over the runs; if the conversion changes the length it falls back to the old way. Tests
  `LaMaiuscolaDiFraseNonRiparteAOgniCambioDiFormato`, `LeIniziaiMaiuscoleNonSpezzanoUnaParolaInDueFormati`.
- **B29 (minor, remaining)** `TextClusters.cs:20`: the previous grapheme is searched starting from 64 characters before; a longer
  grapheme («zalgo» text with dozens of combining marks) makes the cursor stop inside it. No crash or loss.

#### UI and UI.Commands

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.UI/PulseOffice.UI.csproj | 16/16 | ok |
| src/PulseOffice.UI/Theming/DesignTokens.cs | 55/55 | ok |
| src/PulseOffice.UI/Theming/ColorContrast.cs | 75/75 | B30 fixed |
| src/PulseOffice.UI/Theming/Palette.cs | 121/121 | ok |
| src/PulseOffice.UI/Theming/ThemePreference.cs | 123/123 | ok (the accent is recomputed twice at every theme change: harmless) |
| src/PulseOffice.UI.Commands/CommandRegistry.cs | 543/543 | B31 fixed; concurrent registry, ordering under lock |
| src/PulseOffice.UI.Commands/Shortcuts/GestureChord.cs | 129/129 | B31 fixed |
| src/PulseOffice.UI.Commands/Shortcuts/GestureNotation.cs | 109/109 | ok |
| src/PulseOffice.UI.Commands/Shortcuts/ShortcutCatalog.cs | 118/118 | ok |
| src/PulseOffice.UI.Commands/Shortcuts/ShortcutDraft.cs | 118/118 | ok |
| src/PulseOffice.UI.Commands/Shortcuts/ShortcutScope.cs | 16/16 | ok |
| src/PulseOffice.UI.Commands/CommandDefinition.cs | 99/99 | ok |
| src/PulseOffice.UI.Commands/Shortcuts/ReservedShortcuts.cs | 55/55 | B32 (remaining) |
| src/PulseOffice.UI.Commands/Shortcuts/ShortcutCustomization.cs | 58/58 | ok: unreadable saved shortcuts discarded |
| src/PulseOffice.UI.Commands/Shortcuts/ShortcutSearch.cs | 37/37 | ok |
| src/PulseOffice.UI.Commands/CommandContext.cs | 62/62 | ok |
| src/PulseOffice.UI.Commands/CommandLayout.cs | 111/111 | ok |
| src/PulseOffice.UI.Commands/ContextLayout.cs | 83/83 | ok |
| src/PulseOffice.UI.Commands/MenuPlanner.cs | 90/90 | ok: the folding loop terminates (every step turns off a section) |
| src/PulseOffice.UI.Commands/RibbonProjection.cs | 92/92 | ok |
| src/PulseOffice.UI.Commands/CommandEffectGuard.cs | 11/11 | ok |
| src/PulseOffice.UI.Commands/PulseOffice.UI.Commands.csproj | 12/12 | ok |
| src/PulseOffice.UI.Commands/CommandPresentation.cs | 13/13 | ok |
| src/PulseOffice.UI.Commands/CommandOutcome.cs | 17/17 | ok |
| src/PulseOffice.UI.Commands/Ribbon/RibbonDisplayMap.cs | 26/26 | ok |
| src/PulseOffice.UI.Commands/RibbonModel.cs | 38/38 | ok |
| src/PulseOffice.UI.Commands/Ribbon/RibbonStripLayout.cs | 45/45 | ok |
| src/PulseOffice.UI.Commands/Ribbon/RibbonBandLayout.cs | 61/61 | ok: non-finite height treated as zero |
| src/PulseOffice.UI.Commands/Ribbon/RibbonReduction.cs | 67/67 | ok |
| src/PulseOffice.UI.Commands/Ribbon/RibbonPaletteBuilder.cs | 69/69 | ok |
| src/PulseOffice.UI.Commands/Ribbon/RibbonControls.cs | 131/131 | ok |

- **B30 (minor)** `ColorContrast.cs:30`: the blending step was an accumulated `double` (0.02 × 50 = 1.0000000000000007),
  so the last step, full white or black, was never tried and an accent could stay below the requested contrast
  while being able to reach it. The steps are now integers. Test `LaMescolaArrivaFinoAlNeroPieno` (UI.Tests).
- **B31 (medium)** `GestureChord.cs:42`, `CommandRegistry.cs:358`: two shortcut defects that masked each other.
  (1) The chord reader did not know the Avalonia names `OemMinus`, `OemPlus`, `OemComma`, `OemPeriod`,
  `OemQuestion`: in Deck «Ctrl+OemMinus» (optional hyphen) and «Ctrl+Shift+OemMinus» (non-breaking hyphen) were
  never recognized by the dispatcher, and were neither shown nor searchable in the shortcut editor. (2) The key
  with which the registry looks for conflicts and chooses the active shortcut compared the text, not the key: «Ctrl+Minus» and
  «Ctrl+OemMinus», or «Cmd+S» and «Ctrl+S», looked different. The key now goes through the same reader as the editor, and the Oem
  names are read. This surfaces the real conflict in Deck between «Zoom out» (Ctrl+−, as in Folio and Ledger) and the optional
  hyphen: zoom is kept the same in the three modules, the optional hyphen stays without a shortcut as in Folio, the
  non-breaking hyphen becomes «Ctrl+Shift+Minus» (outside the area, minimal: `src/PulseOffice.Presentations.UI/DeckCharacterCommands.cs`).
  Tests `LoStessoTastoScrittoInDueModiEUnConflitto`, `IlNomeAvaloniaDelTastoMenoSiLegge`.
- **B32 (minor, remaining, outside the area)** `ReservedShortcuts.cs:40`: on macOS ⌘Space is reserved (Spotlight) and the editor does not
  let it be assigned, but Ledger's «Select row» has it as its factory shortcut
  (`src/PulseOffice.Sheets.UI/LedgerSelectionCommands.cs:19`): on Mac it never arrives. Choosing another combination is an
  interface decision (Excel uses Shift+Space for the row) to be taken with the user.

#### UI.Icons

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.UI.Icons/PulseOffice.UI.Icons.csproj | 1/1 | ok |
| src/PulseOffice.UI.Icons/IconGeometry.cs | 8/8 | ok |
| src/PulseOffice.UI.Icons/PageIcons.cs | 18/18 | ok |
| src/PulseOffice.UI.Icons/DictionaryIconCatalog.cs | 19/19 | ok |
| src/PulseOffice.UI.Icons/CompositeIconCatalog.cs | 20/20 | ok |
| src/PulseOffice.UI.Icons/ChartIcons.cs | 21/21 | ok |
| src/PulseOffice.UI.Icons/SettingsIcons.cs | 21/21 | ok |
| src/PulseOffice.UI.Icons/TextFormatIcons.cs | 24/24 | ok |
| src/PulseOffice.UI.Icons/FormulaIcons.cs | 28/28 | ok |
| src/PulseOffice.UI.Icons/ToolIcons.cs | 30/30 | ok |
| src/PulseOffice.UI.Icons/ViewIcons.cs | 36/36 | ok |
| src/PulseOffice.UI.Icons/DataIcons.cs | 39/39 | ok |
| src/PulseOffice.UI.Icons/CellIcons.cs | 48/48 | ok |
| src/PulseOffice.UI.Icons/RibbonControlIcons.cs | 49/49 | ok |
| src/PulseOffice.UI.Icons/DrawingIcons.cs | 50/50 | ok |
| src/PulseOffice.UI.Icons/PanelIcons.cs | 52/52 | ok |
| src/PulseOffice.UI.Icons/ReviewIcons.cs | 71/71 | ok |
| src/PulseOffice.UI.Icons/DataDialogIcons.cs | 114/114 | ok |
| src/PulseOffice.UI.Icons/DeckIcons.cs | 199/199 | B33 fixed |
| src/PulseOffice.UI.Icons/IconCatalog.cs | 234/234 | B33 fixed |
| src/PulseOffice.UI.Icons/FolioIcons.cs | 382/382 | ok |

Script check of all 1203 paths (relative and absolute commands, curve control points included):
valid syntax and no point outside the 24×24 grid.

- **B33 (minor)** `DeckIcons.cs:171`: «deck.present» was defined twice, here (a screen) and in `IconCatalog.cs:121`
  (a play triangle). The composite catalogue takes the first, so Deck's shape was dead code. The test
  `NessunaChiaveCompareInDueCataloghi` did not see it because its list of catalogues omitted `DeckIcons`, `FolioIcons`,
  `SettingsIcons` and `TextFormatIcons`: it now includes them all and the dead entry is removed (the visible drawing does not change).

## Overall outcome of the platform

All assigned projects read in full: Platform, Platform.Linux, Platform.MacOS, Platform.Windows, Settings,
Foundation, Diagnostics (rounds 1–2), Text, Text.Rich, Text.Spelling, Text.Spelling.Hunspell, Localization (code; the five
languages checked by script), UI, UI.Commands, UI.Icons (round 3). About 16,300 lines.

Defects: 33 (B1–B33). Severe 2 (B10 MAPI on Windows, B24 quadratic hyphenation); medium 13 (B1, B2, B3, B6, B8, B13, B14,
B16, B22, B27, B28, B31 and B4 counted among the minor ones); minor the rest. All fixed except B29 (graphemes longer than 64
characters) and B32 (factory ⌘Space in Ledger, interface decision, outside the area), declared.

From round 3: no catastrophic regexes in the dictionaries (the grammar expressions have fixed boundaries, the
redaction ones a time cap); dictionary archives and downloads with caps on entries, sizes and decompression, names with
`..` refused, HTTPS only; a malformed `.aff`, `hyph_*.dic` or `th_*.dat` file brings nothing down. Text catalogue:
7912 identical keys in it, en, de, es, fr, no empty value, consistent placeholders.

Tests run one at a time, all green: Text.Tests 32, Localization.Tests 32, Text.Spelling.Tests 160, Text.Rich.Tests
93, UI.Tests 26, UI.Commands.Tests 91, Shell.Tests (shortcuts) 17, Architecture.Tests 38.

### Suggested live checks
- Deck, text being edited: Ctrl+Shift+− inserts the non-breaking hyphen; Ctrl+− zooms out (B31).
- Deck: justified text box with a Chinese or Japanese paragraph without spaces: it must wrap inside the
  frame, without closing punctuation at line start (B27).
- Folio or Deck: «Change case → Sentence» on a sentence with a bold word in the middle (B28).
- Folio: a paragraph with hyphenation active and a pasted «word» of tens of thousands of characters: no blocking (B24).
- Ledger or Deck on Windows: «Send by mail» with an attachment (B10), mp4 video export (B6).
- Deck on macOS: video export of many slides; the window must stay responsive and «Cancel» must stop (B6).
- Startup on Linux with `gsettings` missing or stuck (B14).
