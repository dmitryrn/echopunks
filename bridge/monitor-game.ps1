param(
    [Parameter(Mandatory = $true)]
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'
$launcher = Join-Path $PSScriptRoot 'start-game.ps1'
$logDir = Split-Path -Parent $LogPath
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
function Write-MonitorLog([string]$Message) {
    Add-Content -LiteralPath $LogPath -Value ("{0:o} {1}" -f (Get-Date), $Message)
}

try {
    Write-MonitorLog 'Starting game via start-game.ps1.'
    & $launcher
    $processes = @(Get-Process EXAPUNKS -ErrorAction SilentlyContinue)
    if ($processes.Count -ne 1) {
        throw "Expected one EXAPUNKS process after launch; found $($processes.Count)."
    }
    $process = $processes[0]
    # Acquire the process handle while it is alive so exit information remains available.
    $null = $process.Handle
    Write-MonitorLog ("Monitoring pid={0}, started={1:o}." -f $process.Id, $process.StartTime)
    while ($true) {
        $process.Refresh()
        if ($process.HasExited) {
            try {
                $exitCode = $process.ExitCode
                $hex = '0x{0:X8}' -f ([uint32]([int64]$exitCode -band 0xffffffffL))
                Write-MonitorLog ("Exited code={0} ({1})." -f $exitCode, $hex)
            } catch {
                Write-MonitorLog ("Exited; exit code unavailable: {0}" -f $_.Exception.Message)
            }
            break
        }
        Write-MonitorLog ("sample working_set_bytes={0} private_bytes={1} cpu_seconds={2}" -f
            $process.WorkingSet64, $process.PrivateMemorySize64, $process.TotalProcessorTime.TotalSeconds)
        Start-Sleep -Seconds 2
    }
} catch {
    Write-MonitorLog ("Monitor error: {0}" -f $_.Exception.Message)
    throw
}
