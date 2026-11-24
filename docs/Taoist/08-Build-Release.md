# Taoist - Build, deploy, release

The scripts are shared by the mods: [07-Build-Deploy](../../GuiGuBaHuang-ModLib/docs/guide/07-Build-Deploy.md) of ModLib.
What is specific to this repository:

| Item | Taoist |
|---|---|
| Project | `3161035078` (`Mod_nE7UL2`, namespace `MOD_nE7UL2`), version `TAOIST_VERSION` of `ModProject/ModCode/ModMain/Const/ModConst.cs` |
| `scripts/settings.json` | `"projects": ["3161035078"]`, `"dependencies": ["GuiGuBaHuang-ModLib/3385996759"]` (the ModLib build of the submodule, deployed with Taoist, not built), `modProject` / `modName` / `modId` (`nE7UL2`) for the game tools |
| ModLib.dll | `GuiGuBaHuang-ModLib/3385996759/debug/Mod_flQrxq/ModCode/dll/` (committed in the submodule): referenced by `ModMain.csproj` and the unit tests |
| Build + deploy | task **All: Rebuild & Deploy** (`GuiGuBaHuang-ModLib/scripts/rebuild-and-deploy-all.ps1`): rebuilds 3161035078, copies ModLib + Taoist to the Workshop folder |
| Release | task **Release: Tag Taoist** → tag `Taoist-<TAOIST_VERSION>` → `.github/workflows/release.yml` (zip of `3161035078/debug/Mod_nE7UL2`, notes "Taoist needs ModLib": `releaseRequires`) |
| Nexus | variables `NEXUS_TAOIST_FILE_ID` / `NEXUS_TAOIST_URL` + secret `NEXUSMODS_API_KEY` of this repository: task **Setup: Nexus Mods (CI)** |
| ModLib | released by GuiGuBaHuang-ModLib (task **Release: Tag ModLib** tags the submodule; Nexus: task **Setup: Nexus Mods ModLib (CI)**). When Taoist needs a new ModLib: change ModLib in the submodule, build it (`GuiGuBaHuang-ModLib/3385996759/scripts/build-project.ps1`), commit + push there with its DLLs, release it, then commit the submodule here |
