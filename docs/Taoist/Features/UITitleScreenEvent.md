# UITitleScreenEvent

`3161035078/ModProject/ModCode/ModMain/Mod/UITitleScreenEvent.cs` - cache key `ModConst.UI_TITLE_SCREEN_EVENT` (Global) - group: UI

Title screen additions: a version button, the Workshop description and a warning when ModLib is too old. The Taoist
wallpapers come from ModLib (see below).

## Flow

| Hook | What happens |
|---|---|
| `OnOpenUIEnd(Login)` | 1. Button `Taoist {TAOIST_VERSION}` in the row of the mods at the bottom center (ModLib `TitleModHelper.AddModButton`, next to the other mods): opens the Taoist cache folder in Explorer (`CacheHelper.GetCacheFolderName`). 2. Opens (`ShowWorkshopInfo`, through `UICustom1.WhenFree`: after the window of another mod, e.g. the error reports consent of ModLib, never over it) a `UITextInfoLong` titled `other500020046` with the Workshop description downloaded from the Workshop page (`HttpHelper.GetWorkshopDescription`, id 3161035078). 3. If ModLib's version < `ModConst.MODLIB_REQUIRED_VERSION`: a `UITextInfo` warning `ui_modlib_too_old` (`UICustom2.WhenFree`) (Taoist version, ModLib version, required). |

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
- No saved data, no switch.

## Tests

TT-01, TT-09 (wallpapers; TT-08: the ModLib buttons of the title screen).
