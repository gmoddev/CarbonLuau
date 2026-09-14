param(
    [int]$Port = 28116,
    [string]$SecretPath = 'C:\Sandbox\Codex\Artifacts\CarbonLuau\rcon-win.secret',
    [string]$Command = 'status',
    [Net.WebSockets.ClientWebSocket]$Socket,
    [switch]$KeepOpen
)
$ErrorActionPreference = 'Stop'
if (!$Socket) { $Socket = New-Object Net.WebSockets.ClientWebSocket }
$Timeout = New-Object Threading.CancellationTokenSource
$Timeout.CancelAfter(15000)
try {
    $Secret = [IO.File]::ReadAllText($SecretPath).Trim()
    $Uri = [Uri]("ws://127.0.0.1:$Port/$Secret")
    if ($Socket.State -ne [Net.WebSockets.WebSocketState]::Open) {
        $Socket.ConnectAsync($Uri, $Timeout.Token).GetAwaiter().GetResult() | Out-Null
    }
    if (!$Command) { return $Socket }
    $Identifier = Get-Random -Minimum 1 -Maximum 2147483647
    $Payload = @{ Identifier = $Identifier; Message = $Command; Name = 'CarbonLuauPhase0' } | ConvertTo-Json -Compress
    $Bytes = [Text.Encoding]::UTF8.GetBytes($Payload)
    $Segment = New-Object 'ArraySegment[byte]' -ArgumentList @(,$Bytes)
    $Socket.SendAsync($Segment, [Net.WebSockets.WebSocketMessageType]::Text, $true, $Timeout.Token).GetAwaiter().GetResult() | Out-Null
    $Buffer = New-Object byte[] 65536
    $Receive = New-Object 'ArraySegment[byte]' -ArgumentList @(,$Buffer)
    do {
        $Text = New-Object Text.StringBuilder
        do {
            $Result = $Socket.ReceiveAsync($Receive, $Timeout.Token).GetAwaiter().GetResult()
            if ($Result.MessageType -eq [Net.WebSockets.WebSocketMessageType]::Close) { throw 'RCON closed the connection' }
            [void]$Text.Append([Text.Encoding]::UTF8.GetString($Buffer, 0, $Result.Count))
        } while (!$Result.EndOfMessage)
        $Reply = $Text.ToString() | ConvertFrom-Json
    } while ($Reply.Identifier -ne $Identifier)
    $Text.ToString()
} finally {
    if (!$KeepOpen) {
        try { $Socket.CloseOutputAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'Done', $Timeout.Token).GetAwaiter().GetResult() | Out-Null } catch {}
        $Socket.Dispose()
    }
    $Timeout.Dispose()
}
