// Assets of the marketing videos (none of them in git):
// - copies the pictures of 3161035078/ModProject/SteamImg to public/steam (served by Remotion);
// - downloads the fonts (Google Fonts, OFL: Ma Shan Zheng brush characters, Cinzel titles) to public/fonts once;
// - writes src/promo.generated.json: the plan of each video (intro, chapters with their pictures and captions - the
//   "promo" entries of 3161035078/tests/scenarios.json, pictures taken by scripts/game/run-promo.ps1 - and outro, in
//   beats; src/chapters.json gives the chapters, the timing and the music);
// - cuts the song to the timeline of the video (scripts/cut-music.py -> public/music-long.wav): every cut on a beat,
//   each drop of the song on a chapter card.
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..");
const repo = path.resolve(root, "..", "..");
const steamImg = path.join(repo, "3161035078", "ModProject", "SteamImg");
const scenariosFile = path.join(repo, "3161035078", "tests", "scenarios.json");
const pub = path.join(root, "public");
const steam = path.join(pub, "steam");
const timing = JSON.parse(fs.readFileSync(path.join(root, "src", "chapters.json"), "utf8"));
const music = timing.music;

// --- pictures
if (!fs.existsSync(steamImg)) throw new Error(`No SteamImg folder: ${steamImg}`);
fs.rmSync(steam, { recursive: true, force: true });
fs.mkdirSync(steam, { recursive: true });
// SteamImg/<picture> and SteamImg/promo-<version>/promo_<scenario>_<name>.jpg: copied flat (the names are unique)
const isPicture = (f) => /\.(png|jpe?g)$/i.test(f);
const sources = new Map();
for (const f of fs.readdirSync(steamImg)) {
  const full = path.join(steamImg, f);
  if (fs.statSync(full).isDirectory()) {
    if (f.startsWith("promo-")) for (const g of fs.readdirSync(full).filter(isPicture)) sources.set(g, path.join(full, g));
  } else if (isPicture(f)) sources.set(f, full);
}
const files = [...sources.keys()];
for (const [f, src] of sources) fs.copyFileSync(src, path.join(steam, f));

const scenarios = JSON.parse(fs.readFileSync(scenariosFile, "utf8").replace(/^﻿/, "")).scenarios ?? [];
// the pictures are SteamImg/promo-<version>/promo_<scenario>_<name>.jpg (collect-promo.ps1); chapters.json "scenarios": only the
// pictures of these scenarios (the 6.0 video: its own set TR-04)
const promo = scenarios
  .filter((s) => !Array.isArray(timing.scenarios) || timing.scenarios.includes(s.id))
  .flatMap((s) => (s.promo ?? []).map((p) => ({ name: p.name, file: `promo_${s.id}_${p.name}.jpg`, caption: p.caption ?? "" })))
  .filter((p) => files.includes(p.file));
const extra = files.filter((f) => !f.startsWith("promo_") && !/^preview\./i.test(f)).sort();
console.log(`SteamImg: ${files.length} picture(s), ${promo.length} with a caption -> public/steam`);

// --- fonts
const fonts = {
  "MaShanZheng-Regular.ttf": "https://raw.githubusercontent.com/google/fonts/main/ofl/mashanzheng/MaShanZheng-Regular.ttf",
  "Cinzel.ttf": "https://raw.githubusercontent.com/google/fonts/main/ofl/cinzel/Cinzel%5Bwght%5D.ttf",
};
fs.mkdirSync(path.join(pub, "fonts"), { recursive: true });
for (const [name, url] of Object.entries(fonts)) {
  const file = path.join(pub, "fonts", name);
  if (fs.existsSync(file)) continue;
  const res = await fetch(url);
  if (!res.ok) throw new Error(`Font ${name}: HTTP ${res.status} (${url})`);
  fs.writeFileSync(file, Buffer.from(await res.arrayBuffer()));
  console.log(`font: ${name}`);
}

