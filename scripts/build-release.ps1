$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src\PresenterConsole.Desktop\PresenterConsole.Desktop.csproj'
$publishPath = Join-Path $repositoryRoot 'publish'

if (Test-Path $publishPath) {
    Remove-Item -Recurse -Force -Path $publishPath
}
New-Item -ItemType Directory -Force -Path $publishPath | Out-Null

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishPath

$officeSearchRoots = @(
    (Join-Path ${env:ProgramFiles} 'Microsoft Office'),
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Office')
) | Where-Object { $_ -and (Test-Path $_) }
$officeDll = Get-ChildItem $officeSearchRoots -Filter 'Office.dll' -File -Recurse -ErrorAction SilentlyContinue | Where-Object {
    try {
        ([Reflection.AssemblyName]::GetAssemblyName($_.FullName)).Version -eq [Version]'15.0.0.0'
    } catch {
        $false
    }
} | Select-Object -First 1

if (-not $officeDll) {
    throw '找不到版本 15.0.0.0 的 Office.dll；請確認建置機已安裝 Microsoft Office。'
}

Copy-Item -LiteralPath $officeDll.FullName -Destination (Join-Path $publishPath 'Office.dll') -Force

$requiredAssemblies = @(
    'Office.dll',
    'Microsoft.Office.Interop.PowerPoint.dll'
)
$missingAssemblies = $requiredAssemblies | Where-Object {
    -not (Test-Path (Join-Path $publishPath $_))
}

if ($missingAssemblies) {
    throw "發布輸出缺少必要組件：$($missingAssemblies -join ', ')"
}

Write-Host "發布驗證通過：$($requiredAssemblies -join ', ')"
Write-Host "發布完成：$publishPath\PresenterConsole.Desktop.exe"

# ── 打包 zip（GitHub Releases 用）──
[xml]$csprojXml = Get-Content $projectPath
$version = $csprojXml.Project.PropertyGroup.Version
if (-not $version) { $version = '0.0.0' }

$zipPath = Join-Path $repositoryRoot "presenter-console-$version-win-x64.zip"
if (Test-Path $zipPath) { Remove-Item -Force -Path $zipPath }

# 手冊進 zip（測試者解壓即看）
$userGuide = Join-Path $repositoryRoot 'docs\user-guide.md'
if (Test-Path $userGuide) {
    Copy-Item -LiteralPath $userGuide -Destination (Join-Path $publishPath 'UserGuide.md') -Force
}

Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $zipPath -Force

Write-Host "壓縮包：$zipPath"
Write-Host ""
Write-Host '上傳到 GitHub Releases（測試者下載用）：'
Write-Host "  gh release upload v$version `"$zipPath`""
