# MCP Server — Product Catalogue Sample

A minimal, self-contained example of a **Model Context Protocol (MCP)** server written in
C# on **ASP.NET Core (.NET 10)**, using the official
[`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore)
SDK.

The server exposes a small product catalogue as MCP **tools**, so that any MCP-capable client
(Claude Desktop, Claude Code, an IDE agent, or a custom host) can search the catalogue and
retrieve product details in natural language.

## What it does

The server publishes itself as `ProductServer` during the MCP `initialize` handshake and exposes
two tools:

| Tool | Purpose |
| --- | --- |
| `search_products` | Search the catalogue by keyword. Matches against product name and category and returns SKU, name, category, price and stock level for each hit. |
| `get_product` | Retrieve the full details of a single product by its exact SKU (case-insensitive). |

Each tool and each parameter carries a `[Description]` attribute — this is the text the model
reads when deciding *whether* and *how* to call a tool, so it is part of the public contract of
the server, not just documentation.

The catalogue itself is an in-memory seed list (seven office / electronics items) served through
an `IProductRepository` abstraction. In a real deployment that repository would be backed by a
database or an upstream API; the tool layer would stay unchanged.

## Project layout

```
Mcp.slnx                                  Solution file
McpServer/
  Program.cs                              Host setup, MCP registration, HTTP transport
  McpServer.csproj                        net10.0, ModelContextProtocol.AspNetCore
  ProductBox/
    ProductTools.cs                       [McpServerToolType] — the exposed MCP tools
    Dto/Product.cs                        Product record
    Repository/IProductRepository.cs      Data-access abstraction
    Repository/ProductRepository.cs       In-memory seed catalogue
  Properties/launchSettings.json          Local URLs (http://localhost:5025)
```

## How it is wired up

`Program.cs` keeps the whole configuration in a few lines:

* `AddScoped<IProductRepository, ProductRepository>()` — the repository is resolved by DI and
  injected into `ProductTools` through its constructor.
* `AddMcpServer(...)` — sets the server name and version advertised to clients.
* `.WithHttpTransport()` — enables the **Streamable HTTP** transport, with full MCP session
  support.
* `.WithToolsFromAssembly()` — scans the assembly for `[McpServerToolType]` classes and registers
  every `[McpServerTool]` method automatically.
* `app.MapMcp("/mcp")` — mounts the MCP endpoint.
* `app.MapGet("/health", ...)` — a plain health endpoint, handy for container liveness probes.

## Running the server

Requires the **.NET 10 SDK**.

```bash
dotnet run --project McpServer
```

The server then listens on:

* MCP endpoint — `http://localhost:5025/mcp`
* Health check — `http://localhost:5025/health`

The `https` launch profile additionally binds `https://localhost:7040`.

## Testing with the MCP Inspector

The [MCP Inspector](https://github.com/modelcontextprotocol/inspector) is the official debugging
tool for MCP servers. It connects straight to the HTTP endpoint and lets you list and invoke the
tools without going through a host such as Claude Desktop — which makes it the fastest way to
check that the server itself is behaving. Node.js must be available; `npx -y` fetches the
Inspector on demand.

With the server already running (`dotnet run --project McpServer`), start the Inspector:

```bash
npx -y @modelcontextprotocol/inspector
```

1. The Inspector opens its web UI in the browser (by default on `http://localhost:6274`). Recent
   versions print a URL with a pre-filled session token in the console — use that one.
2. Set **Transport Type** to **Streamable HTTP**.
3. Set **URL** to `http://localhost:5025/mcp`.
4. Click **Connect**. The server info panel should show `ProductServer`, version `1.0.0` — the
   values configured in `Program.cs`.
5. Open the **Tools** tab and click **List Tools**. Both `search_products` and `get_product`
   should appear.
6. Select `search_products`, set `keyword` to `keyboard` and click **Run Tool**; the response
   should include `KBD-001` — *Wireless Mechanical Keyboard*. Then try `get_product` with the SKU
   `KBD-001`.

The **Tools** tab also renders the `[Description]` text of each tool and parameter — exactly what
the model reads when deciding whether and how to call them. It is worth re-reading them there,
from the model's point of view, whenever you add a new tool.

## Connecting the server to Claude Desktop

Claude Desktop speaks MCP over **stdio**, while this server exposes a **Streamable HTTP**
endpoint. The [`mcp-remote`](https://www.npmjs.com/package/mcp-remote) bridge (run through `npx`)
connects the two, so no extra installation step is needed — `npx -y` fetches it on demand.
Node.js must be available on the machine.

1. Make sure the server is running (`dotnet run --project McpServer`).

2. Open the Claude Desktop configuration file:

   * **Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
   * **macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`

   In Claude Desktop you can also reach it via **File → Settings → Developer → Edit Config**.

3. Add an entry under `mcpServers` (keep any servers already configured there):

```json
{
  "mcpServers": {
    "miomcpserver": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-remote",
        "http://localhost:5025/mcp"
      ]
    }
  }
}
```

If the file already contains other servers, add `miomcpserver` alongside them:

```json
  "mcpServers": {
    ...
    },
    "miomcpserver": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-remote",
        "http://localhost:5025/mcp"
      ]
    }
  },
```

4. Restart Claude Desktop completely (quit it, do not just close the window). The configuration
   is read only at startup.

5. The `search_products` and `get_product` tools should now appear in the tools menu. Try asking
   something like *"which keyboards are in the catalogue?"* or *"give me the details for
   KBD-001"*.

### Troubleshooting

* **The tools do not appear.** Check that the JSON is valid (a stray comma is the usual cause)
  and that Claude Desktop was fully restarted.
* **Connection errors.** Confirm the server is up by opening `http://localhost:5025/health` in a
  browser; it should return `{"status":"healthy","server":"ProductServer"}`.
* **`npx` is not found.** Install Node.js, or use an absolute path to `npx` in the `command`
  field.
* Use `http://` (not `https://`) for the local endpoint unless you run the `https` profile and
  the development certificate is trusted.
* **Still not working?** Check the server on its own with the MCP Inspector (see above). If the
  tools list and run correctly there, the problem is on the client side — invalid JSON in the
  configuration file, or Claude Desktop not fully restarted — and not in the server.

## Extending it

To add a tool, add a public method to `ProductTools` (or to any new class marked
`[McpServerToolType]`) and annotate it with `[McpServerTool]` plus a clear `[Description]`.
`WithToolsFromAssembly()` picks it up automatically at startup — no registration code required.
