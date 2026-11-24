<#
.SYNOPSIS
    Build Taoist (Release; ModLib.dll: the build committed in the submodule) and run the unit tests (tests\UnitTests:
    Taoist tests + ModLib tests): runs GuiGuBaHuang-ModLib\scripts\run-unittests.ps1 on this repository.
.EXAMPLE
    .\scripts\run-unittests.ps1
    .\scripts\run-unittests.ps1 -NoBuild -Filter "FullyQualifiedName~Taoist"
#>
[CmdletBinding()]
param([switch]$NoBuild, [string]$Filter, [switch]$IncludeKnownIssues, [string]$GameFolder)
$repo = Split-Path $PSScriptRoot -Parent
& (Join-Path $repo 'GuiGuBaHuang-ModLib\scripts\run-unittests.ps1') -RepoRoot $repo -NoBuild:$NoBuild -Filter $Filter -IncludeKnownIssues:$IncludeKnownIssues -GameFolder $GameFolder
exit $LASTEXITCODE
