# Documentation

The mods of this repository: making a mod with ModLib (setup, ModMain, events, configs, texts, helpers, debug, build),
the Taoist mod (3161035078) and the known issues. Start with `00-Getting-Started.md`; for Taoist, `Taoist/00-Overview.md`.

The docs of the submodules live in their own repositories (paths below are from the repository root):

- `GuiGuBaHuang-ModLib/docs/`: ModLib internals, the game's config samples (`ModConf/`), the recorded effects (`Effects/`)
  and the pictures extracted from the game (`GameAssets/`)
- `GuiGuBaHuang-ModCreator/docs/`: the ModCreator tool

## Tree

```
docs/
├── README.md                     this file: the map of the documentation
├── 00-Getting-Started.md         setup, repository structure, first mod
├── 01-ModMain.md                 the ModMain class and the initialization
├── 02-Events.md                  mod events (ModEvent): creating them, their hooks
├── 03-Configs.md                 config files (ModConf/*.json)
├── 04-LocalText.md               localization: *_localText.json, locale folders, icon tokens
├── 05-Helpers.md                 the helpers of ModLib
├── 06-Debug.md                   debugging, game / mod log locations, run-game.ps1
├── 07-Build-Deploy.md            build and deploy scripts (Release, Steam Workshop)
├── Known-Issues.md               open bugs to check (reproduced by the unit tests' KnownIssues)
│
├── Taoist/                       the Taoist mod
│   ├── 00-Overview.md            structure, event order
│   ├── 01-Features.md            every feature by event (hooks, saved data, S&M options, tests)
│   ├── 02-Configs.md             ModSettings / configs of Taoist
│   ├── 03-Test-Scenarios.md      the test scenarios (TT / TG / TB / TX / TS / TR ...) step by step
│   ├── 04-Autotest.md            the autotest runner, evidence, unit tests, viewers, promo, marketing video
│   ├── 05-Backlog.md             open work and former TODO notes
│   ├── 06-Storylines-Draft.md    design of the NPC storylines A-L and the links between them
│   ├── 07-Paths-Feasibility.md   feasibility of the side cultivation paths (Artisan Dao, spirit sense, Dao heart, merit)
│   ├── Features/                 one page per class of ModMain/Mod (saved data, switches, flow, formulas, UI)
│   │   └── README.md             index by group + points found while documenting
│   └── Storylines/               dialogue of every line
│       ├── 00-Conventions.md
│       ├── A-Cold-Sword.md .. F-Red-Lotus.md               lines A-F (early game)
│       └── G-Hundred-Year-Wager.md .. L-Heart-Demon.md     lines G-L (lifelong, middle and high realms)

GuiGuBaHuang-ModLib/docs/         docs of the ModLib submodule (also on github.com/4azuo/GuiGuBaHuang-ModLib)
├── README.md                     map of the ModLib docs
├── 00-Architecture.md .. 05-Attributes-Objects.md   ModLib internals: dispatch, load sequence, monthly tick,
│                                 event reference, caches, built-in events, UI framework, attributes / objects
├── ModConf/                      the game's config samples (GuiGuBaHuang-ModLib/3385996759/SampleConfs): 00-SampleConfs-List.md + 01..12
├── Effects/                      effects of the game recorded by the galleries TE-xx (WebP, Git LFS)
└── GameAssets/                   pictures extracted from the game (Git LFS): index.html gallery, TextIcons, ItemIcons,
                                  Buildings, DramaBackgrounds

GuiGuBaHuang-ModLib/effect-viewer/  viewer of Effects/ (data: python GuiGuBaHuang-ModLib/effect-viewer/update-data.py)

GuiGuBaHuang-ModCreator/docs/     docs of the ModCreator submodule (on hold): 00-Overview.md + 01..05 (one per feature)
```

## Where to look

| I want to... | Read |
|---|---|
| set up the environment, build, deploy | `00-Getting-Started.md`, `07-Build-Deploy.md` |
| write a feature (event, UI, config, text) | `02-Events.md`, `../GuiGuBaHuang-ModLib/docs/04-UI-Framework.md`, `03-Configs.md`, `04-LocalText.md` |
| find a config type of the game and its fields | `../GuiGuBaHuang-ModLib/docs/ModConf/00-SampleConfs-List.md` |
| know what a Taoist feature does, its hooks and tests | `Taoist/01-Features.md` (summary), `Taoist/Features/<Class>.md` (details, formulas) |
| run / read the autotests, the viewers, the promo pictures, the video | `Taoist/04-Autotest.md` |
| see open work | `Taoist/05-Backlog.md`, `Known-Issues.md` |
| write or change a storyline | `Taoist/06-Storylines-Draft.md`, `Taoist/Storylines/` |
| pick an icon for a text, an item icon or a background for a drama | `../GuiGuBaHuang-ModLib/docs/GameAssets/index.html` |
| find a battle / skill effect for a custom skill | `../GuiGuBaHuang-ModLib/effect-viewer/index.html` (pictures in `../GuiGuBaHuang-ModLib/docs/Effects/`) |
| debug the game, read the logs | `06-Debug.md` |
| understand ModLib (dispatch, caches, built-in events) | `../GuiGuBaHuang-ModLib/docs/00-Architecture.md` |
| use the ModCreator tool | `../GuiGuBaHuang-ModCreator/docs/00-Overview.md` |

## Pictures extracted from the game

The scripts stay in this repository and write into the ModLib submodule (commit them there, then the submodule here):
`python GuiGuBaHuang-ModLib/scripts/extract-game-assets.py` -> `GuiGuBaHuang-ModLib/docs/GameAssets/`, the effect galleries TE-xx
(`GuiGuBaHuang-ModLib/scripts/game/run-effect-gallery.ps1`, `collect-effects.ps1`) -> `GuiGuBaHuang-ModLib/docs/Effects/`. Details:
`GuiGuBaHuang-ModLib/docs/README.md`.
