param([string]$Output = "bin")
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if (-not (Test-Path "bin\lib\LibreHardwareMonitorLib.dll")) { throw "bin\lib is missing: run scripts\libraries.ps1 first" }
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$resources = @(Get-ChildItem "bin\lib\*.dll" | ForEach-Object { "-resource:bin\lib\$($_.Name),lib.$($_.Name)" })
$resources += @(Get-ChildItem "locales\*.txt" | ForEach-Object { "-resource:locales\$($_.Name),locales.$($_.Name)" })
$resources += "-resource:assets\WeightMonitor.ico,WeightMonitor.ico", "-resource:assets\WeightMonitor.png,WeightMonitor.png"
& $csc -nologo -target:winexe -optimize -platform:x64 -codepage:65001 "-out:$Output\WeightMonitor.exe" `
    "-win32manifest:assets\app.manifest" "-win32icon:assets\WeightMonitor.ico" "-r:bin\lib\LibreHardwareMonitorLib.dll" `
    -r:System.Management.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Web.Extensions.dll `
    "-r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\netstandard.dll" $resources "-recurse:src\*.cs"
if ($LASTEXITCODE -ne 0) { throw "build failed" }
$hash = (Get-FileHash "$Output\WeightMonitor.exe" -Algorithm SHA256).Hash.ToLower()
"built: $Output\WeightMonitor.exe ($([math]::Round((Get-Item "$Output\WeightMonitor.exe").Length / 1MB, 2)) MB, SHA-256 $hash)"
