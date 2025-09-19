using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace ToolsLibrary.Resources;

[McpServerResourceType]
public class DocumentationResource
{
    [McpServerResource]
    [Description("High-level system overview document.")]
    public ResourceContents GetSystemOverview()
        => new TextResourceContents()
        {
            Uri = "docs/system-overview",
            MimeType = "text/markdown",
            Text =
"""
# MCP Server Overview (v1.2)

This MCP server provides a stateless HTTP transport at `/mcp` and exposes Prompts, Tools, and Resources.

## Tools

### DateTools
- `GetCurrentDateTime()`
  - Returns the current UTC date/time in ISO 8601 format (`o`).

### RandomNumberTools
- `GetRandomNumber(int min = 0, int max = 100)`
  - Generates a random integer in the range [min, max).
- `GetRandomNumberFromDateTime(DateTime? datetime = null)`
  - Generates a pseudo-random number derived from the provided (or current) date/time ticks.

## Prompts
`PromptExamples` are registered (not detailed here) and can be listed by the client via the MCP prompts listing operation.

## Resources
- `docs/system-overview` (this document)

## Typical Client Flow
1. List tools → select a tool.
2. (Optionally) list prompts → choose a prompt template.
3. Invoke tool calls as needed.
4. Read resources (like this overview) for grounding.
5. Use results in higher-level reasoning or orchestration.

## Notes
- All tools are side-effect free (read/compute only) except for inherent randomness.
- Randomness uses `Random.Shared`.
- Timestamps are UTC to avoid timezone ambiguity.

"""
        };
}