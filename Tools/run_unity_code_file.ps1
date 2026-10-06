param([Parameter(Mandatory=$true)][string]$Path)
$payload = @{name='execute_code';arguments=@{action='execute';code=(Get-Content -LiteralPath $Path -Raw)}}
$requestPath = Join-Path $PSScriptRoot '../SourceArt/MazeAftermath/current_unity_request.json'
$payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $requestPath -Encoding utf8
python (Join-Path $PSScriptRoot 'unity_mcp.py') tools/call ('@'+$requestPath)
