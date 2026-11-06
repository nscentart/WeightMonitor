param([ValidateSet("a", "b")][string]$Style = "b")
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$target = "C:\Program Files\WeightMonitor"
New-Item -ItemType Directory -Force -Path (Join-Path $root "bin") | Out-Null
Start-Transcript -Path (Join-Path $root "bin\update.log") -Force | Out-Null
try {
    Stop-ScheduledTask -TaskName "WeightMonitor" -ErrorAction SilentlyContinue
    Get-Process WeightMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    Copy-Item (Join-Path $root "bin\WeightMonitor.exe") $target -Force
    "copied to ${target}"
    $action = New-ScheduledTaskAction -Execute "$target\WeightMonitor.exe" -Argument "--style $Style" -WorkingDirectory $target
    Set-ScheduledTask -TaskName "WeightMonitor" -Action $action | Out-Null
    "autostart: --style $Style"
    Start-ScheduledTask -TaskName "WeightMonitor"
    Start-Sleep -Seconds 3
    "WeightMonitor running: $([bool](Get-Process WeightMonitor -ErrorAction SilentlyContinue))"
} finally {
    Stop-Transcript | Out-Null
}
