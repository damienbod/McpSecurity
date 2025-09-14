# MCP Security

Research MCP, OAuth, security, authorization

## Definitions

- MCP: a standard integration layer for accessing tools
- MCP Client: used by an agent
- MCP Server: implementation of MCP standard for access to a resource
- Local data src: local data
  - authn, authz, trust, data protection
- Remote Services: internet services
  - authn, authz, trust, data protection
- MCP Host: Any AI tool that requires data using an MCP
- A2A: agent to agent
- Agent (Tools, resources, prompts, discovery, uses MCPs, uses agents, uses LLMs)
- RAG (Retrieval-Augmented Generation)
- LLM (Large language model)
- Generative AI (reactive based on existing model)
- Agentic AI (Pro-active: action: perceive, decide, execute, learn)
- copilot UI over AI Agent
- Vector database
- AI Slop: name for  rubbish created by AI Tools
- S in MCP: `S` stands for Security in the MCP abbreviation

## MCP flow types

### Simple

![Flow 1](https://github.com/damienbod/McpSecurity/blob/main/flows/mcp-flow-1.drawio.png)

### On Behalf Of

![Flow 2](https://github.com/damienbod/McpSecurity/blob/main/flows/mcp-flow-2.drawio.png)

### Multi Client

![Flow 3](https://github.com/damienbod/McpSecurity/blob/main/flows/mcp-flow-3.drawio.png)

### Multi Client, Multi Server

![Flow 4](https://github.com/damienbod/McpSecurity/blob/main/flows/mcp-flow-4.drawio.png)

## SPIFFE

https://spiffe.io/docs/latest/spiffe-about/overview/

## Ready made MCP

https://mcp.azure.com/

https://github.com/microsoft/azure-devops-mcp

https://auth0.com/blog/an-introduction-to-mcp-and-authorization/

https://learning.postman.com/docs/postman-ai-agent-builder/mcp-server-flows/mcp-server-flows/

https://stytch.com/blog/MCP-authentication-and-authorization-guide/

## .NET MCP server

https://devblogs.microsoft.com/dotnet/mcp-server-dotnet-nuget-quickstart/

https://github.com/microsoft/mcp-dotnet-samples

https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server

## Standards, draft Standards

[OAuth 2.0 Dynamic Client Registration Protocol](https://datatracker.ietf.org/doc/html/rfc7591)

[OAuth 2.0 Authorization Server Metadata](https://datatracker.ietf.org/doc/html/rfc8414)

https://modelcontextprotocol.io/specification/2025-06-18/basic/authorization

https://modelcontextprotocol.io/specification/2025-06-18/basic/security_best_practices

https://github.com/modelcontextprotocol/modelcontextprotocol/issues/1299

https://den.dev/blog/mcp-authorization-resource/

## AI UI agents with OAuth support

https://github.com/daodao97/chatmcp

https://claude.ai/download

https://cursor.com/

Visual Studio code

Visual Studio

## Links

https://github.com/MicrosoftDocs/mcp

https://devblogs.microsoft.com/dotnet/mcp-csharp-sdk-2025-06-18-update/

https://modelcontextprotocol.io/docs/learn/architecture

https://github.com/SonarSource/sonarqube-mcp-server

https://den.dev/blog/mcp-authorization-resource/

https://den.dev/blog/mcp-csharp-sdk-authorization/

https://github.com/modelcontextprotocol/modelcontextprotocol/issues/1299

https://blog.cloudflare.com/building-ai-agents-with-mcp-authn-authz-and-durable-objects/

https://blog.aidanjohn.org/2025/07/30/mcp-a-new-frontier-in.html

https://medium.com/kagenti-the-agentic-platform/security-in-and-around-mcp-part-1-oauth-in-mcp-3f15fed0dd6e

https://medium.com/kagenti-the-agentic-platform/security-in-and-around-mcp-part-2-mcp-in-deployment-65bdd0ba9dc6

https://blog.christianposta.com/implementing-mcp-dynamic-client-registration-with-spiffe/

https://blog.christianposta.com/authenticating-mcp-oauth-clients-with-spiffe/

https://luke.geek.nz/azure/akahu-mcp-apim/

https://github.com/modelcontextprotocol/inspector

## Copilot Links

https://github.com/dotnet/AspNetCore.Docs/issues/35798

https://docs.github.com/en/copilot/how-tos/custom-instructions/adding-repository-custom-instructions-for-github-copilot

https://github.com/dotnet/docs-aspire/blob/main/.github/copilot-instructions.md

## Ready to use servers

https://mcpservers.org/

https://devblogs.microsoft.com/azure-sdk/introducing-the-azure-mcp-server/
 
## Learning Courses

https://github.com/microsoft/mcp-for-beginners

## Azure App Service MCP server

https://github.com/Azure-Samples/remote-mcp-webapp-dotnet

## Testing MCP Client tool

```
npx @modelcontextprotocol/inspector
```

https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/ProtectedMcpClient/Program.cs

## App registrations

**Http MCP Server**

ClientID: 96b0f495-3b65-4c8f-a0c6-c3767c3365ed
TenantID: 5698af84-5720-4ff0-bdc3-9d9195314244
Scope: api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/mcp:tools

**Http MCP Client**

ClientID: 344677a4-a975-4cba-a4b0-2d0771847938
TenantID: 5698af84-5720-4ff0-bdc3-9d9195314244
Scope: api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/mcp:tools
Secret: 
RedirectUrl: https://localhost:5001/signin-oidc

**Http MCP Desktop Client**

ClientID: eff6bb0e-9871-458f-92ea-923c02250a05
TenantID: 5698af84-5720-4ff0-bdc3-9d9195314244
Scope: api://96b0f495-3b65-4c8f-a0c6-c3767c3365ed/mcp:tools
RedirectUri: http://localhost:1179/callback