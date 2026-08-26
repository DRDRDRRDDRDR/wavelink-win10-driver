<#
.SYNOPSIS
    Build WaveLinkWin10Setup as a self-contained single-file Windows exe.
.DESCRIPTION
    dotnet publish (self-contained, single-file, win-x64) -> dist/WaveLinkWin10Setup.exe,
    then copies the external assets (driver/, input/) next to the exe so it is self-contained.
    The patched MSIX logic is embedded in the exe; only the driver MSI + your input MSIX travel alongside.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$proj = Join-Path $root "src/WaveLinkWin10Setup/WaveLinkWin10Setup.csproj"
$out  = Join-Path $root "dist"

Write-Host "=== Building WaveLinkWin10Setup (self-contained, win-x64) ==="
& dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false -p:DebugType=None -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# Copy external assets so the exe is self-contained at runtime.
Copy-Item (Join-Path $root "driver") $out -Recurse -Force
Copy-Item (Join-Path $root "input")  $out -Recurse -Force

$exe = Join-Path $out "WaveLinkWin10Setup.exe"
if (-not (Test-Path $exe)) { throw "Build produced no exe: $exe" }
$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "=== Built: $exe ($size MB) ==="
Write-Host "dist/ 目录即最终分发包（含 driver/ 与 input/）。"
