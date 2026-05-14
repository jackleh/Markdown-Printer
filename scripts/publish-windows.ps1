param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot
$projectFile = Join-Path $projectRoot "MDPrinter.csproj"
$publishDir = Join-Path $projectRoot "artifacts\publish\windows\MarkdownPrinter-win"
$distDir = Join-Path $projectRoot "artifacts\dist"
$zipPath = Join-Path $distDir "MarkdownPrinter-windows.zip"
$entryPoint = Join-Path $publishDir "MDPrinter.exe"

Set-Location $projectRoot

Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

dotnet publish `
    $projectFile `
    -c $Configuration `
    -f net10.0-windows10.0.19041.0 `
    -p:WindowsPackageType=None `
    -o $publishDir

if (-not (Test-Path $entryPoint))
{
    throw "Windows publish completed, but MDPrinter.exe was not found in $publishDir."
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Created Windows distributable:"
Write-Host "  $zipPath"
