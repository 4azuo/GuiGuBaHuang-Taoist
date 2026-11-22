import { useId, useMemo } from "react";
import { AbsoluteFill, Img, staticFile } from "remotion";

/**
 * The cherry blossom world of the 6.0 video: a spring sky, an avenue of cherry trees in full bloom (the camera moving
 * slowly down it all the video long), petals falling, gusts of petals, flowering branches, and the flower shapes the
 * transitions reveal through.
 */

export const SKY_TOP = "#9fd3ee";
export const SKY_LOW = "#fdeef2";
export const ROSE = "#d9577f";
export const DEEP = "#8e2f55";
export const PLUM = "#3e2432";
export const CREAM = "#fffaf7";
export const PINKS = ["#f9c9d6", "#f6b3c6", "#fbdde5", "#f4a3bb", "#fde9ee"];

/** Pseudo-random 0..1 (deterministic: the same frame renders the same). */
export const rand = (i: number, seed: number) => {
  const v = Math.sin(i * 127.1 + seed * 311.7) * 43758.5453;
  return v - Math.floor(v);
};
const clamp01 = (x: number) => Math.max(0, Math.min(1, x));
/** 0 -> 1 as x goes from a to b. */
export const span = (x: number, a: number, b: number) => clamp01((x - a) / (b - a));

/** A sakura petal (pointing right, notched tip), as an SVG path of length 24. */
export const PETAL = "M0 0 C 6 -11, 20 -10, 24 -2 C 21 -1, 21 1, 24 2 C 20 10, 6 11, 0 0 Z";

/** The path of a five-petal sakura flower of radius r at (cx, cy) turned `rot` degrees (notched petals). */
export const flowerPath = (cx: number, cy: number, r: number, rot = 0) => {
  let d = "";
  for (let i = 0; i < 5; i++) {
    const a = ((rot + i * 72) * Math.PI) / 180;
    const p = (x: number, y: number) => {
      // petal pointing up (-y), rotated by a
      const X = x * Math.cos(a) - y * Math.sin(a), Y = x * Math.sin(a) + y * Math.cos(a);
      return `${(cx + X * r).toFixed(1)} ${(cy + Y * r).toFixed(1)}`;
    };
    d += `M${p(0, 0)} C${p(-0.62, -0.3)} ${p(-0.58, -1.05)} ${p(-0.13, -1)} L${p(0, -0.84)} L${p(0.13, -1)} C${p(0.58, -1.05)} ${p(0.62, -0.3)} ${p(0, 0)}Z`;
  }
  return d;
};

/** CSS mask (mask-image) from SVG shapes on a 1920x1080 canvas (white: shown). */
export const svgMask = (shapes: string): React.CSSProperties => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1920" height="1080" viewBox="0 0 1920 1080">${shapes}</svg>`;
  const url = `url("data:image/svg+xml;utf8,${encodeURIComponent(svg)}")`;
  return { WebkitMaskImage: url, maskImage: url, WebkitMaskSize: "100% 100%", maskSize: "100% 100%" } as React.CSSProperties;
};

/** One flower (for decorations), r: radius. */
export const Flower: React.FC<{ x: number; y: number; r: number; rot?: number; light?: boolean; opacity?: number }> = ({ x, y, r, rot = 0, light, opacity = 1 }) => (
  <g opacity={opacity}>
    <path d={flowerPath(x, y, r, rot)} fill={light ? "#fde8ee" : "#f8c3d2"} stroke="#e597ae" strokeWidth={Math.max(0.6, r * 0.04)} />
    <circle cx={x} cy={y} r={r * 0.2} fill="#e0739a" />
    {[0, 1, 2, 3, 4].map((i) => {
      const a = ((rot + 36 + i * 72) * Math.PI) / 180;
      return <circle key={i} cx={x + Math.sin(a) * r * 0.38} cy={y - Math.cos(a) * r * 0.38} r={r * 0.06} fill="#a8304f" />;
    })}
  </g>
);

