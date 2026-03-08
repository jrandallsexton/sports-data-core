# Seq MCP Server

## Setup

- **Tool:** `seq-mcp-server` (installed via `dotnet tool install -g SeqMcpServer`)
- **Config location:** `~/.claude.json` under `mcpServers.seq` (user scope)
- **Seq URL:** `https://logging.sportdeets.com`
- **Available tools:** `SeqSearch`, `SeqWaitForEvents`, `SignalList`

## Verification Prompt

Paste this into a new Claude Code session to verify the MCP is working and check on the current sourcing run:

---

We have a Seq MCP server configured at user scope in `~/.claude.json`. It connects to `https://logging.sportdeets.com` and provides `SeqSearch`, `SeqWaitForEvents`, and `SignalList` tools. Please verify the MCP server is connected and working by:

1. Listing available signals via `SignalList`
2. Running a quick `SeqSearch` for recent Error-level events (last hour) with a small result limit (5)

If it works, we have a historical sourcing run in progress for college football (NCAA). Search for any `DbUpdateException` or `ExternalDocumentNotSourcedException` errors from the `SportsData.Producer` application in the last 2 hours and give a summary of error frequency and whether they appear to be transient (retried/handled) or persistent failures.

---

## Notes

- The MCP server is read-only (queries only, no writes to Seq)
- Queries are on-demand, not continuous — no background load on the cluster
- The API key has read-only permissions
- If performance concerns arise, the MCP can be removed with `claude mcp remove seq --scope user`
