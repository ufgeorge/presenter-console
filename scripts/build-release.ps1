$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\PresenterConsole.Desktop\PresenterConsole.Desktop.csproj'
$publishPath = Join-Path $repositoryRoot 'publish'

New-Item -ItemType Directory -Force -Path $publishPath | Out-Null

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishPath

Write-Host "發布完成：$publishPath\PresenterConsole.Desktop.exe"
