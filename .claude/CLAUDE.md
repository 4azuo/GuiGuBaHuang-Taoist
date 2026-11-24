# GuiGuBaHuang-Taoist

Mods for Gui Gu Ba Huang (鬼谷八荒), C# (.NET Framework 4.7.2) + MelonLoader/Harmony.

## Projects

- **GuiGuBaHuang-ModLib** - ModLib (submodule, public repo `4azuo/GuiGuBaHuang-ModLib`): sources `ModLib/` (`ModLib.csproj`), Workshop project `3385996759/` (ModMain + configs + SampleConfs; its solution builds `ModLib/ModLib.csproj`; build output `3385996759/debug/Mod_flQrxq` provides ModLib.dll), shared tools. A change there is committed and pushed in the submodule, then the new submodule commit is committed here. ModLib is built and released in its own repository
- **3161035078** - Taoist mod (uses ModLib, also the reference example mod)
- **GuiGuBaHuang-ModCreator** - WPF tool for creating/editing mod projects (submodule; development on hold indefinitely)

## Structure

- **ModProject/ModConf/** - JSON configs (name as `*_<original>.json`, reference SampleConfs)
- **ModProject/ModCode/** - C# source (solution: `ModProject/ModCode/ModMain/ModMain.sln`)
- **ModProject/ModImg/** - Assets
- **GuiGuBaHuang-ModLib/3385996759/SampleConfs/** - Templates for all game config types
- **debug/Mod_xxxxx/** - Build output (`ModCode/dll/`, `ModConf/`, `ModImg/`)
- **<ProjectId>/scripts/** - Per-project `settings.json` + 2-line wrappers of the shared build/deploy scripts (`GuiGuBaHuang-ModLib/scripts/project/`)
- **scripts/** - `settings.json` (projects, dependencies, and the mod for the shared tools: `modProject`, `modName`, `modId`), `setup/` (Taoist steps of the first-time install: install, clone, ModCreator resources), `game/export-scenarios-doc.py`; every other script is shared in `GuiGuBaHuang-ModLib/scripts/` (run from this repository: it works on the git repository of the current folder)
- **Shared tools in ModLib** (used by every mod repository holding ModLib, e.g. Dao of Creation; the data stays in the mod repository): `GuiGuBaHuang-ModLib/scripts/game/` (game runs, autotest, game slots, promo, effect galleries), `scripts/project/` (build/deploy), `test-viewer/` + `promo-viewer/` (pages generated into `logs/test-viewer/` / `logs/promo-viewer/` here), `Translate/` (cache in `.translate-cache/` here), `tests/` (shared unit test setup `tests/UnitTests.props`; the ModLib tests run in ModLib), `tools/SlotDataPath/`, `ModLib/AutoTest/` (the in-game autotest runner: `Mod/AutoTestEvent.cs` here is its Taoist subclass), `docs/guide/` (writing a mod with ModLib). A change there is a ModLib change (commit + push in the submodule)

## Guidelines

- Folder-specific rules live in `.claude/rules/` (Taoist) and load automatically for matching files; for a change in the submodules, read their own `CLAUDE.md` and `.claude/rules/` first (`GuiGuBaHuang-ModLib/CLAUDE.md`, `GuiGuBaHuang-ModCreator/.claude/rules/`)
- Use existing Helper utilities before creating new ones
- Check SampleConfs when working with configs
- Search for existing patterns before implementing new features
- Use LocalText.json for all user-facing text
- No TODO notes in code: write open work in `docs/Taoist/05-Backlog.md`
- Batch independent operations when possible

## Build & Deploy

Always build in Release. Run scripts with `powershell -NoProfile -ExecutionPolicy Bypass -File <script>`.

- Build one project: `<ProjectId>/scripts/build-project.ps1` (clean + build: `rebuild-project.ps1`)
- Build + deploy to Steam Workshop: `<ProjectId>/scripts/rebuild-and-deploy.ps1`
- All projects: `GuiGuBaHuang-ModLib/scripts/rebuild-and-deploy-all.ps1`
- Auto-build watcher writes failures to `<ProjectId>/scripts/auto-built-errors.log` (use `/fix-build-errors`)
- 3161035078 references the ModLib.dll committed in the submodule (`GuiGuBaHuang-ModLib/3385996759/debug/Mod_flQrxq/ModCode/dll/`): after a ModLib change, build ModLib (`GuiGuBaHuang-ModLib/3385996759/scripts/build-project.ps1`), commit its DLL in the submodule, then build Taoist
- Release: each repository releases its own mod (`GuiGuBaHuang-ModLib/scripts/release/create-release-tag.ps1`; task **Release: Tag Taoist** here; ModLib from its own repository and its VS Code tasks), no combined release
- Same scripts are exposed as VS Code tasks in `.vscode/tasks.json`
- Environment check: `GuiGuBaHuang-ModLib/scripts/setup/04-verify-environment.ps1` (many "missing type/member" build errors after a game update = stale `MelonLoader\Managed`, not code bugs)

## Testing in game

- Run the game and collect logs: `GuiGuBaHuang-ModLib/scripts/game/run-game.ps1` (e.g. `-UntilModLoaded -StopGame -Duration 600`); read `logs/game-runs/<latest>/summary.json` first, then `timeline.log`
- Paths of game / Workshop / log folders: `GuiGuBaHuang-ModLib/scripts/open-folder.ps1 <target> -Print`
- Details: `GuiGuBaHuang-ModLib/docs/guide/06-Debug.md`
- Unit tests (no game): `GuiGuBaHuang-ModLib/scripts/run-unittests.ps1` (`tests/UnitTests`; `-IncludeKnownIssues` runs the tests reproducing open bugs)
- Known bugs to check: `docs/Known-Issues.md`
- Taoist autotest (scenarios + evidence): `GuiGuBaHuang-ModLib/scripts/game/run-autotest.ps1 -Save <test save>` -> evidence in `logs/autotest/<time>/`; OK/NG per scenario id in `3161035078/tests/scenario-status.json`. All results (autotest + unit tests, every run, screenshots by step): `logs/test-viewer/index.html` (data from `GuiGuBaHuang-ModLib/test-viewer/update-data.ps1`, run after every test run). Prefer `-Scenario` / `-Failed` (NG and NOT RUN ids) over full runs; TP-01 (new character) only runs for a full run or scenarios with `"newCharacter": true`. Keep a run: `GuiGuBaHuang-ModLib/scripts/pin-test-run.ps1` (a full all-pass run is pinned automatically). Several sessions may run autotests at once: each run takes a free game slot (slot copies 2..5) and never closes another run's game; `-Deploy` waits for the other games before copying a new build. Details: `docs/Taoist/04-Autotest.md`

## Documentation

Read the relevant doc from `docs/` before answering questions or making non-trivial changes:

- `README.md` - Map of the documentation (tree, where to look)
- `Known-Issues.md` - open bugs to check
- `Taoist/00-Overview.md` - Taoist structure and event order (`01` features per event + known issues, `Features/<Class>.md` one detailed page per Mod class, `02` configs + LocalText files / id blocks, `03` test scenarios, `04` autotest/evidence/unit tests, `05` backlog: open work and former TODO notes, `06` storyline design, `Storylines/` dialogue per line, `08` build / deploy / release of Taoist)

Docs of the submodules (in their own repositories):

- `GuiGuBaHuang-ModLib/docs/guide/` - writing a mod with ModLib: `00-Getting-Started` (setup and structure), `01-ModMain`, `02-Events`, `03-Configs`, `04-LocalText`, `05-Helpers`, `06-Debug` (log locations), `07-Build-Deploy` (build / deploy / release scripts)
- `GuiGuBaHuang-ModLib/docs/README.md` - Map of the ModLib docs; `00-Architecture.md` - ModLib internals: dispatch, load sequence, monthly tick (`01` event reference, `02` cache/persistence, `03` built-in events, `04` UI framework, `05` attributes/objects/enums, `06` shared tools of the mod repositories: what lives in ModLib, what a mod repository provides)
- `GuiGuBaHuang-ModLib/docs/ModConf/00-SampleConfs-List.md` - Index of config samples (per-category files `01`-`12`)
- `GuiGuBaHuang-ModLib/docs/GameAssets/` - icons, item icons, buildings and drama backgrounds extracted from the game (`GuiGuBaHuang-ModLib/scripts/extract-game-assets.py`); `Effects/` - effects recorded by the galleries TE-xx (viewer: `GuiGuBaHuang-ModLib/effect-viewer/`) (both Git LFS, written by the scripts of this repository: commit them in the submodule)
- `GuiGuBaHuang-ModCreator/docs/00-Overview.md` - ModCreator tool (per-feature files `01`-`05`)
