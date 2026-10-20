# Builds a self-contained Windows release (no .NET install needed) and zips it.
#   powershell -ExecutionPolicy Bypass -File tools/publish.ps1
param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
$out = Join-Path $root "artifacts\LeviShare-$version-$Runtime"
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
dotnet publish (Join-Path $root 'src\LeviShare.App\LeviShare.App.csproj') -c Release -r $Runtime --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }
Copy-Item (Join-Path $root 'README.md'), (Join-Path $root 'README_JP.md'), (Join-Path $root 'LICENSE') $out
Get-ChildItem $out -Filter *.pdb -Recurse | Remove-Item
$zip = "$out.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
"Release: $zip"
