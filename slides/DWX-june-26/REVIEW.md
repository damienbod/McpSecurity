# Review: "MCP in .NET - From Zero to Hero" vs. MCP spec 2026-07-28

Review only, the deck is unchanged. Slide numbers refer to `DWX-MCP-2026.pptx` (52 slides). Details: [docs/MCP-2026-07-28-upgrade.md](../../docs/MCP-2026-07-28-upgrade.md).

## Storyline: what changes overall
1. Slide 29 shows "MCP Draft 2026-07-28". It is now **final** (released 2026-07-28), so the draft label must go and it becomes the baseline of the talk.
2. The elicitation story (slides 32-37) is the biggest one: the server no longer calls the client (`elicitation/create`). It returns "input required" and the client **retries** (MRTR). Same human-in-the-loop idea, different mechanics.
3. The security story (slides 38-47) gets stronger: stateless (no session id to hijack), RFC 9207 `iss` validation, PKCE S256 enforced by the SDK, issuer-bound credentials, CIMD replacing DCR.
4. Add a short "what's new in the spec / SDK 2.x" section between MCP basics and demos, or in the recap.

## Slide by slide

| Slide(s) | Topic | Adjust |
|---|---|---|
| 15 | SDK packages | Show 2.x (2.4.0). Mention extension packages: `Extensions.Tasks`, Apps, Skills. |
| 24 | Client-server / transport ("SSE for streaming") | Streamable HTTP is stateless: no `initialize`, no `Mcp-Session-Id`, `server/discover`, per-request `_meta`. HTTP+SSE is deprecated. Streaming notifications = `subscriptions/listen`. SSE resumability gone. |
| 25, 33 | MCP flow / elicitation flow diagram | Replace the server -> client request arrow with MRTR: tool call -> `input_required` -> client asks user -> retry with `inputResponses` (+ `requestState`). |
| 29 | Capabilities | Remove "Draft". Mark **Roots, Sampling, Logging as deprecated**. Remove ping. Tasks = extension. Add `extensions`, caching (`ttlMs`/`cacheScope`), deterministic tool list. Elicitation stays (via MRTR). Utilities listing (Completion, Logging, Pagination) needs review. |
| 34 | Server elicitation code | Replace `ElicitAsync` with `InputRequiredException` + `InputRequest.ForElicitation`, read `InputResponses` on retry, check `IsMrtrSupported`. See `src/ToolsLibrary/Tools/ElicitationHelper.cs`. Mention `ElicitAsync` throws on stateless servers. |
| 35-36 | Client elicitation / handler | Handler API unchanged; explain that the SDK now resolves input requests and retries automatically. Remove any sampling handler. |
| 31 | MCP Inspector | Check Inspector version supports 2026-07-28; otherwise explain hybrid mode (`StatefulForInitializeClients`). |
| 39 | Best practices | Add: validate `iss`, issuer-bound client credentials, PKCE S256 required, `Mcp-Method`/`Mcp-Name` headers allow gateway-level authorization. |
| 40, 47 | Entra ID demo setup | Unchanged architecture (pre-registered clients). Note that Entra does not use DCR/CIMD; mention CIMD as the future of client registration. |
| 41-43 | Server OAuth security | Still valid. Add stateless note: every request is authorized independently (matches the per-request bearer validation already shown). |
| 44-45 | Confidential OIDC client | Unchanged. If SDK built-in OAuth is shown: `AuthorizationCallbackHandler`, issuer + PKCE checks. |
| 49-50 | Recap | Add upgrade checklist: stateless default, MRTR instead of server requests, deprecations (`MCP9005`), PKCE/issuer validation, security patches 1.4.2/2.3.1. |
| 51-52 | Links / references | Add spec 2026-07-28 changelog, C# SDK 2.x docs (MRTR, stateless), SDK release notes. README spec links now point to 2026-07-28. |

## Demos: risks and checks (all projects)

| Demo | Project | Risk | Status |
|---|---|---|---|
| Local function calling (slides 21-22) | McpWebClient (local tools), McpClient | None, no MCP involved. MEAI 10 -> 11 bump only. | builds |
| Unsecure remote MCP, tools (slide 30) | UnsecureHttpMcpServer + McpClient | Now stateless by default; hybrid mode set. | tested with SDK client |
| Elicitation human-in-the-loop, console (37) | McpClient | Server must use MRTR. | tested, accept + decline, both revisions |
| Elicitation, web with SignalR (37) | McpWebClient | Handler now runs within retry; timeouts/UI flow. | **to test manually** |
| Secure remote MCP, desktop MSAL (46) | HttpMcpServer + McpClient | `server/discover` probe vs. auth (401 handling). | **to test with Entra tenant** |
| Secure remote MCP, web OIDC (46) | HttpMcpServer + McpWebClient | Same, plus token per request. | **to test with Entra tenant** |
| MCP Inspector (31) | both servers | Inspector support for 2026-07-28. | **to test** |

## Suggested new content (optional, 2-3 slides)
- "What's new in MCP 2026-07-28": stateless, MRTR, extensions, deprecations.
- "Stateless = easier to secure and scale": no session store, round-robin load balancing, header-based routing/authz at gateways.
- "OAuth hardening": `iss` (RFC 9207), PKCE S256, issuer-bound credentials, CIMD vs DCR.
