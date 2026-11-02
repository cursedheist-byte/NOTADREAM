# Helper to talk to the Unity MCP server over HTTP JSON-RPC.
# Usage: . .\.mcp.ps1 ; mcp <method> <json-params>  |  mcptool <toolName> <json-args>

$Global:MCP_URL = "http://127.0.0.1:8081/mcp"
$Global:MCP_SESSION = "7bb35daa6c7745aa891a2eb6f8244049"

function Mcp-Raw {
    param([string]$Method, [string]$ParamsJson = "{}", [int]$Id = 2)
    $body = '{"jsonrpc":"2.0","id":' + $Id + ',"method":"' + $Method + '","params":' + $ParamsJson + '}'
    $headers = @{
        "Accept"           = "application/json, text/event-stream"
        "Content-Type"     = "application/json"
        "Mcp-Session-Id"   = $Global:MCP_SESSION
        "MCP-Protocol-Version" = "2024-11-05"
    }
    try {
        $r = Invoke-WebRequest -Uri $Global:MCP_URL -Method POST -Headers $headers -Body $body -TimeoutSec 120
        return $r.Content
    } catch {
        $msg = "ERR: " + $_.Exception.Message
        if ($_.Exception.Response) {
            try {
                $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $msg += "`n" + $sr.ReadToEnd()
            } catch {}
        }
        return $msg
    }
}

function Mcp-Call {
    param([string]$Method, [string]$ParamsJson = "{}")
    $raw = Mcp-Raw -Method $Method -ParamsJson $ParamsJson
    # strip SSE framing: lines "event: message" / "data: {...}"
    $data = ($raw -split "`n" | Where-Object { $_ -like "data: *" }) -replace "^data: ", ""
    if (-not $data) { return $raw }
    return ($data -join "`n")
}

function McpTool {
    param([string]$ToolName, [string]$ArgsJson = "{}")
    $params = '{"name":"' + $ToolName + '","arguments":' + $ArgsJson + '}'
    return Mcp-Call -Method "tools/call" -ParamsJson $params
}

function McpExec {
    param([string]$Code)
    # Run arbitrary C# in the Unity Editor via the execute_code tool.
    $code = $Code -replace '\\', '\\\\' -replace '"', '\"' -replace "`r", '' -replace "`n", '\n'
    $args = '{"code":"' + $code + '"}'
    return McpTool -ToolName "execute_code" -ArgsJson $args
}
