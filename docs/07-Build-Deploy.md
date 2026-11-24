# Build and Deploy

## Build Scripts

`3161035078/scripts/` holds `settings.json` and 3-line wrappers of the shared scripts `GuiGuBaHuang-ModLib/scripts/project/`
(same for the ModLib project `GuiGuBaHuang-ModLib/3385996759/scripts/`, built in the ModLib repository):

**clean-project.ps1**
- Cleans build artifacts and output folders

**build-project.ps1**
- Compiles the project in Release mode
- Copies the dependencies (`CodeArtifacts`, `ModLib.dll` of the submodule: `GuiGuBaHuang-ModLib/3385996759/debug/Mod_flQrxq/ModCode/dll/`)
- Outputs to `debug/Mod_xxxxx/ModCode/dll/`

**rebuild-project.ps1**
- Runs clean-project.ps1
- Runs build-project.ps1

**start-auto-build.ps1**
- Watches for file changes
- Automatically rebuilds on save
- Requires Python and watchdog module

**copy-to-steam.ps1**
- Copies built mod to Steam Workshop folder
- Used for testing and publishing

**copy-from-steam.ps1**
- Copies mod from Steam Workshop back to Git repository

**rebuild-and-deploy.ps1**
- Runs rebuild-project.ps1
- Runs copy-to-steam.ps1
- Complete workflow for testing

**compress.ps1**
- Compresses `debug/Mod_xxxxx/` to `debug/Mod_xxxxx.rar` (requires WinRAR)

## Root Scripts

Located in `scripts/` (settings in `scripts/settings.json`):

**rebuild-and-deploy-all.ps1** - Rebuilds every project of `settings.json` `"projects"` (3161035078), then copies the `"dependencies"` (the ModLib build committed in the submodule, not rebuilt) and the projects to Steam Workshop

**copy-both-to-steam.ps1** - Copies the dependencies and the projects to Steam Workshop; **copy-both-from-steam.ps1** - copies the projects back (not ModLib)

**compress-mods.ps1** - Runs each project's `compress.ps1`

**update-repo.ps1** - Pulls latest changes and updates submodules (`-Force` to auto-stash, `-NoSubmodules` to skip submodules)

**setup/** - One-time environment setup: `install.ps1` runs `00-install-dependencies.ps1` → `01-clone-repository.ps1` → `03-extract-resources.ps1` → `02-configure-settings.ps1` → `04-verify-environment.ps1` (shared helpers in `_common.ps1`). Every step is re-runnable and works from any repo folder; see [INSTALLATION.md](../../INSTALLATION.md)

Release scripts (shared, in `GuiGuBaHuang-ModLib/scripts/release/`; each repository releases its own projects: this repository Taoist, GuiGuBaHuang-ModLib ModLib; the repository is `-RepoRoot` or the git repository of the current folder):

**package-release.ps1** - Zips the folder `<ProjectId>/debug/<modName>` (as made by the build task: DLLs, ModConf, ModImg, SteamImg, ModExportData.cache) into `dist/` (`Taoist-<ver>.zip`) + `release-notes.md` (downloads, install, requirements: `releaseRequires` of the project settings, SHA-256). `-Target <releaseName>` (default: the only project of the repository); `-Tag <tag>` (CI) takes the target from the tag and fails when the tag does not match the version of the source code; `-PrepareResources` (CI) first runs `build-project.ps1 -ResourcesOnly`. VS Code task **Release: Package Zips (local)**

**create-release-tag.ps1** `[-Target <releaseName>] [-RepoRoot <repository>]` - Creates the release tag on HEAD of the repository and pushes it (starts the CI of that repository). Checks: tag not existing yet (local + origin), uncommitted changes of the mods (asks), HEAD pushed (offers to push the branch); asks a confirmation (`-Yes` skips, `-NoPush` only tags). VS Code tasks **Release: Tag Taoist** (this repository) and **Release: Tag ModLib** (`-RepoRoot GuiGuBaHuang-ModLib`: tags and pushes in the submodule)

**_release.ps1** - Shared: repository, projects (`"projects"` of `scripts/settings.json`), versions (`versionFile` / `versionPattern` of each `<ProjectId>/scripts/settings.json`), targets and tags

**setup/setup-nexus.ps1** - Nexus Mods setup of the CI on GitHub with `gh`: secret `NEXUSMODS_API_KEY` (checked with Nexus, given to gh on stdin) and variables `NEXUS_<MOD>_URL` / `NEXUS_<MOD>_FILE_ID`. Shows what is set; Enter keeps a value, `-` removes it. VS Code task **Setup: Nexus Mods (CI)**

`build-project.ps1 -ResourcesOnly` (each project) - only the copy of ModConf/ModImg/SteamImg to `debug/` (no dotnet build)

## GitHub Release and Nexus Mods (CI)

`.github/workflows/release.yml` of each repository runs **only when its release tag is pushed** (never on a normal push):

| Repository | Tag (version from the source code) | Zip |
|---|---|---|
| GuiGuBaHuang-ModLib | `ModLib-<Version of 3385996759 ModMain.cs>` | `ModLib-<ver>.zip` |
| GuiGuBaHuang-Taoist (this one) | `Taoist-<TAOIST_VERSION>` | `Taoist-<ver>.zip` |

Taoist reuses the ModLib DLL of the submodule: release ModLib first (in its repository) when Taoist needs a new ModLib, then update the submodule here.

Steps: checkout (with Git LFS: SteamImg; the submodule for the shared scripts) → `package-release.ps1 -Tag <tag> -PrepareResources` (the CI cannot build: the DLLs of `debug/` are the committed ones, ModConf/ModImg/SteamImg are copied by `build-project.ps1 -ResourcesOnly`) → GitHub Release `<tag>` with the zips and the notes → Nexus Mods (optional): each zip as a new version of an existing Nexus file with [`Nexus-Mods/upload-action`](https://github.com/Nexus-Mods/upload-action) (previous version archived).

Publishing a version: bump the version → build in Release (task) → commit (the DLLs of `debug/` included) and push → task **Release: Tag ...**.

Nexus (task **Setup: Nexus Mods (CI)**, or Settings → Secrets and variables → Actions); the mod page and a first file must be created by hand on Nexus first:

| Name | Kind | Value |
|---|---|---|
| `NEXUSMODS_API_KEY` | secret (in each repository) | API key of https://www.nexusmods.com/settings/api-keys |
| `NEXUS_TAOIST_FILE_ID` (here), `NEXUS_MODLIB_FILE_ID` (GuiGuBaHuang-ModLib) | variable | File id (Files tab of the mod page → "Advanced", or Manage Files). Not set: no Nexus upload |
| `NEXUS_TAOIST_URL` (here), `NEXUS_MODLIB_URL` (GuiGuBaHuang-ModLib) | variable | Mod page URL, linked in the release notes |

## VS Code Tasks

All scripts above are available in `.vscode/tasks.json`: **Terminal → Run Task...** (or `Ctrl+Shift+B` for the default build task "All: Rebuild & Deploy").
