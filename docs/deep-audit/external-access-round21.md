# Deep audit round 21: external access across the suite (security)

Scope: every place where the suite opens the network or a file because a document's content asks for it.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.UI/WebResourceAccess.cs | 78/78 | defect 1 fixed |
| src/PulseOffice.Sheets.UI/ExternalData/ExternalDataSources.cs | 93/93 | defects 2, 3 fixed |
| src/PulseOffice.Sheets.UI/ExternalData/ExternalLinkAutoRefresh.cs | 158/158 | clean: refresh runs only for workbooks the user trusted (`ExternalLinkSchedule.Trust`), never on open by itself |
| src/PulseOffice.Presentations.UI/DeckMediaFiles.cs | 37/37 | defect 4 fixed |
| src/PulseOffice.Formats/LinkedFiles.cs | 59/59 | clean: `Load` has no caller; linked images, sub-documents and media are kept as links, never fetched while reading |
| src/PulseOffice.Formats.Interchange/Text/Html/HtmlDocumentReader.cs (images, links) | 440-500 | clean: only `data:` images are decoded; remote `src` only sets a flag, nothing is fetched |
| src/PulseOffice.Agent/Transport/AgentHttpServer.cs | 261/261 | clean: binds 127.0.0.1 only, checks remote loopback, `Host` must be a loopback literal or `localhost` (defeats DNS rebinding), loopback `Origin` only, bearer access code, 4 MB body cap |
| src/PulseOffice.Desktop/Composition/PlatformIntegrationComposition.cs | 66/66 | wiring updated |

Not content-driven (user-initiated, fixed or user-configured hosts), noted only: online font catalogs
(`PulseOffice.Typography/Sources`), dictionary catalog (`DictionaryHttp`), standalone Python download, cloud and WebDAV
storage, database drivers (connection strings typed by the user, credentials in the OS store). No DDE implementation
exists. Remote templates and fonts are not fetched from documents.

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | Severe | src/PulseOffice.Sheets.UI/WebResourceAccess.cs:16-23 | With web access on, `WEBSERVICE` in a document reached loopback, private networks and link-local addresses (cloud metadata 169.254.169.254), directly or through a host name, a redirect, or DNS rebinding; it also used the system proxy | `IOutboundHttpClients.Create(DocumentContent)`: public addresses only, checked on the IP actually connected for every connection (redirects included), no proxy, no cookies, no credentials |
| 2 | Severe | src/PulseOffice.Sheets.UI/ExternalData/ExternalDataSources.cs:28-50 | A link to external data as `\\host\share\x.csv` or `file://host/share/x.csv` was probed with `File.Exists`, which on Windows sends the user's NTLM hash to that host | `INetworkSharePaths` refuses network shares (also `//host/...`) with `UnauthorizedAccessException` before any file access |
| 3 | Medium | src/PulseOffice.Sheets.UI/ExternalData/ExternalDataSources.cs:13 | External data used the shared `HttpClient` with no address policy: a link could reach the link-local metadata address | own client with `UserChosen` policy: public, private and loopback (the user may query an intranet or local server), never link-local, multicast, unspecified or reserved |
| 4 | Medium | src/PulseOffice.Presentations.UI/DeckMediaFiles.cs:25-37 | Playing a linked medium on a network share probed it with `File.Exists` (NTLM leak on Windows) | network shares become `BlockedMediaFile` (existing warning text, five languages) |

## Shared component

`PulseOffice.Foundation.Network` (Foundation so that Formats and every module can use it): `IAddressClassifier`
(IPv4, IPv6, IPv4-mapped, NAT64 and 6to4 embedded addresses), the `IDestinationPolicy` strategies `PublicInternetPolicy` and
`UserChosenDestinationPolicy`, `IOutboundHttpClients` (factory of `HttpClient` over a `SocketsHttpHandler` whose
`ConnectCallback` resolves the host, filters the addresses and verifies the connected endpoint),
`INetworkSharePaths`. Registered as singletons in `PlatformIntegrationComposition`.

## Tests

`tests/PulseOffice.Sheets.UI.Tests/DeepAuditExternalAccessRound21Tests.cs` (local TCP server on loopback, no internet):
the two WEBSERVICE tests failed before the fix (content returned); network share, metadata address, user-chosen
loopback still allowed, address classification. `tests/PulseOffice.Presentations.UI.Tests/DeepAuditExternalAccessRound21Tests.cs`
for media on shares. Run: Sheets.UI (filtered, 23 + 17), Presentations.UI (filtered, 6), Architecture (38), Desktop build.

## Remaining / to try live

- On a real Windows: an ODS with a linked data area on `\\host\share` must show the failure in the status bar and no SMB
  traffic must leave the machine.
- With web access on, `=WEBSERVICE("http://127.0.0.1:…")` and `=WEBSERVICE("http://169.254.169.254/")` must give `#VALUE!`;
  a public URL must still work.
- The agent server was not changed; on Windows and Linux it is still untested (open point).
