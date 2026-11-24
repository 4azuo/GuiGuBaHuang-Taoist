<#
.SYNOPSIS
    Setup helpers of GuiGuBaHuang-Taoist: the generic ones come from ModLib, this file adds the Taoist ones.
.DESCRIPTION
    Dot-source from a setup script:  . (Join-Path $PSScriptRoot '_common.ps1')
    Generic helpers (output, prompts, repository, Steam / game, JSON): GuiGuBaHuang-ModLib\scripts\setup\_common.ps1 of
    the repository, or modlib-setup\_common.ps1 next to this file, downloaded from GitHub while the repository (and its
    ModLib submodule) is not cloned yet (install.ps1 on a new machine).
    Here: the repository URLs, ModCreator, BaseGameResources.

    Keep this file ASCII-only: Windows PowerShell 5.1 reads BOM-less scripts as ANSI.
#>

$script:ModLibSetupRaw = 'https://raw.githubusercontent.com/4azuo/GuiGuBaHuang-ModLib/main/scripts/setup'

# Folder of the setup scripts of ModLib: the submodule of the repository, else modlib-setup\ next to this file
# (downloaded: _common.ps1, 00-install-dependencies.ps1).
function Get-ModLibSetupDir([string]$ScriptDir) {
    $inRepo = Join-Path $ScriptDir '..\..\GuiGuBaHuang-ModLib\scripts\setup'
    if (Test-Path (Join-Path $inRepo '_common.ps1')) { return (Resolve-Path $inRepo).Path }
    $local = Join-Path $ScriptDir 'modlib-setup'
    if (-not (Test-Path $local)) { New-Item -ItemType Directory -Path $local | Out-Null }
    foreach ($name in '_common.ps1', '00-install-dependencies.ps1') {
        $file = Join-Path $local $name
        if (-not (Test-Path $file)) {
            Write-Host "Downloading ModLib setup script $name ..." -ForegroundColor Cyan
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072
            (New-Object Net.WebClient).DownloadFile("$script:ModLibSetupRaw/$name", $file)
        }
    }
    return (Resolve-Path $local).Path
}

. (Join-Path (Get-ModLibSetupDir $PSScriptRoot) '_common.ps1')

$script:RepoUrlDefault       = 'https://github.com/4azuo/GuiGuBaHuang-Taoist.git'
$script:ModCreatorUrlDefault = 'https://github.com/4azuo/GuiGuBaHuang-ModCreator.git'
$script:DefaultRepoPath      = 'C:\git\GuiGuBaHuang-Taoist'

# ---------------------------------------------------------------- ModCreator

# Make sure GuiGuBaHuang-ModCreator is checked out.
# Uses the submodule mapping when .gitmodules has one; otherwise clones the
# ModCreator repo directly and checks out the commit the parent repo points to.
function Initialize-ModCreator {
    param([string]$RepoPath, [string]$Url = $script:ModCreatorUrlDefault)
    $ErrorActionPreference = 'Continue'
    $dir = Join-Path $RepoPath 'GuiGuBaHuang-ModCreator'

    if (Test-Path (Join-Path $dir 'ModCreator.sln')) {
        Write-Ok "ModCreator already present: $dir"
        return $true
    }

    $pinned = $null
    Push-Location $RepoPath
    try {
        $gitmodules = Join-Path $RepoPath '.gitmodules'
        if ((Test-Path $gitmodules) -and (Select-String -Path $gitmodules -Pattern 'GuiGuBaHuang-ModCreator' -Quiet)) {
            Write-Info 'Initializing ModCreator submodule...'
            git submodule update --init --recursive
            if (Test-Path (Join-Path $dir 'ModCreator.sln')) {
                Write-Ok 'ModCreator submodule initialized'
                return $true
            }
        }
        $entry = git ls-tree HEAD GuiGuBaHuang-ModCreator
        if ($entry -match '^160000 commit ([0-9a-f]{40})') { $pinned = $Matches[1] }
    }
    finally { Pop-Location }

    if ((Test-Path $dir) -and (Get-ChildItem $dir -Force | Select-Object -First 1)) {
        Write-Warn "Folder exists but has no ModCreator.sln: $dir"
        Write-Warn 'Empty or delete that folder, then run this step again.'
        return $false
    }

    Write-Info "Cloning ModCreator: $Url"
    git clone $Url $dir
    if ($LASTEXITCODE -ne 0) {
        Write-Err 'Failed to clone ModCreator'
        return $false
    }

    if ($pinned) {
        $head = git -C $dir rev-parse HEAD
        if ($head -ne $pinned) {
            git -C $dir checkout --quiet $pinned
            if ($LASTEXITCODE -eq 0) {
                Write-Info "ModCreator checked out at pinned commit $($pinned.Substring(0, 7)) (detached HEAD)"
            }
            else {
                Write-Warn "Could not check out pinned commit $pinned, staying on default branch"
            }
        }
    }
    Write-Ok "ModCreator ready: $dir"
    return $true
}

# ---------------------------------------------------------------- BaseGameResources

function Get-ResourceSearchDirs([string]$RepoPath) {
    $dirs = @()
    if ($RepoPath) {
        $dirs += Join-Path $RepoPath 'GuiGuBaHuang-ModCreator'
        $dirs += $RepoPath
    }
    $dirs += Join-Path $env:USERPROFILE 'Downloads'
    $dirs += Join-Path $env:USERPROFILE 'Desktop'
    return $dirs | Where-Object { Test-Path $_ }
}

# Already-extracted BaseGameResources folder (newest), or $null.
function Find-BaseGameResourcesDir([string]$RepoPath) {
    foreach ($d in (Get-ResourceSearchDirs $RepoPath)) {
        $hit = Get-ChildItem -Path $d -Directory -Filter 'BaseGameResources*' -ErrorAction SilentlyContinue |
            Where-Object { Get-ChildItem $_.FullName -Force | Select-Object -First 1 } |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

# BaseGameResources archive (newest), or $null.
function Find-BaseGameResourcesArchive([string]$RepoPath) {
    foreach ($d in (Get-ResourceSearchDirs $RepoPath)) {
        $hit = Get-ChildItem -Path $d -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'BaseGameResources*' -and $_.Extension -in '.rar', '.7z', '.zip' } |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}