/**
 * A flowering branch drawn along a curve (grow 0 -> 1: the wood grows from its foot, the flowers open as it passes),
 * in a 1000x300 box: put it anywhere with a transform.
 */
export const Branch: React.FC<{ grow?: number; seed?: number; style?: React.CSSProperties; width?: number }> = ({ grow = 1, seed = 1, style, width = 1000 }) => {
  const main = "M0 210 C 180 150, 330 190, 470 130 S 760 40, 1000 70";
  const twigs = [
    { d: "M300 175 C 330 120, 350 100, 390 80", at: 0.3 },
    { d: "M520 118 C 560 160, 600 180, 660 185", at: 0.5 },
    { d: "M700 75 C 720 30, 760 15, 800 10", at: 0.68 },
    { d: "M160 180 C 170 230, 200 250, 240 262", at: 0.15 },
  ];
  // flowers: [x, y, r, at]
  const flowers: [number, number, number, number][] = useMemo(() => {
    const pts: [number, number, number, number][] = [
      [390, 80, 30, 0.33], [372, 98, 22, 0.32], [660, 185, 28, 0.52], [632, 176, 20, 0.51], [800, 10, 26, 0.7], [776, 26, 19, 0.69],
      [240, 262, 24, 0.17], [1000, 70, 32, 0.98], [968, 60, 22, 0.96], [980, 96, 20, 0.97], [470, 130, 24, 0.47], [880, 48, 24, 0.86],
      [120, 186, 20, 0.11], [560, 104, 18, 0.55],
    ];
    return pts.map(([x, y, r, at], i) => [x + (rand(i, seed) - 0.5) * 16, y + (rand(i, seed + 1) - 0.5) * 16, r * (0.85 + rand(i, seed + 2) * 0.3), at]);
  }, [seed]);
  const len = 1150;
  return (
    <svg viewBox="-40 -40 1080 340" width={width} height={(width * 340) / 1080} style={{ overflow: "visible", ...style }}>
      <path d={main} fill="none" stroke="#4a2f25" strokeWidth={16} strokeLinecap="round" strokeDasharray={len} strokeDashoffset={len * (1 - grow)} />
      <path d={main} fill="none" stroke="#6b4636" strokeWidth={6} strokeLinecap="round" strokeDasharray={len} strokeDashoffset={len * (1 - grow)} transform="translate(0 -4)" opacity={0.6} />
      {twigs.map((t, i) => {
        const g = span(grow, t.at, t.at + 0.18);
        return <path key={i} d={t.d} fill="none" stroke="#4a2f25" strokeWidth={7} strokeLinecap="round" strokeDasharray={200} strokeDashoffset={200 * (1 - g)} />;
      })}
      {flowers.map(([x, y, r, at], i) => {
        const o = span(grow, at, at + 0.12);
        if (o <= 0) return null;
        return <Flower key={i} x={x} y={y} r={r * (0.3 + 0.7 * o)} rot={rand(i, seed + 3) * 72} light={rand(i, seed + 4) > 0.5} opacity={o} />;
      })}
    </svg>
  );
};

/** The sky: blue at the top, blush at the horizon, the sun's glow and soft rays turning slowly. */
const Sky: React.FC<{ frame: number }> = ({ frame }) => (
  <AbsoluteFill style={{ background: `linear-gradient(180deg, ${SKY_TOP} 0%, #cfe8f4 38%, ${SKY_LOW} 62%, #fbe3ea 100%)` }}>
    <AbsoluteFill style={{ background: "radial-gradient(circle at 74% 18%, rgba(255,252,235,0.95) 0%, rgba(255,246,225,0.5) 12%, rgba(255,255,255,0) 34%)" }} />
    <AbsoluteFill style={{ opacity: 0.22, mixBlendMode: "screen",
      background: `repeating-conic-gradient(from ${frame * 0.04}deg at 74% 18%, rgba(255,255,255,0.9) 0deg 4deg, rgba(255,255,255,0) 4deg 13deg)`,
      WebkitMaskImage: "radial-gradient(circle at 74% 18%, #000 0%, transparent 60%)", maskImage: "radial-gradient(circle at 74% 18%, #000 0%, transparent 60%)" }} />
  </AbsoluteFill>
);

