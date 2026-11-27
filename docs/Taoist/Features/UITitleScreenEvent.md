# UITitleScreenEvent

`3161035078/ModProject/ModCode/ModMain/Mod/UITitleScreenEvent.cs` (+ `.OtherMods.cs`, `.Config.cs`) - cache key `ModConst.UI_TITLE_SCREEN_EVENT` (Global) - group: UI

Title screen additions: a version button, the Workshop description, a warning when ModLib is too old and the panel of
the other mods of the author. The Taoist wallpapers come from ModLib (see below).

## Flow

| Hook | What happens |
|---|---|
| `OnOpenUIEnd(Login)` | 1. Button `Taoist {TAOIST_VERSION}` in the row of the mods at the bottom center (ModLib `TitleModHelper.AddModButton`, next to the other mods): opens the Taoist cache folder in Explorer (`CacheHelper.GetCacheFolderName`). 2. Opens (`ShowWorkshopInfo`, through `UICustom1.WhenFree`: after the window of another mod, e.g. the error reports consent of ModLib, never over it) a `UITextInfoLong` titled `other500020046` with the Workshop description downloaded from the Workshop page (`HttpHelper.GetWorkshopDescription`, id 3161035078). 3. If ModLib's version < `ModConst.MODLIB_REQUIRED_VERSION`: a `UITextInfo` warning `ui_modlib_too_old` (`UICustom2.WhenFree`) (Taoist version, ModLib version, required). 4. `ShowOtherMods`: the panel of the other mods (below). |
| `OnTimeUpdate1000ms` (title screen) | `RotateOtherMods`: the next mod of the panel every `RotateSec`; the labels of the title buttons set again. |

## Other mods panel ("More mods by Fouru")

`UITitleScreenEvent.OtherMods.cs`, settings `ModConf/title_othermods_configs.json` (`TitleOtherModsConfig`: `Enabled`,
`RotateSec`, `Mods`: `ModId`, `Name`, `Text`, `Image`, `Url`, `Button`). In the free space on the left of the title
screen, between the buttons of the game and the middle of the wallpaper (`PANEL_X`, `PANEL_Y`, `PANEL_W`: shares of the
screen, placed as `TitleModHelper` places its row: screen point -> world point of the layer of the buttons): a dark
panel with the title `ui_othermods_title`, the picture of the mod (`ModImg`, 16:9), its name, its text (a localText key
of Taoist or the text itself) and a button (`Button`, default `ui_othermods_open`) opening `Url` (http/https only; no
address: no button). Only the mods not loaded in this game (`g.mod.IsLoadMod(ModId)`; no `ModId`: always) are shown: the
player who has Dao of Creation does not see it; no mod left: no panel. Several mods: one at a time, the next every
`RotateSec` (at least 3). Built again at every title screen (the one of an older title screen removed). Now: Dao of
Creation (`othermods_doc.jpg`, `ui_othermods_doc`, Discord). A new mod: an entry in the file, its picture in `ModImg`,
its text in `ui_localText.json` (+ the translated folders).

## Version compare

`CompareVersion(req, cur)`: both split on non-digits, compared part by part; equal prefix = OK.

## Wallpapers

No code: one file per wallpaper in `3161035078/ModProject/ModConf/`: `taoist_1_wallpaper.json` and
`taoist_2_wallpaper.json` (images `ModImg/taoist_1_wallpaper.png`, `taoist_2_wallpaper.png`, 2560x1440; each
`hideGameTitle: true`, `startupRate: 0.25`). ModLib (`WallpaperHelper`, guide
`GuiGuBaHuang-ModLib/docs/guide/08-Wallpapers.md`) lists them in the game's Change Theme window, shows the chosen one
(saved in the ModLib global settings) and, on the first title screen of a game session, shows one of them at random
half of the time (this session only; no draw when the player already chose a Taoist wallpaper). The game's title is
hidden over them unless the player turns off the toggle of the Change Theme window.

## Notes

- The description is downloaded at every title screen (network access); a failure is handled by `HttpHelper`.
- No saved data; the panel of the other mods: `Enabled` of its file.

## Tests

TT-01, TT-09 (wallpapers; TT-08: the ModLib buttons of the title screen).
