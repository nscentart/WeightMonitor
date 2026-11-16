param(
    [string]$Exe = "bin\WeightMonitor.exe",
    [string]$OutDir = "bin\tests",
    [int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$suites = "--test-security", "--test-notifications", "--test-flyouts", "--test-themes", "--test-close",
          "--test-languages", "--test-license", "--test-etw", "--test-disks", "--test-paint", "--test-migration"
$resultOption = "--result"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ("wm-test-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $sandbox | Out-Null
$savedLocalAppData = $env:LOCALAPPDATA
$env:LOCALAPPDATA = $sandbox
$summary = @()
try {
    foreach ($suite in $suites) {
        $name = $suite.TrimStart('-')
        $file = Join-Path (Resolve-Path $OutDir) ($name + ".txt")
        if (Test-Path $file) { Remove-Item $file -Force }
        $arguments = @($suite, $resultOption, "`"$file`"")
        if ($suite -eq "--test-migration") { $arguments += "--data", "`"$(Join-Path $root "tests\legacy-data")`"" }
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $process = Start-Process -FilePath (Resolve-Path $Exe) -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $finished = $process.WaitForExit($TimeoutSeconds * 1000)
        if (-not $finished) { $process.Kill(); $process.WaitForExit() }
        $watch.Stop()
        $passed = 0; $failed = 0; $other = 0
        if (Test-Path $file) {
            foreach ($line in (Get-Content -LiteralPath $file -Encoding UTF8)) {
                if ($line -match '^(?i:ok)(\s|$)') { $passed++ } elseif ($line -match '^ERROR') { $failed++ } else { $other++ }
            }
        }
        $exit = if ($finished) { $process.ExitCode } else { "timeout" }
        $summary += [pscustomobject]@{ Suite = $name; Passed = $passed; Failed = $failed; Other = $other; Exit = $exit; Seconds = [math]::Round($watch.Elapsed.TotalSeconds, 1) }
    }
} finally {
    $env:LOCALAPPDATA = $savedLocalAppData
    try { Remove-Item $sandbox -Recurse -Force } catch { }
}
$summary | Format-Table -AutoSize | Out-String -Width 200
$summary | ConvertTo-Json | Set-Content -Path (Join-Path $OutDir "summary.json") -Encoding UTF8
$total = ($summary | Measure-Object -Property Passed -Sum).Sum
$failures = ($summary | Measure-Object -Property Failed -Sum).Sum
"total passed: $total, failed: $failures"
if ($failures -gt 0) { exit 1 }