/** Far hills and the far end of the avenue: soft washes of blue-green and of pink blossom. */
const Distance: React.FC = () => (
  <svg width={1920} height={1080} style={{ position: "absolute", inset: 0 }}>
    <defs>
      <filter id="wash" x="-20%" y="-20%" width="140%" height="140%">
        <feTurbulence type="fractalNoise" baseFrequency="0.012" numOctaves={3} seed={4} result="n" />
        <feDisplacementMap in="SourceGraphic" in2="n" scale={40} xChannelSelector="R" yChannelSelector="G" result="d" />
        <feGaussianBlur in="d" stdDeviation={3} />
      </filter>
      <linearGradient id="ground" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0%" stopColor="#cfe3b8" /><stop offset="100%" stopColor="#9fc77f" />
      </linearGradient>
      {/* soft, ragged edges: the grass meets the path and the hills without a hard line */}
      <filter id="soft" x="-10%" y="-10%" width="120%" height="120%">
        <feTurbulence type="fractalNoise" baseFrequency="0.035" numOctaves={3} seed={7} result="n" />
        <feDisplacementMap in="SourceGraphic" in2="n" scale={22} xChannelSelector="R" yChannelSelector="G" result="d" />
        <feGaussianBlur in="d" stdDeviation={5} />
      </filter>
      <linearGradient id="path" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0%" stopColor="#f6e9e4" /><stop offset="100%" stopColor="#e8cfc6" />
      </linearGradient>
    </defs>
    <g filter="url(#wash)">
      <path d="M-50 640 Q 250 470 520 560 T 1050 520 T 1500 560 T 1980 500 L1980 760 L-50 760 Z" fill="#a9c6d6" opacity={0.55} />
      <path d="M-50 690 Q 300 560 640 640 T 1300 610 T 1980 640 L1980 800 L-50 800 Z" fill="#9ebfa5" opacity={0.5} />
      {/* blossom haze at the far end */}
      {Array.from({ length: 14 }, (_, i) => (
        <circle key={i} cx={960 + (i - 6.5) * 70 + rand(i, 3) * 40} cy={600 - rand(i, 4) * 60} r={70 + rand(i, 5) * 60} fill={PINKS[i % PINKS.length]} opacity={0.55} />
      ))}
    </g>
    <path d="M-80 650 L2000 650 L2000 1120 L-80 1120 Z" fill="url(#ground)" filter="url(#soft)" />
    {/* the path down the avenue, its edges worn into the grass */}
    <path d="M930 646 L990 646 L1520 1120 L400 1120 Z" fill="url(#path)" filter="url(#soft)" />
    <path d="M945 652 L975 652 L1330 1120 L590 1120 Z" fill="#f3e2dc" opacity={0.5} filter="url(#soft)" />
    {/* petals lying on the grass and the path */}
    {Array.from({ length: 160 }, (_, i) => {
      const y = 655 + Math.pow(rand(i, 7), 1.7) * 430;
      const k = (y - 650) / 430;
      const x = 960 + (rand(i, 8) - 0.5) * (200 + 2200 * k);
      return <ellipse key={i} cx={x} cy={y} rx={2 + 6 * k} ry={1 + 3 * k} fill={PINKS[i % PINKS.length]} opacity={0.9} />;
    })}
  </svg>
);

/** Bark and blossom colours of the cherry trees. */
const BARK = ["#2f2320", "#4a3830", "#5e4a40"];
const BLOOM = { shade: "#e2a2b5", mid: "#f6c8d5", light: "#fde6ed", white: "#fffafb", deep: "#e08aa6" };

