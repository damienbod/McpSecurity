# MCP spec 2026-07-28 and C# SDK 2.x: what changed and how this repo adopts it

Sources: [spec changelog](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/docs/specification/2026-07-28/changelog.mdx), [C# SDK releases](https://github.com/modelcontextprotocol/csharp-sdk/releases), NuGet.

## 1. Spec changes (2025-11-25 -> 2026-07-28)

**Major**
- No protocol sessions: `Mcp-Session-Id` removed, list endpoints no longer vary per connection (SEP-2567).
- Stateless: no `initialize` handshake. Each request carries protocol version and client capabilities in `_meta` (SEP-2575).
- New `server/discover` RPC (versions, capabilities, identity). Version mismatch -> `UnsupportedProtocolVersionError`.
- `subscriptions/listen` replaces the HTTP GET stream and `resources/subscribe`.
- Removed: `ping`, `logging/setLevel`, `notifications/roots/list_changed`, SSE resumability (`Last-Event-ID`).
- Tasks moved to the extension `io.modelcontextprotocol/tasks` (polling, `tasks/update`).
- **Multi Round-Trip Requests (MRTR)** replace server-initiated `elicitation/create`, `sampling/createMessage`, `roots/list`: the server returns `resultType: "input_required"` with `inputRequests`; the client retries the call with `inputResponses` and `requestState` (SEP-2322).
- All results carry `resultType` (`complete` / `input_required`).

**Minor**
- `extensions` in capabilities; OpenTelemetry trace context in `_meta`; deterministic `tools/list` order.
- Required headers `Mcp-Method` / `Mcp-Name` (gateway routing/authz), `x-mcp-header` for tool params.
- `ttlMs` / `cacheScope` on list/read results.
- Resource not found is now `-32602`; MCP error codes renumbered (`-32020..-32022`).
- JSON Schema 2020-12 for `inputSchema`/`outputSchema`, `structuredContent` can be any JSON value.
- Elicitation completion notification and `elicitationId` removed (client learns by retry; server uses `requestState`).

**Security / authorization**
- Clients MUST validate `iss` (RFC 9207) before redeeming the code; servers SHOULD send it.
- Client credentials are bound to the issuer that issued them (persist per issuer, re-register on change).
- `application_type` required in DCR.
- **DCR deprecated** in favour of Client ID Metadata Documents (CIMD).

**Deprecated** (12 month window): Roots, Sampling, Logging; HTTP+SSE transport; `includeContext` `thisServer`/`allServers`; DCR.

## 2. Package versions

| Package | In repo before | Latest | Done |
|---|---|---|---|
| ModelContextProtocol / .AspNetCore | 1.3.0 | 2.4.0 | yes |
| ModelContextProtocol.Extensions.Tasks | - | 2.4.0 | not needed (no Tasks used) |
| Microsoft.Extensions.AI / .OpenAI / .Abstractions | 10.5.0 / 10.5.2 | 11.0.0 | yes (aligned) |
| Microsoft.Identity.Web (+UI, DownstreamApi) | 4.3.0 | 4.18.0 | yes |
| Microsoft.Identity.Client | 4.82.1 | 4.92.0 | yes |
| Microsoft.Extensions.* / AspNetCore auth (net10.0) | 10.0.3-10.0.7 | 10.0.15 (11.0.0 targets net11) | yes, 10.0.15 (SDK 2.4.0 needs >= 10.0.14) |

SDK highlights: 1.4.2 and 2.3.1 are security patches (OAuth PKCE/issuer validation, authenticated session hardening). 2.0.0 breaking changes: stateless HTTP by default (`SessionMode`), discovery-first with `initialize` fallback, Roots/Sampling/Logging APIs warn `MCP9005`, Tasks in separate package, `AuthorizationRedirectDelegate` -> `AuthorizationCallbackHandler` (`MCP9007`), RFC 9207/8414 issuer checks, PKCE `S256` must be advertised, non-object structured results no longer wrapped in `{result}`, `Tool.inputSchema` required. 2.1: `subscriptions/listen` handler. 2.2: `HttpServerSessionMode` (hybrid). 2.3: `McpClient.ServerInfo` nullable. 2.4: Skills extension, legacy OAuth metadata fallback needs explicit opt-in.

## 3. What was changed in this repo

| Project | Change |
|---|---|
| all csproj | package bumps above |
| ToolsLibrary `ElicitationHelper` + `DateTools`/`RandomNumberTools` | `McpServer.ElicitAsync` (fails on stateless sessions) replaced by MRTR: tool throws `InputRequiredException` with an elicitation request, and on retry reads `InputResponses`. Falls back to direct execution if the client has no elicitation capability or MRTR is unsupported (demo behaviour as before). |
| HttpMcpServer, UnsecureHttpMcpServer | explicit `SessionMode = StatefulForInitializeClients`: 2026-07-28 clients stateless, older clients (e.g. current hosts, MCP Inspector) still get sessions. Use `Stateless` once all clients are updated. |
| McpClient | removed deprecated `SamplingHandler` (MCP9005); elicitation handler unchanged. |

Verified: solution builds with 0 errors; unsecure server + SDK 2.4.0 client with elicitation, accept and decline, on both negotiated `2026-07-28` and pinned `2025-11-25`: elicitation handler called once, tool executed or declined correctly.

## 4. Not verified / still open
- **Secure server and Entra ID flows** (HttpMcpServer, McpClient with MSAL, McpWebClient with OIDC) need tenant secrets; only compile-verified. Check: discovery probe (`server/discover`) passes through `RequireAuthorization` / 401 handling and the client falls back correctly; bearer token still sent on every request.
- McpWebClient: elicitation via SignalR (`ElicitationCoordinator`) now runs inside the retry flow; test the approval UI end to end.
- MCP Inspector and other hosts: confirm support for 2026-07-28 (hybrid mode keeps them working via `initialize`).
- Entra ID is a pre-registered-client setup, so CIMD/DCR is not used; if you later use SDK OAuth (`ClientOAuthOptions`), use `AuthorizationCallbackHandler`, and verify the AS metadata advertises `S256` and returns `iss`.
- `.mcp/server.json` still references the draft 2025-07-09 schema.
- Pre-existing nullable warnings (`FunctionCallHelper`, `ElicitationCoordinator`, `PromptingService`) untouched.

## 5. Adopting for the talk
See [`slides/DWX-june-26/REVIEW.md`](../slides/DWX-june-26/REVIEW.md).
