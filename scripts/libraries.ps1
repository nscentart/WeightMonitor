$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$url = "https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/download/v0.9.6/LibreHardwareMonitor.zip"
$expectedHash = "086d9f1b5a99e643edc2cfaaac16051685b551e4c5ac0b32a57c58c0e529c001"
$needed = "LibreHardwareMonitorLib", "RAMSPDToolkit-NDD", "HidSharp", "DiskInfoToolkit", "BlackSharp.Core",
          "System.Memory", "System.Runtime.CompilerServices.Unsafe", "System.Buffers", "System.Numerics.Vectors"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$zip = Join-Path $env:TEMP "LibreHardwareMonitor-0.9.6.zip"
Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
if ($hash -ne $expectedHash) { Remove-Item $zip; throw "hash differs from the official one: $hash" }
$temp = Join-Path $env:TEMP "LibreHardwareMonitor-0.9.6"
if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
Expand-Archive $zip $temp
$target = Join-Path $root "bin\lib"
New-Item -ItemType Directory -Force -Path $target | Out-Null
foreach ($name in $needed) { Copy-Item (Join-Path $temp "$name.dll") (Join-Path $target "$name.dll") -Force }
Remove-Item $temp -Recurse -Force; Remove-Item $zip
"bin\lib ready: $($needed.Count) libraries from LibreHardwareMonitor 0.9.6 (hash verified)"