/**
 * A cherry tree in full bloom, drawn to look real: a short dark trunk with the bands of cherry bark, limbs that split
 * low and spread wide like an umbrella, and a canopy of blossom clusters (shadow, body, sunlit top, single flowers
 * on the outside) with ragged edges. (x, y): the foot of the trunk; sway (degrees): the wind; detail: fewer flowers
 * far away.
 */
export const SakuraTree: React.FC<{ x: number; y: number; size?: number; lean?: number; seed?: number; sway?: number; detail?: number }> =
  ({ x, y, size = 1, lean = 0, seed = 3, sway = 0, detail = 1 }) => {
    const id = useId().replace(/:/g, "");
    const geo = useMemo(() => {
      let k = 0;
      const r = () => rand(k++, seed);
      const limbs: { d: string; w: number }[] = [];
      const tips: { x: number; y: number; r: number }[] = [];
      const bands: string[] = [];
      const limb = (bx: number, by: number, mx: number, my: number, ex: number, ey: number, w0: number, w1: number) => {
        const dx = ex - bx, dy = ey - by, l = Math.hypot(dx, dy) || 1;
        const nx = -dy / l, ny = dx / l, wm = (w0 + w1) / 2;
        limbs.push({ w: w0, d: `M${bx + nx * w0 / 2} ${by + ny * w0 / 2} Q${mx + nx * wm / 2} ${my + ny * wm / 2} ${ex + nx * w1 / 2} ${ey + ny * w1 / 2}` +
          ` L${ex - nx * w1 / 2} ${ey - ny * w1 / 2} Q${mx - nx * wm / 2} ${my - ny * wm / 2} ${bx - nx * w0 / 2} ${by - ny * w0 / 2} Z` });
      };
      const grow = (bx: number, by: number, a: number, len: number, w: number, level: number) => {
        // the limbs lean out more the higher they are (an umbrella crown), never down
        a = Math.max(-1.25, Math.min(1.25, a));
        const ex = bx + Math.sin(a) * len, ey = by - Math.cos(a) * len;
        const bend = (r() - 0.5) * len * 0.35;
        const mx = (bx + ex) / 2 + Math.cos(a) * bend, my = (by + ey) / 2 + Math.sin(a) * bend - len * 0.06;
        limb(bx, by, mx, my, ex, ey, w, w * 0.68);
        if (level === 5)
          for (let i = 0; i < 3; i++) {
            const t = 0.2 + r() * 0.6, px = bx + (ex - bx) * t, py = by + (ey - by) * t;
            bands.push(`M${px - w * 0.35} ${py} l${w * 0.7} ${(r() - 0.5) * 3}`);
          }
        if (level === 0) {
          tips.push({ x: ex, y: ey, r: 1 });
          for (let i = 0; i < 2; i++) {
            const ta = a + (i ? 0.8 : -0.8) + (r() - 0.5) * 0.4, tl = len * 0.5;
            limb(ex, ey, ex + Math.sin(ta) * tl * 0.5, ey - Math.cos(ta) * tl * 0.5, ex + Math.sin(ta) * tl, ey - Math.cos(ta) * tl, w * 0.5, w * 0.15);
            tips.push({ x: ex + Math.sin(ta) * tl, y: ey - Math.cos(ta) * tl, r: 0.8 });
          }
          return;
        }
        if (level === 1 && r() > 0.4) tips.push({ x: (bx + ex) / 2, y: (by + ey) / 2 - len * 0.2, r: 0.65 });
        const n = level >= 4 ? 2 + (r() > 0.5 ? 1 : 0) : 2;
        for (let i = 0; i < n; i++) {
          const spread = (i - (n - 1) / 2) * (0.75 + r() * 0.35);
          const out = a + spread + (a > 0 ? 0.12 : a < 0 ? -0.12 : 0);
          grow(ex, ey, out + (r() - 0.5) * 0.3, len * (0.7 + r() * 0.12), w * 0.64, level - 1);
        }
      };
      // a short trunk, split low
      grow(0, 0, lean * 0.5, 250 * size, 40 * size, 5);
      // the clusters of blossom: a ring of puffs around each tip, single flowers on the outside
      const puffs = tips.flatMap((t, i) => {
        const R = 46 * size * t.r * (0.75 + rand(i, seed + 3) * 0.5);
        return [0, 1, 2].map((j) => ({ x: t.x + (rand(i * 3 + j, seed + 4) - 0.5) * R * 0.9, y: t.y - R * 0.25 + (rand(i * 3 + j, seed + 5) - 0.5) * R * 0.7, r: R * (0.65 + rand(i * 3 + j, seed + 6) * 0.35) }));
      });
      const dots: { x: number; y: number; r: number; c: string }[] = [];
      puffs.forEach((p, i) => {
        const n = Math.round(10 * detail);
        for (let j = 0; j < n; j++) {
          const a = rand(i * 31 + j, seed + 7) * Math.PI * 2, d = Math.sqrt(rand(i * 31 + j, seed + 8)) * p.r;
          const top = Math.sin(a) < 0;
          dots.push({ x: p.x + Math.cos(a) * d, y: p.y + Math.sin(a) * d * 0.85, r: (2.2 + rand(i * 31 + j, seed + 9) * 3.2) * Math.max(0.6, size),
            c: rand(i * 31 + j, seed + 10) > 0.94 ? BLOOM.deep : top ? BLOOM.white : rand(i * 31 + j, seed + 11) > 0.5 ? BLOOM.light : BLOOM.mid });
        }
      });
      return { limbs, bands, puffs, dots };
    }, [seed, size, lean, detail]);
    return (
      <svg style={{ position: "absolute", left: x, top: y, overflow: "visible", transform: `rotate(${sway}deg)`, transformOrigin: "0 0" }} width={1} height={1}>
        <defs>
          <linearGradient id={`b${id}`} x1="0" y1="0" x2="1" y2="0">
            <stop offset="0%" stopColor={BARK[0]} /><stop offset="45%" stopColor={BARK[2]} /><stop offset="100%" stopColor={BARK[1]} />
          </linearGradient>
          <filter id={`c${id}`} x="-30%" y="-30%" width="160%" height="160%">
            <feTurbulence type="fractalNoise" baseFrequency={0.05 / Math.max(0.3, size)} numOctaves={3} seed={seed} result="n" />
            <feDisplacementMap in="SourceGraphic" in2="n" scale={28 * size} xChannelSelector="R" yChannelSelector="G" result="d" />
            <feGaussianBlur in="d" stdDeviation={1.4 * size} />
          </filter>
          <filter id={`k${id}`} x="-20%" y="-20%" width="140%" height="140%">
            <feTurbulence type="fractalNoise" baseFrequency="0.12 0.03" numOctaves={2} seed={seed} result="n" />
            <feDisplacementMap in="SourceGraphic" in2="n" scale={3} xChannelSelector="R" yChannelSelector="G" />
          </filter>
        </defs>
        {/* its shadow on the grass */}
        <ellipse cx={0} cy={6 * size} rx={170 * size} ry={26 * size} fill="#5e8a48" opacity={0.28} style={{ filter: `blur(${8 * size}px)` }} />
        <g filter={`url(#k${id})`} fill={`url(#b${id})`}>{geo.limbs.map((l, i) => <path key={i} d={l.d} />)}</g>
        <g stroke="#8a7468" strokeWidth={1.2 * size} opacity={0.55}>{geo.bands.map((d, i) => <path key={i} d={d} />)}</g>
        {/* the canopy: shadow, body, the sunlit top */}
        <g filter={`url(#c${id})`}>
          {geo.puffs.map((p, i) => <circle key={`s${i}`} cx={p.x + p.r * 0.12} cy={p.y + p.r * 0.18} r={p.r} fill={BLOOM.shade} opacity={0.9} />)}
          {geo.puffs.map((p, i) => <circle key={`m${i}`} cx={p.x} cy={p.y} r={p.r * 0.86} fill={BLOOM.mid} />)}
          {geo.puffs.map((p, i) => <circle key={`l${i}`} cx={p.x - p.r * 0.2} cy={p.y - p.r * 0.28} r={p.r * 0.55} fill={BLOOM.light} />)}
        </g>
        {/* single flowers: the texture of the blossom */}
        <g>{geo.dots.map((d, i) => <circle key={i} cx={d.x} cy={d.y} r={d.r} fill={d.c} opacity={0.92} />)}</g>
      </svg>
    );
  };

