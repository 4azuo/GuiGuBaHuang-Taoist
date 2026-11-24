## Project Context

- **3161035078** - Taoist mod (uses ModLib)
- **GuiGuBaHuang-ModLib** - submodule: ModLib (sources `ModLib/`, Workshop project `3385996759/` with `SampleConfs/`, the ModLib.dll used here) and the shared tools; its own instructions: `GuiGuBaHuang-ModLib/.github/`
- **GuiGuBaHuang-ModCreator** - submodule: ModCreator tool (on hold); its own instructions: `GuiGuBaHuang-ModCreator/.github/`

## Working Principles

### Structure
- **ModConf/** - JSON configs (name as `*_<original>.json`, reference SampleConfs)
- **ModCode/** - C# source (compiles to ModCode/dll/)
- **GuiGuBaHuang-ModLib/3385996759/SampleConfs/** - Templates for all config types

### Guidelines
- Read instruction files before modifying matching code
- Use existing Helper utilities before creating new ones
- Check SampleConfs when working with configs
- Search for existing patterns before implementing new features
- Read docs from `docs/` for context

### Workflow
- Use `rebuild-and-deploy.ps1` in `<ProjectId>/scripts/` for build/deploy (or `GuiGuBaHuang-ModLib/scripts/rebuild-and-deploy-all.ps1` for all projects)
- Output: `debug/Mod_xxxxx/ModCode/dll/`
- Batch independent operations when possible
- Use LocalText.json for all user-facing text

## Documentation

Files in `docs/`: `README.md` (map), `Known-Issues.md`, `Taoist/` (Taoist features, configs, test scenarios, autotest, build / release).

In the submodules:
- **GuiGuBaHuang-ModLib/docs/guide/** - writing a mod with ModLib (setup, ModMain, events, configs, LocalText, helpers, debug, build)
- **GuiGuBaHuang-ModLib/docs/** - ModLib architecture, event reference, cache, built-in events, UI framework, shared tools
- **GuiGuBaHuang-ModLib/docs/ModConf/00-SampleConfs-List.md** - Config samples list
- **GuiGuBaHuang-ModLib/docs/GameAssets/**, **Effects/** - Pictures and effects of the game (Git LFS)
- **GuiGuBaHuang-ModCreator/docs/** - ModCreator tool

Reference appropriate docs when answering questions.