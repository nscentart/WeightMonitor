param(
    [string]$Exe = "bin\WeightMonitor.exe",
    [string]$OutDir = "docs"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class EmptyDesktop {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO { public int cb; public string lpReserved; public string lpDesktop; public string lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags; public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError; }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr devmode, int flags, uint access, IntPtr sa);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateProcess(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, int flags, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr h, uint code);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    public static int Run(string exe, string commandLine, int seconds) {
        string name = "WeightMonitorPhotos" + Guid.NewGuid().ToString("N");
        IntPtr desktop = CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, 0x10000000, IntPtr.Zero);
        if (desktop == IntPtr.Zero) return -1;
        try {
            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = "WinSta0\\" + name;
            PROCESS_INFORMATION pi;
            if (!CreateProcess(exe, commandLine, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref si, out pi)) return -2;
            try {
                if (WaitForSingleObject(pi.hProcess, (uint)(seconds * 1000)) != 0) { TerminateProcess(pi.hProcess, 1); return -3; }
                uint code;
                GetExitCodeProcess(pi.hProcess, out code);
                return (int)code;
            } finally { CloseHandle(pi.hProcess); CloseHandle(pi.hThread); }
        } finally { CloseDesktop(desktop); }
    }
}
"@
$program = (Resolve-Path $Exe).Path
$languages = "en", "it", "fr", "es", "de"
$flyouts = [ordered]@{ "flyout-overview" = "all"; "flyout-cpu" = "cpu"; "flyout-gpu" = "gpu"; "flyout-ram" = "ram"; "flyout-commit" = "commit"; "flyout-disks" = "disks"; "flyout-network" = "network" }
function Photo([string]$File, [string[]]$Arguments) {
    if (Test-Path $File) { Remove-Item $File -Force }
    $quoted = ($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $code = [EmptyDesktop]::Run($program, '"' + $program + '" ' + $quoted, 60)
    if ($code -ne 0 -or -not (Test-Path $File)) { throw "screenshot failed ($code): $File" }
}
function Blank([string]$File) {
    $bitmap = New-Object System.Drawing.Bitmap $File
    try { return $bitmap.GetPixel([int]($bitmap.Width / 2), [int]($bitmap.Height / 2)).ToArgb() -eq [System.Drawing.Color]::Black.ToArgb() } finally { $bitmap.Dispose() }
}
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ("wm-photos-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $sandbox | Out-Null
$savedLocalAppData = $env:LOCALAPPDATA
$env:LOCALAPPDATA = $sandbox
try {
    foreach ($language in $languages) {
        $folder = Join-Path $OutDir $language
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
        $folder = (Resolve-Path $folder).Path
        Photo (Join-Path $folder "bar.png") @("--screenshot", (Join-Path $folder "bar.png"), "--sample", "--height", "48", "--language", $language)
        foreach ($name in $flyouts.Keys) {
            $file = Join-Path $folder "$name.png"
            Photo $file @("--screenshot", $file, "--sample", "--height", "48", "--flyout", $flyouts[$name], "--tab", "1", "--language", $language)
        }
        $file = Join-Path $folder "license.png"
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            Photo $file @("--screenshot-license", $file, "--scale", "1", "--language", $language)
            if (-not (Blank $file)) { break }
        }
        if (Blank $file) { throw "blank screenshot: $file" }
        "ok $language"
    }
} finally {
    $env:LOCALAPPDATA = $savedLocalAppData
    Remove-Item $sandbox -Recurse -Force -ErrorAction SilentlyContinue
}