/** The trees of the avenue: [side (-1 left, 1 right), depth z (1 near ... far)]. */
const TREES: [number, number][] = [];
for (const z of [6.5, 4.4, 3, 2.05, 1.4, 1]) for (const side of [-1, 1]) TREES.push([side, z]);

/** The layers of the avenue drawn once as pictures (scripts/render-layers.mjs -> public/avenue/<layer>.png): far rows,
 *  middle rows, the near tree on each side. Drawing the trees on every frame took seconds a frame. */
export const LAYERS = ["far", "mid", "nearL", "nearR"] as const;
export type Layer = (typeof LAYERS)[number];
const layerOf = (side: number, z: number): Layer => (z >= 3 ? "far" : z > 1 ? "mid" : side < 0 ? "nearL" : "nearR");
/** Margin around the screen in the layer pictures (the sway and the dolly never show their edges). */
export const LAYER_PAD = { x: 192, y: 108 };
const VX = 960, VY = 640;

/** The trees of one layer, still (no sway), on a transparent background: rendered once to a picture. */
export const AvenueLayer: React.FC<{ layer: Layer }> = ({ layer }) => (
  <AbsoluteFill style={{ backgroundColor: "transparent" }}>
    <div style={{ position: "absolute", left: LAYER_PAD.x, top: LAYER_PAD.y, width: 1920, height: 1080 }}>
      {TREES.map(([side, z], i) => {
        if (layerOf(side, z) !== layer) return null;
        const x = VX + side * (900 / z) + (rand(i, 12) - 0.5) * 60 / z;
        const y = VY + 470 / z;
        const size = 1.45 / z;
        // far trees paler (aerial perspective)
        const haze = Math.min(0.7, (z - 1) * 0.13);
        return (
          <div key={i} style={{ position: "absolute", inset: 0, filter: haze > 0.05 ? `saturate(${1 - haze * 0.6}) brightness(${1 + haze * 0.45}) blur(${haze * 1.6}px)` : undefined }}>
            <SakuraTree x={x} y={y} size={size} lean={-side * (0.22 + rand(i, 13) * 0.12)} seed={3 + i * 7} sway={0} detail={z > 3 ? 0.5 : 1} />
          </div>
        );
      })}
    </div>
  </AbsoluteFill>
);

