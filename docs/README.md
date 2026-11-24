# Documentation

The Taoist mod (3161035078) and its known issues. Start with `Taoist/00-Overview.md`.

How to write a mod with ModLib (setup, ModMain, events, configs, texts, helpers, debug, build / deploy / release) and the
ModLib internals live in the ModLib submodule: `GuiGuBaHuang-ModLib/docs/` (start with
`GuiGuBaHuang-ModLib/docs/guide/00-Getting-Started.md`). The ModCreator tool: `GuiGuBaHuang-ModCreator/docs/`.

## Tree

```
docs/
├── README.md                     this file: the map of the documentation
├── Known-Issues.md               open bugs to check (reproduced by the unit tests' KnownIssues)
│
└── Taoist/                       the Taoist mod
    ├── 00-Overview.md            structure, event order, InfoDialog
    ├── 01-Features.md            every feature by event (hooks, saved data, S&M options, tests)
    ├── 02-Configs.md             ModSettings / configs of Taoist, LocalText files and id blocks
    ├── 03-Test-Scenarios.md      the test scenarios (TT / TG / TB / TX / TS / TR ...) step by step
    ├── 04-Autotest.md            the autotest runner, evidence, unit tests, viewers, promo, marketing video
    ├── 05-Backlog.md             open work and former TODO notes
    ├── 06-Storylines-Draft.md    design of the NPC storylines A-L and the links between them
    ├── 07-Paths-Feasibility.md   feasibility of the side cultivation paths (Artisan Dao, spirit sense, Dao heart, merit)
    ├── 08-Build-Release.md       what is specific to Taoist in build / deploy / release (projects, ModLib.dll, tags, Nexus)
    ├── Features/                 one page per class of ModMain/Mod (saved data, switches, flow, formulas, UI)
    │   └── README.md             index by group + points found while documenting
    └── Storylines/               dialogue of every line
        ├── 00-Conventions.md
        ├── A-Cold-Sword.md .. F-Red-Lotus.md               lines A-F (early game)
        └── G-Hundred-Year-Wager.md .. L-Heart-Demon.md     lines G-L (lifelong, middle and high realms)

GuiGuBaHuang-ModLib/              the ModLib submodule (github.com/4azuo/GuiGuBaHuang-ModLib)
├── INSTALLATION.md               setup of ModLib alone
└── docs/
    ├── README.md                 map of the ModLib docs
    ├── guide/                    writing a mod with ModLib: 00-Getting-Started, 01-ModMain, 02-Events, 03-Configs,
    │                             04-LocalText, 05-Helpers, 06-Debug, 07-Build-Deploy, 08-Wallpapers
    ├── 00-Architecture.md .. 05-Attributes-Objects.md   ModLib internals
    ├── 06-Shared-Tools.md        the tools shared by the mod repositories (scripts, viewers, Translate, tests, setup)
    ├── ModConf/                  the game's config samples (3385996759/SampleConfs): 00-SampleConfs-List.md + 01..12
    ├── Effects/                  effects of the game recorded by the galleries TE-xx (WebP, Git LFS)
    └── GameAssets/               pictures extracted from the game (Git LFS)

GuiGuBaHuang-ModCreator/docs/     docs of the ModCreator submodule (on hold): 00-Overview.md + 01..05 (one per feature)
```

## Where to look

| I want to... | Read |
|---|---|
| set up the machine | `../INSTALLATION.md` |
| build, deploy, release Taoist | `Taoist/08-Build-Release.md`, `../GuiGuBaHuang-ModLib/docs/guide/07-Build-Deploy.md` |
| write a feature (event, UI, config, text) | `../GuiGuBaHuang-ModLib/docs/guide/02-Events.md`, `../GuiGuBaHuang-ModLib/docs/04-UI-Framework.md`, `guide/03-Configs.md`, `guide/04-LocalText.md` (+ the Taoist files: `Taoist/02-Configs.md`) |
| add or change a title screen wallpaper | `Taoist/02-Configs.md` (ModLib files), `../GuiGuBaHuang-ModLib/docs/guide/08-Wallpapers.md` |
| find a config type of the game and its fields | `../GuiGuBaHuang-ModLib/docs/ModConf/00-SampleConfs-List.md` |
| know what a Taoist feature does, its hooks and tests | `Taoist/01-Features.md` (summary), `Taoist/Features/<Class>.md` (details, formulas) |
| run / read the autotests, the viewers, the promo pictures, the video | `Taoist/04-Autotest.md` |
| see open work | `Taoist/05-Backlog.md`, `Known-Issues.md` |
| write or change a storyline | `Taoist/06-Storylines-Draft.md`, `Taoist/Storylines/` |
| pick an icon, an item icon or a drama background | `../GuiGuBaHuang-ModLib/docs/GameAssets/index.html` |
| find a battle / skill effect for a custom skill | `../GuiGuBaHuang-ModLib/effect-viewer/index.html` |
| debug the game, read the logs | `../GuiGuBaHuang-ModLib/docs/guide/06-Debug.md` |
| understand ModLib (dispatch, caches, built-in events) | `../GuiGuBaHuang-ModLib/docs/00-Architecture.md` |
| use the ModCreator tool | `../GuiGuBaHuang-ModCreator/docs/00-Overview.md` |
