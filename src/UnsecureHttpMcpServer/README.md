# Unsecure HTTP MCP server

This is an unauthenticated MCP server using the Streamable HTTP transport. Run it
locally from the repository root:

```powershell
dotnet run --project src\UnsecureHttpMcpServer --launch-profile http
```

In another terminal, start the MCP Inspector:

```powershell
npx @modelcontextprotocol/inspector
```

In the Inspector, select **Streamable HTTP** as the transport and connect to
`http://localhost:3001/mcp`. You can then list tools and invoke
`get_random_number` or `get_all_customers`, for example.

The server is an HTTP server, not a stdio server: do not configure Inspector to
launch it as a stdio command.

## Using HTTPS with Inspector on Windows

Start the server with its HTTPS profile:

```powershell
dotnet run --project src\UnsecureHttpMcpServer --launch-profile https
```

Check that the ASP.NET Core development certificate is trusted with
`dotnet dev-certs https --check --trust`. If it is not trusted, run
`dotnet dev-certs https --trust` and approve the trust prompt.

Inspector runs on Node.js, which may not use the Windows certificate store by
default. With Node.js 24, start a new Inspector process from PowerShell with:

```powershell
$env:NODE_OPTIONS = "--use-system-ca"
npx @modelcontextprotocol/inspector
```

Select **Streamable HTTP** and connect to `https://localhost:7133/mcp`. Restart
Inspector after changing `NODE_OPTIONS`; the setting only affects processes
started from that terminal.

This server has no authentication, including for the sales tools, and is
intended for local demos only. Do not expose it to untrusted networks.