// --- plan: the chapters of the video with their pictures present in SteamImg (empty chapters skipped)
const slideOf = (p) => ({ file: p.file, caption: p.caption, name: p.name });
const partsFor = () => {
  if (promo.length === 0) return [{ title: "TAOIST", subtitle: "", glyph: "道", number: 1, scenes: extra.map((file) => [{ file }]) }];
  const named = (n) => promo.find((p) => p.name === n);
  const parts = timing.chapters
    .map((c) => ({
      title: c.title, subtitle: c.subtitle, glyph: c.glyph,
      // a scene: one picture (a name) or the pictures of one feature (a list of names)
      scenes: c.pictures.map((x) => [x].flat().map(named).filter(Boolean).map(slideOf)).filter((x) => x.length > 0),
    }))
    .filter((p) => p.scenes.length > 0);
  // the pictures of no chapter are shown in the last one
  if (parts.length > 0 && timing.extras !== false)
    parts[parts.length - 1].scenes.push(...promo.filter((p) => !timing.chapters.some((c) => c.pictures.flat().includes(p.name))).map((p) => [slideOf(p)]));
  return parts.map((p, i) => ({ ...p, number: i + 1 }));
};
const partBeats = (p) => timing.chapterBeats + p.scenes.flat().length * timing.slideBeats;

/** A section of the song cut to `take` beats: the end of it for a build-up (its riser leads into the drop), else its
 *  first 16 beats and its end (so the next piece follows on); longer than the section: its middle played again (it
 *  still ends on its end). */
const fitSection = (name, take) => {
  const [s, e] = music.sections[name];
  const len = e - s;
  if (take <= 0) return [];
  if (take === len) return [[s, len]];
  if (take > len) {
    const extra = Math.min(take - len, len - 32);
    return [[s, 16 + extra], ...fitSection.rest(name, s + 16, take - 16 - extra)];
  }
  if (name === "build" || name === "break") return [[e - take, take]];
  const head = Math.min(16, take);
  return [[s, head], ...(take > head ? [[e - (take - head), take - head]] : [])];
};
/** From beat `from` of a section to its end, `take` beats (the middle looped when longer). */
fitSection.rest = (name, from, take) => {
  const [, e] = music.sections[name];
  if (take <= e - from) return [[e - take, take]];
  return [[from, e - from - 16], ...fitSection.rest(name, from, take - (e - from - 16))];
};

/** The song pieces of the video: intro, the body sections (each one after the first ends on a chapter card, so the next
 *  section - a drop - starts on one; the last one fills the rest and ends where the outro starts), outro. */
const musicPieces = (variant, parts) => {
  const v = music[variant];
  const bodyBeats = parts.reduce((s, p) => s + partBeats(p), 0);
  const cards = parts.reduce((a, p) => [...a, a[a.length - 1] + partBeats(p)], [0]);
  const pieces = [[v.intro[0], v.intro[1] - v.intro[0]]];
  let at = 0;
  v.body.forEach((name, i) => {
    const [s, e] = music.sections[name];
    let take;
    if (i === v.body.length - 1) take = bodyBeats - at;
    else {
      // a card where the section ends, keeping at least half of it; else the whole section
      const fits = cards.filter((b) => b - at >= (e - s) / 2 && b - at <= e - s && b <= bodyBeats - 16);
      take = fits.length > 0 ? Math.max(...fits) - at : Math.max(0, Math.min(e - s, bodyBeats - 16 - at));
    }
    pieces.push(...fitSection(name, take));
    at += take;
  });
  pieces.push([v.outro[0], v.outro[1] - v.outro[0]]);
  // pieces that follow on in the song are one piece
  return pieces.reduce((a, p) => {
    const last = a[a.length - 1];
    if (last && last[0] + last[1] === p[0]) last[1] += p[1];
    else a.push([...p]);
    return a;
  }, []);
};

const plans = {};
const song = path.join(pub, "song.wav");
const mp3 = path.join(pub, music.file);
if (!fs.existsSync(song) || fs.statSync(song).mtimeMs < fs.statSync(mp3).mtimeMs)
  // the ffmpeg shipped with Remotion
  execFileSync(process.execPath, [path.join(root, "node_modules", "@remotion", "cli", "remotion-cli.js"), "ffmpeg", "-y", "-loglevel", "error",
    "-i", mp3, "-ar", "44100", "-ac", "2", song], { cwd: root, stdio: "inherit" });
{
  const parts = partsFor();
  const v = music.long;
  plans.long = { intro: v.intro[1] - v.intro[0], outro: v.outro[1] - v.outro[0], parts };
  const tl = path.join(root, "src", "timeline-long.generated.json");
  fs.writeFileSync(tl, JSON.stringify({ bpm: music.bpm, firstBeat: music.firstBeat, pieces: musicPieces("long", parts) }, null, 2));
  process.stdout.write(execFileSync("python", [path.join(here, "cut-music.py"), song, tl, path.join(pub, "music-long.wav")], { encoding: "utf8" }));
}
fs.writeFileSync(path.join(root, "src", "promo.generated.json"), JSON.stringify(plans, null, 2));