/**
 * The avenue: two rows of cherry trees in bloom going away to the vanishing point, the path between them; the camera
 * moves slowly down it (dolly 0 -> 1 over the video), the trees sway in the wind (each layer bending at its foot).
 */
export const Avenue: React.FC<{ frame: number; dolly: number }> = ({ frame, dolly }) => {
  const scale = 1 + 0.2 * dolly;
  const layer = (name: Layer, i: number, origin: string, amp: number) => {
    const sway = Math.sin(frame / 38 + i * 1.7) * amp + Math.sin(frame / 17 + i) * amp * 0.3;
    return (
      <Img key={name} src={staticFile(`avenue/${name}.png`)} style={{ position: "absolute", left: -LAYER_PAD.x, top: -LAYER_PAD.y,
        width: 1920 + LAYER_PAD.x * 2, height: 1080 + LAYER_PAD.y * 2, transformOrigin: origin, transform: `rotate(${sway}deg)` }} />
    );
  };
  return (
    <AbsoluteFill style={{ overflow: "hidden" }}>
      <Sky frame={frame} />
      <AbsoluteFill style={{ transform: `scale(${1 + 0.06 * dolly})`, transformOrigin: `${VX}px ${VY}px` }}><Distance /></AbsoluteFill>
      <AbsoluteFill style={{ transform: `scale(${scale})`, transformOrigin: `${VX}px ${VY}px` }}>
        {layer("far", 0, `${VX}px ${VY + 120}px`, 0.25)}
        {layer("mid", 1, `${VX}px ${VY + 300}px`, 0.45)}
        {layer("nearL", 2, `${VX - 900}px ${VY + 470}px`, 0.9)}
        {layer("nearR", 3, `${VX + 900}px ${VY + 470}px`, 0.9)}
      </AbsoluteFill>
      {/* the light of spring over it */}
      <AbsoluteFill style={{ background: "radial-gradient(ellipse at 50% 55%, rgba(255,250,247,0) 45%, rgba(255,240,245,0.35) 100%)" }} />
    </AbsoluteFill>
  );
};

