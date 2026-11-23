$ErrorActionPreference = 'Stop'
$game = if ($env:EXAPUNKS_DIR) { $env:EXAPUNKS_DIR } else { 'C:\GOG Games\EXAPUNKS' }
$tokenPath = Join-Path $PSScriptRoot 'bridge-token.local'
$exe = Join-Path $game 'EXAPUNKS.exe'
if (-not (Test-Path $exe) -or -not (Test-Path $tokenPath)) {
    throw 'The installed game or bridge token is missing.'
}
if ((Get-FileHash $exe -Algorithm SHA256).Hash -ne
    '9e32c95075421faa26fdbeabb97e70f89f398908ee6eecfb9bf679107e43420f') {
    throw 'Unsupported game executable.'
}
if (Get-Process EXAPUNKS -ErrorAction SilentlyContinue) {
    throw 'Close the running EXAPUNKS process before starting the installed game.'
}
$env:ECHOPUNKS_DEV = '1'
$env:EXA_BRIDGE_ONLY = '1'
$env:EXA_BRIDGE_TOKEN = [IO.File]::ReadAllText($tokenPath)
$process = Start-Process -FilePath $exe -WorkingDirectory $game -PassThru
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ExaStartupInput {
    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
'@
$headers = @{ 'X-Exa-Token' = $env:EXA_BRIDGE_TOKEN }
$deadline = [DateTime]::UtcNow.AddSeconds(90)
while ([DateTime]::UtcNow -lt $deadline) {
    $process.Refresh()
    if ($process.HasExited) { throw 'The game exited during startup.' }
    try {
        $health = Invoke-RestMethod 'http://127.0.0.1:8772/health' -Headers $headers -TimeoutSec 2
        if ($health.game_initialized) {
            Write-Host 'Installed game initialized with its normal save location.'
            return
        }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $point = [IntPtr]((100 -shl 16) -bor 100)
            [void][ExaStartupInput]::PostMessage($process.MainWindowHandle, 0x201, [IntPtr]1, $point)
            [void][ExaStartupInput]::PostMessage($process.MainWindowHandle, 0x202, [IntPtr]::Zero, $point)
        }
    } catch [System.Net.WebException] {
        # The server has not started listening yet.
    }
    Start-Sleep -Milliseconds 500
}
throw 'The game did not finish initializing within 90 seconds; inspect the game window.'
