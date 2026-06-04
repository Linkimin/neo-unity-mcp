param(
    [string]$SourceRoot = "",
    [string]$PackageRoot = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = Join-Path $repoRoot "packages/com.neo.unity-mcp"
}

$pluginsDir = Join-Path $PackageRoot "Editor/Plugins"
$devNugetDir = Join-Path $repoRoot "dev-project/Assets/Packages"

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    if (Test-Path $devNugetDir) {
        $SourceRoot = $devNugetDir
    } else {
        throw "SourceRoot was not provided and dev-project/Assets/Packages does not exist. Pass -SourceRoot pointing at a NuGetForUnity restore folder, NuGet cache folder, or flat DLL folder."
    }
}

$requiredDlls = @(
    "Microsoft.CodeAnalysis.dll",
    "Microsoft.CodeAnalysis.CSharp.dll",
    "System.Collections.Immutable.dll",
    "System.Reflection.Metadata.dll",
    "System.Text.Encoding.CodePages.dll",
    "System.Threading.Tasks.Extensions.dll"
)

$blockedDlls = @(
    "System.Memory.dll",
    "System.Buffers.dll",
    "System.Runtime.CompilerServices.Unsafe.dll",
    "System.Numerics.Vectors.dll"
)

function Find-Dll($root, $name) {
    $matches = Get-ChildItem -Path $root -Recurse -File -Filter $name |
        Sort-Object @{ Expression = {
            if ($_.FullName -match "\\lib\\netstandard2\.0\\") { 0 }
            elseif ($_.FullName -match "\\lib\\netstandard2\.1\\") { 1 }
            elseif ($_.FullName -match "\\lib\\") { 2 }
            else { 3 }
        }}, FullName

    if ($matches.Count -eq 0) {
        return $null
    }

    return $matches[0]
}

New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null

foreach ($dll in $requiredDlls) {
    $source = Find-Dll $SourceRoot $dll
    if ($null -eq $source) {
        throw "Required DLL not found under SourceRoot: $dll"
    }

    Copy-Item -LiteralPath $source.FullName -Destination (Join-Path $pluginsDir $dll) -Force
    Write-Host "Vendored $dll from $($source.FullName)"
}

foreach ($dll in $blockedDlls) {
    $target = Join-Path $pluginsDir $dll
    if (Test-Path $target) {
        throw "Blocked duplicate Unity-provided dependency is present in Editor/Plugins: $dll. Remove it; Unity provides this dependency."
    }
}

Write-Host "Roslyn vendor set is complete in $pluginsDir"
Write-Host "After changing DLLs, open Unity and ensure PluginImporter settings are Editor-only and Auto Reference is disabled."