/** A petal at (x, y), size s, turned `rot` degrees, fluttering (flip 1 -> -1: the petal seen face-on to edge-on). */
const Petal: React.FC<{ x: number; y: number; s: number; rot: number; flip: number; color: string; opacity?: number; blur?: number }> =
  ({ x, y, s, rot, flip, color, opacity = 1, blur = 0 }) => (
    <svg width={s} height={s} viewBox="-2 -12 28 24" style={{ position: "absolute", left: x, top: y, overflow: "visible", opacity,
      transform: `rotate(${rot}deg) scaleY(${0.25 + 0.75 * Math.abs(flip)})`, filter: blur > 0 ? `blur(${blur}px)` : undefined }}>
      <path d={PETAL} fill={color} stroke="#e48aa2" strokeWidth={0.7} />
    </svg>
  );

/** Petals falling all the time, drifting left on a light wind; some near the lens (big, blurred). pulse 0..1 pushes them. */
export const PetalField: React.FC<{ frame: number; count?: number; opacity?: number }> = ({ frame, count = 64, opacity = 1 }) => (
  <AbsoluteFill style={{ pointerEvents: "none", opacity }}>
    {Array.from({ length: count }, (_, i) => {
      const near = rand(i, 40) > 0.86;
      const vy = (near ? 2.6 : 1.1) + rand(i, 41) * 1.4;
      const vx = -(0.5 + rand(i, 42) * 1.3) * (near ? 1.8 : 1);
      const y = ((rand(i, 43) * 1300 + frame * vy) % 1300) - 110;
      const x = ((((rand(i, 44) * 2300 + frame * vx + Math.sin(frame / 28 + i) * 40) % 2300) + 2300) % 2300) - 190;
      const s = near ? 46 + rand(i, 45) * 30 : 12 + rand(i, 46) * 16;
      return <Petal key={i} x={x} y={y} s={s} rot={frame * (1.5 + rand(i, 47) * 3) + i * 47} flip={Math.cos(frame / (9 + rand(i, 48) * 9) + i)}
        color={PINKS[i % PINKS.length]} opacity={near ? 0.7 : 0.92} blur={near ? 3 : 0} />;
    })}
  </AbsoluteFill>
);

/** A gust of petals blowing across the screen from right to left (p 0 -> 1), thick in the middle of it. */
export const Gust: React.FC<{ p: number; count?: number; seed?: number; dir?: 1 | -1 }> = ({ p, count = 130, seed = 1, dir = -1 }) => {
  if (p <= 0 || p >= 1) return null;
  return (
    <AbsoluteFill style={{ pointerEvents: "none" }}>
      {Array.from({ length: count }, (_, i) => {
        const delay = rand(i, seed + 50) * 0.45;
        const q = span(p, delay, delay + 0.55);
        if (q <= 0 || q >= 1) return null;
        const startX = dir < 0 ? 2000 + rand(i, seed + 51) * 300 : -300 - rand(i, seed + 51) * 300;
        const x = startX + dir * q * (2500 + rand(i, seed + 52) * 600);
        const y = rand(i, seed + 53) * 1180 - 80 + Math.sin(q * 6 + i) * 50 + q * (rand(i, seed + 54) - 0.3) * 300;
        const near = rand(i, seed + 55) > 0.85;
        const s = near ? 50 + rand(i, seed + 56) * 30 : 16 + rand(i, seed + 57) * 22;
        return <Petal key={i} x={x} y={y} s={s} rot={q * 900 * (rand(i, seed + 58) - 0.5) + (dir < 0 ? 180 : 0)} flip={Math.cos(q * 14 + i)}
          color={PINKS[i % PINKS.length]} opacity={Math.sin(q * Math.PI) * (near ? 0.75 : 1)} blur={near ? 2.5 : 0} />;
      })}
    </AbsoluteFill>
  );
};

