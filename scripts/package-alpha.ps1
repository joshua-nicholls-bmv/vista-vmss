param([string]$Version = "0.1.0-alpha.1")
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$packageFolder = Join-Path $repoRoot "artifacts/VISTA-$Version-win-x64"
& dotnet publish (Join-Path $repoRoot "src/Vista.Desktop/Vista.Desktop.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false "-p:Version=$Version" -p:AssemblyVersion=0.1.0.0 -p:FileVersion=0.1.0.0 "-p:InformationalVersion=$Version" -o $packageFolder --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
Copy-Item -LiteralPath (Join-Path $repoRoot "ALPHA-TESTING.md") -Destination (Join-Path $packageFolder "START-HERE.txt")
Write-Output "Portable build ready at $packageFolder"
