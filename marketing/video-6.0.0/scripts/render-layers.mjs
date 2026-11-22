// The layers of the cherry tree avenue (src/Sakura.tsx AvenueLayer), drawn once to public/avenue/<layer>.png with a
// transparent background (1.5x the screen, so the dolly keeps them sharp): the video moves and sways these pictures
// instead of drawing every tree on every frame. Run by npm run render / studio (after prepare-assets); a layer is
// drawn again only when src/Sakura.tsx is newer than its picture (or with --force).
import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..");
const out = path.join(root, "public", "avenue");
const source = path.join(root, "src", "Sakura.tsx");
const force = process.argv.includes("--force");
fs.mkdirSync(out, { recursive: true });
const cli = path.join(root, "node_modules", "@remotion", "cli", "remotion-cli.js");
for (const layer of ["far", "mid", "nearL", "nearR"]) {
  const file = path.join(out, `${layer}.png`);
  if (!force && fs.existsSync(file) && fs.statSync(file).mtimeMs > fs.statSync(source).mtimeMs) continue;
  const props = path.join(out, `${layer}.props.json`);
  fs.writeFileSync(props, JSON.stringify({ layer }));
  execFileSync(process.execPath, [cli, "still", "AvenueLayer", file, `--props=${props}`, "--image-format=png", "--scale=1.5", "--log=error"],
    { cwd: root, stdio: "inherit" });
  fs.rmSync(props);
  console.log(`avenue layer: ${layer} -> public/avenue/${layer}.png`);
}