/** Petals bursting out from (x, y) (p 0 -> 1): on a big drum hit. */
export const Burst: React.FC<{ p: number; x?: number; y?: number; count?: number; seed?: number }> = ({ p, x = 960, y = 470, count = 60, seed = 9 }) => {
  if (p <= 0 || p >= 1) return null;
  const e = 1 - Math.pow(1 - p, 3);
  return (
    <AbsoluteFill style={{ pointerEvents: "none" }}>
      {Array.from({ length: count }, (_, i) => {
        const a = rand(i, seed) * Math.PI * 2;
        const d = (300 + rand(i, seed + 1) * 900) * e;
        const s = 14 + rand(i, seed + 2) * 26;
        return <Petal key={i} x={x + Math.cos(a) * d} y={y + Math.sin(a) * d * 0.7 + p * p * 160} s={s} rot={(a * 180) / Math.PI + p * 300}
          flip={Math.cos(p * 10 + i)} color={PINKS[i % PINKS.length]} opacity={(1 - p) * 0.95} />;
      })}
    </AbsoluteFill>
  );
};

/** A falling curtain of petals along a line at height y (for the curtain transition). */
export const PetalEdge: React.FC<{ y: number; t: number; seed?: number }> = ({ y, t, seed = 5 }) => (
  <AbsoluteFill style={{ pointerEvents: "none" }}>
    {Array.from({ length: 70 }, (_, i) => {
      const x = (i / 70) * 2000 - 40 + rand(i, seed) * 30;
      const yy = y + Math.sin(i * 0.9 + t / 5) * 34 + (rand(i, seed + 1) - 0.5) * 60;
      return <Petal key={i} x={x} y={yy} s={18 + rand(i, seed + 2) * 22} rot={t * 6 + i * 50} flip={Math.cos(t / 6 + i)} color={PINKS[i % PINKS.length]} />;
    })}
  </AbsoluteFill>
);

/** A red seal stamped (vertical text for several characters), stamped at `p` 0 -> 1. */
export const Seal: React.FC<{ text: string; size: number; p?: number; style?: React.CSSProperties }> = ({ text, size, p = 1, style }) => {
  const chars = [...text];
  const vertical = chars.length > 1;
  const s = 1.6 - 0.6 * Math.min(1, p);
  return (
    <div style={{ width: vertical ? size * 0.62 : size, height: vertical ? size * 0.62 * chars.length + size * 0.12 : size, background: "#c2362f",
      borderRadius: size * 0.08, display: "flex", flexDirection: "column", justifyContent: "center", alignItems: "center",
      boxShadow: "inset 0 0 0 3px #f1d2c4, inset 0 0 0 5px #c2362f, 0 3px 10px rgba(120,30,30,0.35)", color: "#fff6ef",
      fontFamily: "'Ma Shan Zheng', 'KaiTi', serif", fontSize: vertical ? size * 0.5 : size * 0.72, lineHeight: 1.05,
      opacity: Math.min(1, p * 3), transform: `scale(${s}) rotate(${-6 + 6 * Math.min(1, p)}deg)`, ...style }}>
      {chars.map((c, i) => <span key={i}>{c}</span>)}
    </div>
  );
};
