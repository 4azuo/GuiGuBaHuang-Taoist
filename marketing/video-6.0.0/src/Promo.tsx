import { AbsoluteFill, Audio, Easing, Img, interpolate, Sequence, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { ACCENTS, BEAT, CHAPTER, CREDIT, ENDLINE, Part, Plan, SLIDE, Slide, TAGLINE, frameOf, romanOf, volumeOf } from "./data";
import { BRUSH, DISPLAY, SERIF } from "./fonts";
import { Avenue, Branch, Burst, CREAM, DEEP, Flower, Gust, PetalEdge, PetalField, PLUM, ROSE, Seal, flowerPath, rand, span, svgMask } from "./Sakura";

export type PromoProps = { plan: Plan; music: string };

/**
 * The 6.0 video, cherry blossom theme (the 5.5.45 video: ink, gold and mist): all of it in an avenue of cherry trees in
 * bloom, the camera moving slowly down it; the pictures, chapter cards and titles come and go over it in sakura ways
 * (a gust of petals, a flower opening, a folding fan...), every one on a drum hit of the song (it plays whole).
 */

const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;
const outCubic = { easing: Easing.out(Easing.cubic), ...clamp };
const inOutCubic = Easing.inOut(Easing.cubic);
/** A soft white glow behind the text (readable over the trees). */
const HALO = "0 0 22px rgba(255,250,247,0.95), 0 0 6px rgba(255,250,247,0.95)";

/** Sakura text: deep rose to pink, with a white glow. */
const sakuraText = (glow = 24): React.CSSProperties => ({
  background: `linear-gradient(180deg, #f39ab6 0%, ${ROSE} 45%, ${DEEP} 100%)`,
  WebkitBackgroundClip: "text", backgroundClip: "text", color: "transparent",
  filter: `drop-shadow(0 0 ${glow * 0.5}px rgba(255,250,247,0.95)) drop-shadow(0 3px 4px rgba(120,40,70,0.3))`,
});

/** A character written with the brush: revealed from the top like a stroke, from blur to sharp. */
const BrushGlyph: React.FC<{ char: string; size: number; p: number; color?: string; opacity?: number }> = ({ char, size, p, color = DEEP, opacity = 1 }) => (
  <div style={{ fontFamily: BRUSH, fontSize: size, lineHeight: 1, color, opacity,
    WebkitMaskImage: `linear-gradient(180deg, #000 ${p * 120 - 20}%, transparent ${p * 120}%)`, maskImage: `linear-gradient(180deg, #000 ${p * 120 - 20}%, transparent ${p * 120}%)`,
    filter: `blur(${(1 - p) * 6}px) drop-shadow(0 0 18px rgba(255,250,247,0.9))` }}>{char}</div>
);

/** A soft cream light behind text (a glow, not a box). */
const Glow: React.FC<{ x: number; y: number; w: number; h: number; o?: number }> = ({ x, y, w, h, o = 1 }) => (
  <div style={{ position: "absolute", left: x - w / 2, top: y - h / 2, width: w, height: h, opacity: o,
    background: "radial-gradient(ellipse at 50% 50%, rgba(255,250,247,0.92) 0%, rgba(255,250,247,0.7) 45%, rgba(255,250,247,0) 72%)" }} />
);

// ---------------------------------------------------------------------------------------------------------------- intro

/** Title: the game's name in the blossoms, then TAOIST gathering out of the petals with its seal, then the tagline. */
const Intro: React.FC<{ beats: number }> = ({ beats }) => {
  const frame = useCurrentFrame();
  const B = BEAT;
  const game = Math.min(span(frame, B * 1, B * 3), 1 - span(frame, B * 5.5, B * 6.5));
  const taoist = span(frame, B * 6.5, B * 9);
  const seal = span(frame, B * 9, B * 9.6);
  const tag = span(frame, B * 10, B * 11.5);
  // the title fades as the first chapter comes in
  const out = 1 - span(frame, beats * B, beats * B + B);
  return (
    <AbsoluteFill style={{ opacity: out }}>
      <Glow x={960} y={500} w={1700} h={760} o={Math.max(game, taoist)} />
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", opacity: game }}>
        <div style={{ textAlign: "center", transform: `scale(${0.96 + frame / 900})` }}>
          <div style={{ fontFamily: BRUSH, fontSize: 112, color: PLUM, letterSpacing: 26, textShadow: HALO }}>鬼谷八荒</div>
          <div style={{ fontFamily: DISPLAY, fontSize: 42, letterSpacing: 22, color: ROSE, marginTop: 8, textShadow: HALO }}>TALE OF IMMORTAL</div>
        </div>
      </AbsoluteFill>
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center" }}>
        <div style={{ display: "flex", alignItems: "center", gap: 40, marginTop: -60 }}>
          <div style={{ display: "flex", fontFamily: DISPLAY, fontWeight: 900, fontSize: 230, letterSpacing: 20 }}>
            {[..."TAOIST"].map((c, i) => {
              // each letter gathers from its own place, one after another
              const q = outCubic.easing(span(taoist, i * 0.08, 0.6 + i * 0.08));
              return <span key={i} style={{ ...sakuraText(46), display: "inline-block", opacity: q,
                transform: `translate(${(1 - q) * (rand(i, 3) - 0.5) * 900}px, ${(1 - q) * (rand(i, 4) - 0.7) * 500}px) rotate(${(1 - q) * (rand(i, 5) - 0.5) * 120}deg) scale(${1.6 - 0.6 * q})` }}>{c}</span>;
            })}
          </div>
          <Seal text="道" size={128} p={seal} style={{ visibility: seal > 0 ? "visible" : "hidden" }} />
        </div>
        <div style={{ position: "absolute", top: "66%", fontFamily: SERIF, fontStyle: "italic", fontSize: 56, color: PLUM, opacity: tag,
          transform: `translateY(${(1 - tag) * 30}px)`, textShadow: HALO }}>
          {TAGLINE[0]} <span style={{ color: ROSE, fontWeight: 700 }}>{TAGLINE[1]}</span>
        </div>
      </AbsoluteFill>
      {/* petals flying in to make TAOIST */}
      <Gust p={span(frame, B * 5.6, B * 9.2)} count={110} seed={31} dir={-1} />
      {/* fade from white */}
      <AbsoluteFill style={{ backgroundColor: CREAM, opacity: 1 - span(frame, 0, B * 1.2) }} />
    </AbsoluteFill>
  );
};

// -------------------------------------------------------------------------------------------------------- chapter card

/** A chapter: a pale moon with the brush character, a flowering branch growing over it, the volume seal, the title. */
const ChapterCard: React.FC<{ part: Part }> = ({ part }) => {
  const frame = useCurrentFrame();
  const B = BEAT;
  const moon = outCubic.easing(span(frame, 0, B * 0.9));
  const glyph = span(frame, B * 0.2, B * 1.2);
  const branch = span(frame, B * 0.1, B * 1.6);
  const vol = span(frame, B * 0.5, B * 1.0);
  const title = outCubic.easing(span(frame, B * 0.6, B * 1.4));
  const sub = span(frame, B * 1.4, B * 2.1);
  const seal = span(frame, B * 1.2, B * 1.6);
  const drift = frame * 0.25;
  return (
    <AbsoluteFill>
      <Glow x={1150} y={520} w={1500} h={760} o={title} />
      {/* the moon */}
      <div style={{ position: "absolute", left: 300 - drift * 0.3, top: 190, width: 560, height: 560, borderRadius: "50%",
        transform: `scale(${0.6 + 0.4 * moon})`, opacity: moon,
        background: "radial-gradient(circle at 45% 40%, #fffdfb 0%, #fff4f6 55%, #fbdbe4 88%, #f6c3d2 100%)",
        boxShadow: "0 0 0 6px rgba(255,250,247,0.7), 0 0 80px 20px rgba(255,236,242,0.9)", display: "flex", justifyContent: "center", alignItems: "center" }}>
        <BrushGlyph char={part.glyph} size={380} p={glyph} />
        <Seal text={volumeOf(part.number)} size={74} p={seal} style={{ position: "absolute", right: 6, bottom: 20, visibility: seal > 0 ? "visible" : "hidden" }} />
      </div>
      {/* the branch over the moon */}
      <div style={{ position: "absolute", left: 120, top: 70, transform: `rotate(8deg) translateX(${-drift * 0.2}px)` }}>
        <Branch grow={branch} seed={part.number * 5} width={900} />
      </div>
      <div style={{ position: "absolute", left: 960, top: 300, width: 900 }}>
        <div style={{ fontFamily: DISPLAY, fontSize: 34, letterSpacing: 16, color: ROSE, opacity: vol, textShadow: HALO }}>CHAPTER {romanOf(part.number)}</div>
        <div style={{ fontFamily: DISPLAY, fontWeight: 700, fontSize: part.title.length > 18 ? 84 : 104, lineHeight: 1.05, color: PLUM, marginTop: 14,
          opacity: title, transform: `translateX(${(1 - title) * 60 + drift * 0.15}px)`, textShadow: HALO, letterSpacing: `${0.01 + (1 - title) * 0.2}em` }}>
          {part.title}
        </div>
        <div style={{ width: 520 * title, height: 3, margin: "26px 0 22px", background: `linear-gradient(90deg, ${ROSE}, rgba(217,87,127,0))` }} />
        <div style={{ fontFamily: SERIF, fontStyle: "italic", fontSize: 40, color: DEEP, opacity: sub, textShadow: HALO, transform: `translateY(${(1 - sub) * 16}px)` }}>
          {part.subtitle}
        </div>
      </div>
    </AbsoluteFill>
  );
};

// ------------------------------------------------------------------------------------------------------------- pictures

/** Frame of the pictures: the card fills most of the screen, the caption goes below it. */
const CARD = { left: 270, top: 52, width: 1380, height: 776 };
/** Frames of the next picture of a scene coming in. */
const CARD_MOVE = Math.round(BEAT * 1.1);

/** A picture on white paper with rounded corners, a sprig of blossom on its corner. */
const Card: React.FC<{ file: string; style?: React.CSSProperties; zoom?: number; sprig?: number }> = ({ file, style, zoom = 1, sprig = 1 }) => (
  <div style={{ position: "absolute", ...CARD, backfaceVisibility: "hidden", ...style }}>
    <div style={{ position: "absolute", inset: 0, borderRadius: 16, overflow: "hidden", background: "#000",
      boxShadow: "0 0 0 8px #fffaf7, 0 0 0 9px rgba(217,87,127,0.35), 0 22px 50px rgba(110,40,70,0.35)" }}>
      <Img src={staticFile(`steam/${file}`)} style={{ width: "100%", height: "100%", objectFit: "contain", transform: `scale(${zoom})` }} />
    </div>
    {sprig > 0 && (
      <div style={{ position: "absolute", right: -120, top: -40, transform: "scaleX(-1) rotate(-18deg)", opacity: sprig, pointerEvents: "none" }}>
        <Branch grow={sprig} seed={7} width={420} />
      </div>
    )}
  </div>
);

/** How the next picture of a scene of several comes in (q 0 -> 1): the scenes take them in turn. */
type Swap = "slide" | "fold" | "drop";
const SWAPS: Swap[] = ["fold", "slide", "drop"];
const swapStyles = (swap: Swap, q: number): [React.CSSProperties, React.CSSProperties] => {
  const e = inOutCubic(q);
  switch (swap) {
    case "fold": // the old one turns away like a fan leaf, the new one turns in
      return [
        { transformOrigin: "0% 50%", transform: `perspective(2400px) rotateY(${-95 * Math.min(1, e * 2)}deg)`, opacity: e < 0.5 ? 1 : 0, zIndex: 2 },
        { transformOrigin: "100% 50%", transform: `perspective(2400px) rotateY(${95 * (1 - Math.max(0, e * 2 - 1))}deg)`, opacity: e < 0.5 ? 0 : 1, zIndex: 3 },
      ];
    case "drop": // the new one falls like a petal, swinging, onto the old one
      return [
        { transform: `translateY(${e * 90}px) scale(${1 - 0.06 * e})`, filter: `brightness(${1 - 0.3 * e})`, opacity: 1 - span(q, 0.7, 1), zIndex: 2 },
        { transform: `translate(${Math.sin(e * Math.PI * 1.5) * 120 * (1 - e)}px, ${(e - 1) * 1000}px) rotate(${Math.sin(e * Math.PI * 2) * 10 * (1 - e)}deg)`, zIndex: 3 },
      ];
    default: // passed from hand to hand: the old one to the left, the new one from the right
      return [
        { transform: `translateX(${-1900 * e}px) rotate(${-8 * e}deg)`, zIndex: 3 },
        { transform: `translateX(${1900 * (1 - e)}px) rotate(${8 * (1 - e)}deg)`, zIndex: 2 },
      ];
  }
};

/**
 * A scene: one picture, or the pictures of one feature (each SLIDE beats, the next one coming in its way). Below: the
 * chapter, "2 / 3", the caption.
 */
const FeatureScene: React.FC<{ slides: Slide[]; part: Part; swap: Swap; inFrames: number }> = ({ slides, part, swap, inFrames }) => {
  const frame = useCurrentFrame();
  const per = SLIDE * BEAT;
  const k = Math.min(slides.length - 1, Math.floor(frame / per));
  const local = frame - k * per;
  const moving = k > 0 && local < CARD_MOVE;
  const q = moving ? local / CARD_MOVE : 1;
  const [oldStyle, newStyle] = swapStyles(swap, q);
  const zoom = (i: number) => 1 + 0.035 * Math.min(1, Math.max(0, (frame - i * per) / (per + CARD_MOVE)));
  const caption = k === 0 ? span(frame, inFrames * 0.6, inFrames * 0.6 + BEAT) : Math.abs(1 - 2 * Math.min(1, local / (BEAT * 0.7)));
  const shown = k > 0 && local < BEAT * 0.35 ? k - 1 : k;
  const text = slides[shown].caption ?? "";
  const size = Math.min(44, Math.floor(1180 / Math.max(1, text.length * 0.56)));
  // a pulse of the card on each beat
  const beatAge = (frame % BEAT) / BEAT;
  const pulse = 1 + 0.004 * Math.exp(-beatAge * 6);
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{ transform: `scale(${pulse})` }}>
        {moving && <Card file={slides[k - 1].file} zoom={zoom(k - 1)} style={oldStyle} sprig={0} />}
        <Card file={slides[k].file} zoom={zoom(k)} style={moving ? newStyle : undefined} sprig={span(frame, inFrames * 0.5, inFrames * 0.5 + BEAT * 1.3)} />
      </AbsoluteFill>
      {/* the caption on a band of cream */}
      <div style={{ position: "absolute", left: CARD.left - 30, right: CARD.left - 30, top: CARD.top + CARD.height + 30, height: 150, borderRadius: 75,
        background: "linear-gradient(90deg, rgba(255,250,247,0) 0%, rgba(255,250,247,0.9) 8%, rgba(255,250,247,0.9) 92%, rgba(255,250,247,0) 100%)",
        display: "flex", alignItems: "center", gap: 26, paddingLeft: 70, opacity: k === 0 ? caption : 1 }}>
        <svg width={86} height={86} viewBox="-50 -50 100 100" style={{ flexShrink: 0, transform: `rotate(${frame * 0.6}deg)` }}>
          <Flower x={0} y={0} r={44} />
        </svg>
        <div style={{ minWidth: 0 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 2 }}>
            <span style={{ fontFamily: BRUSH, fontSize: 30, color: ROSE }}>{part.glyph}</span>
            <span style={{ fontFamily: DISPLAY, fontSize: 20, letterSpacing: 6, color: ROSE }}>{part.title.toUpperCase()}</span>
            {slides.length > 1 && <span style={{ fontFamily: DISPLAY, fontSize: 20, letterSpacing: 4, color: DEEP, marginLeft: 10, opacity: 0.7 }}>{shown + 1} / {slides.length}</span>}
          </div>
          <div style={{ fontFamily: SERIF, fontSize: size, fontWeight: 700, color: PLUM, whiteSpace: "nowrap", opacity: k === 0 ? 1 : caption,
            transform: `translateY(${(1 - caption) * 10}px)` }}>
            {text}
          </div>
        </div>
      </div>
    </AbsoluteFill>
  );
};

// ---------------------------------------------------------------------------------------------------------------- outro

/** End card on the last chord: the end line, TAOIST with its seal, where to get it, the music credit. */
const Outro: React.FC<{ beats: number }> = ({ beats }) => {
  const frame = useCurrentFrame();
  const B = BEAT;
  const a = outCubic.easing(span(frame, B * 0.3, B * 1.8));
  const b = span(frame, B * 2, B * 3.2);
  const fade = 1 - span(frame, beats * B - B * 1.2, beats * B);
  return (
    <AbsoluteFill style={{ opacity: fade }}>
      <Glow x={960} y={520} w={1800} h={900} o={a} />
      <div style={{ position: "absolute", left: 180, top: 40, transform: "rotate(4deg)" }}><Branch grow={span(frame, 0, B * 2)} seed={41} width={760} /></div>
      <div style={{ position: "absolute", right: 140, bottom: 60, transform: "rotate(186deg)" }}><Branch grow={span(frame, B * 0.5, B * 2.5)} seed={43} width={660} /></div>
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", textAlign: "center" }}>
        <div style={{ fontFamily: DISPLAY, fontSize: 48, fontWeight: 700, letterSpacing: 10, color: ROSE, opacity: a, textShadow: HALO }}>
          {ENDLINE}
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 36, margin: "6px 0", transform: `scale(${1.15 - 0.15 * a})`, opacity: a }}>
          <div style={{ fontFamily: DISPLAY, fontWeight: 900, fontSize: 200, letterSpacing: 22, ...sakuraText(46) }}>TAOIST</div>
          <Seal text="道" size={112} p={span(frame, B * 1.4, B * 1.9)} />
        </div>
        <div style={{ fontFamily: SERIF, fontSize: 52, color: PLUM, opacity: b, textShadow: HALO }}>Subscribe on the Steam Workshop</div>
        <div style={{ fontFamily: SERIF, fontStyle: "italic", fontSize: 32, marginTop: 12, opacity: b, color: DEEP, textShadow: HALO }}>Requires the ModLib framework mod</div>
      </AbsoluteFill>
      <div style={{ position: "absolute", bottom: 30, width: "100%", textAlign: "center", fontFamily: SERIF, fontSize: 24, color: PLUM, opacity: b * 0.85, textShadow: HALO }}>
        {CREDIT}
      </div>
    </AbsoluteFill>
  );
};

// ---------------------------------------------------------------------------------------------------------- transitions

/**
 * The ways a scene comes in over the previous one (the previous one leaves in the matching way), all of them with
 * blossoms: "gust" (blown in by a gust of petals), "bloom" (through a sakura flower opening from the middle), "fan"
 * (unfolding like a folding fan), "blossoms" (through flowers opening all over), "branch" (behind a flowering branch
 * sweeping across), "moon" (through a round moon gate), "curtain" (under a curtain of falling petals), "page" (turned
 * in like the page of an album). The scenes take them in turn.
 */
type Kind = "gust" | "bloom" | "fan" | "blossoms" | "branch" | "moon" | "curtain" | "page";
const KINDS: Kind[] = ["gust", "bloom", "fan", "blossoms", "branch", "moon", "curtain", "page"];
/** Frames of a transition. */
const TRANS = Math.round(BEAT * 1.6);

/** Many flowers opening at random places (for "blossoms"), p 0 -> 1: the screen covered at 1. */
const blossomsMask = (p: number) => {
  let shapes = "";
  for (let i = 0; i < 46; i++) {
    const x = (i % 9) * 230 + rand(i, 61) * 160 - 20, y = Math.floor(i / 9) * 240 + rand(i, 62) * 160 - 30;
    const d = rand(i, 63) * 0.45;
    const q = span(p, d, d + 0.55);
    if (q <= 0) continue;
    const r = 420 * Easing.out(Easing.quad)(q);
    shapes += `<path d="${flowerPath(x, y, r, rand(i, 64) * 72 + q * 40)}" fill="white"/><circle cx="${x}" cy="${y}" r="${r * 0.55}" fill="white"/>`;
  }
  return svgMask(shapes || `<rect width="1" height="1" fill="black"/>`);
};

/** A scene coming in (its first TRANS frames). */
const Enter: React.FC<{ kind: Kind; children: React.ReactNode }> = ({ kind, children }) => {
  const frame = useCurrentFrame();
  const p = Math.min(1, frame / TRANS);
  if (p >= 1) return <AbsoluteFill>{children}</AbsoluteFill>;
  const e = inOutCubic(p);
  const o = outCubic.easing(p);
  switch (kind) {
    case "gust":
      return (
        <>
          <AbsoluteFill style={{ transform: `translateX(${(1 - o) * 1100}px) rotate(${(1 - o) * 7}deg)`, opacity: span(p, 0.1, 0.5), filter: `blur(${(1 - o) * 8}px)` }}>{children}</AbsoluteFill>
          <Gust p={p} count={150} seed={3} />
        </>
      );
    case "bloom": {
      const r = 2600 * Math.pow(e, 1.6);
      return (
        <>
          <AbsoluteFill style={svgMask(`<path d="${flowerPath(960, 480, r, e * 110)}" fill="white"/><circle cx="960" cy="480" r="${r * 0.45}" fill="white"/>`)}>{children}</AbsoluteFill>
          <svg width={1920} height={1080} style={{ position: "absolute", inset: 0, opacity: 1 - span(p, 0.15, 0.45), pointerEvents: "none" }}>
            <path d={flowerPath(960, 480, r, e * 110)} fill="none" stroke="#fbd3de" strokeWidth={10} />
          </svg>
        </>
      );
    }
    case "fan": {
      const a = 180 * e;
      const mask = `conic-gradient(from -90deg at 50% 100%, #000 0deg, #000 ${a}deg, transparent ${a + 0.6}deg)`;
      return (
        <AbsoluteFill style={{ WebkitMaskImage: mask, maskImage: mask }}>
          {children}
          {/* the ribs of the fan */}
          <AbsoluteFill style={{ opacity: 1 - span(p, 0.65, 1),
            background: "repeating-conic-gradient(from -90deg at 50% 100%, rgba(255,250,247,0) 0deg 8deg, rgba(142,47,85,0.1) 8deg 8.4deg, rgba(255,240,245,0.12) 8.4deg 9deg)" }} />
        </AbsoluteFill>
      );
    }
    case "blossoms":
      return <AbsoluteFill style={blossomsMask(p)}>{children}</AbsoluteFill>;
    case "branch": {
      // the edge goes from top-left to bottom-right, the branch along it
      const pos = -25 + 150 * e;
      const mask = `linear-gradient(125deg, #000 ${pos - 8}%, transparent ${pos + 2}%)`;
      const x = -1400 + 3300 * e, y = -700 + 1800 * e;
      return (
        <>
          <AbsoluteFill style={{ WebkitMaskImage: mask, maskImage: mask }}>{children}</AbsoluteFill>
          <div style={{ position: "absolute", left: x, top: y, transform: "rotate(-55deg)", transformOrigin: "50% 50%", pointerEvents: "none" }}>
            <Branch grow={1} seed={17} width={1700} />
          </div>
          <Gust p={span(p, 0.1, 1)} count={50} seed={23} dir={1} />
        </>
      );
    }
    case "moon": {
      const r = 1250 * Math.pow(e, 1.3);
      const mask = `radial-gradient(circle at 50% 46%, #000 ${r}px, transparent ${r + 2}px)`;
      return (
        <>
          <AbsoluteFill style={{ WebkitMaskImage: mask, maskImage: mask }}>
            <AbsoluteFill style={{ transform: `scale(${1.25 - 0.25 * o})` }}>{children}</AbsoluteFill>
          </AbsoluteFill>
          <div style={{ position: "absolute", left: 960 - r - 14, top: 497 - r - 14, width: (r + 14) * 2, height: (r + 14) * 2, borderRadius: "50%",
            border: "14px solid #fbe0e7", boxShadow: "0 0 30px rgba(217,87,127,0.5), inset 0 0 30px rgba(217,87,127,0.4)", opacity: 1 - span(p, 0.7, 1), pointerEvents: "none" }} />
        </>
      );
    }
    case "curtain": {
      const y = -120 + 1300 * e;
      // the lower edge waving, from the right to the left
      const wave = Array.from({ length: 25 }, (_, i) => `L${(24 - i) * 80} ${(y + Math.sin((24 - i) * 0.9 + frame / 5) * 34).toFixed(1)}`).join(" ");
      return (
        <>
          <AbsoluteFill style={svgMask(`<path d="M0 -10 L1920 -10 ${wave} Z" fill="white"/>`)}>
            <AbsoluteFill style={{ transform: `translateY(${(1 - o) * -60}px)` }}>{children}</AbsoluteFill>
          </AbsoluteFill>
          <PetalEdge y={y - 10} t={frame} />
        </>
      );
    }
    default: // page
      return (
        <>
          <AbsoluteFill style={{ transformOrigin: "0% 50%", transform: `perspective(2600px) rotateY(${(1 - o) * 80}deg)`, opacity: span(p, 0, 0.3) }}>{children}</AbsoluteFill>
          <Gust p={span(p, 0, 0.9)} count={50} seed={29} dir={1} />
        </>
      );
  }
};

/** A scene leaving while the next one comes in its way (frames start .. start + dur of the scene). */
const Exit: React.FC<{ kind: Kind; start: number; dur: number; children: React.ReactNode }> = ({ kind, start, dur, children }) => {
  const frame = useCurrentFrame();
  const q = Math.max(0, Math.min(1, (frame - start) / dur));
  if (q <= 0) return <AbsoluteFill>{children}</AbsoluteFill>;
  const e = inOutCubic(q);
  const fadeEnd = 1 - span(q, 0.55, 1);
  let style: React.CSSProperties;
  switch (kind) {
    case "gust":
      style = { transform: `translate(${-1700 * e}px, ${-140 * e}px) rotate(${-9 * e}deg)`, filter: `blur(${e * 8}px)`, opacity: 1 - span(q, 0.4, 0.9) };
      break;
    case "bloom":
    case "blossoms":
      style = { transform: `scale(${1 - 0.08 * e})`, filter: `blur(${e * 4}px) brightness(${1 + 0.15 * e})`, opacity: fadeEnd };
      break;
    case "moon":
      style = { transform: `scale(${1 + 0.5 * e})`, filter: `blur(${e * 6}px)`, opacity: fadeEnd };
      break;
    case "curtain":
      style = { transform: `translateY(${e * 120}px)`, opacity: fadeEnd };
      break;
    case "page":
      style = { transformOrigin: "100% 50%", transform: `perspective(2600px) rotateY(${-80 * e}deg)`, opacity: 1 - span(q, 0.5, 0.9) };
      break;
    default: // fan, branch: covered, fading
      style = { opacity: fadeEnd, filter: `brightness(${1 + 0.1 * e})` };
  }
  return <AbsoluteFill style={style}>{children}</AbsoluteFill>;
};

// ---------------------------------------------------------------------------------------------------------------- video

/** The video: the avenue all along, the scenes over it on the beats of the song (it plays whole, no cut). */
export const Promo: React.FC<PromoProps> = ({ plan, music }) => {
  const { durationInFrames } = useVideoConfig();
  const frame = useCurrentFrame();
  type Item = { beats: number; node: (inFrames: number) => React.ReactNode };
  const items: Item[] = [];
  let groups = 0;
  for (const part of plan.parts) {
    items.push({ beats: CHAPTER, node: () => <ChapterCard part={part} /> });
    part.scenes.forEach((slides) => {
      const swap = SWAPS[slides.length > 1 ? groups++ % SWAPS.length : 0];
      items.push({ beats: slides.length * SLIDE, node: (inFrames) => <FeatureScene slides={slides} part={part} swap={swap} inFrames={inFrames} /> });
    });
  }
  const kindOf = (i: number) => KINDS[i % KINDS.length];
  let at = plan.intro;
  const scenes = items.map((it, i) => {
    const from = frameOf(at);
    const own = frameOf(at + it.beats) - from;
    const seq = (
      <Sequence key={i} from={from} durationInFrames={own + TRANS}>
        <Exit kind={kindOf(i + 1)} start={own} dur={TRANS}>
          <Enter kind={kindOf(i)}>{it.node(TRANS)}</Enter>
        </Exit>
      </Sequence>
    );
    at += it.beats;
    return seq;
  });
  const outroFrom = frameOf(at);
  // a burst of petals on each big drum hit
  const bursts = ACCENTS.map((a, i) => {
    const f = frameOf(a);
    return <Sequence key={`b${i}`} from={f} durationInFrames={Math.round(BEAT * 2)}><BurstAt seed={i * 13 + 5} /></Sequence>;
  });
  return (
    <AbsoluteFill style={{ backgroundColor: CREAM }}>
      <Audio src={staticFile(music)} volume={(f) => interpolate(f, [0, 4, durationInFrames - 10, durationInFrames], [0, 1, 1, 0], clamp)} />
      <Avenue frame={frame} dolly={frame / durationInFrames} />
      <PetalField frame={frame} count={46} opacity={0.85} />
      <Sequence from={0} durationInFrames={frameOf(plan.intro) + TRANS}>
        <Exit kind={kindOf(0)} start={frameOf(plan.intro)} dur={TRANS}><Intro beats={plan.intro} /></Exit>
      </Sequence>
      {scenes}
      <Sequence from={outroFrom} durationInFrames={durationInFrames - outroFrom}>
        <Enter kind={kindOf(items.length)}><Outro beats={plan.outro} /></Enter>
      </Sequence>
      {bursts}
      {/* petals near the lens, in front of everything */}
      <PetalField frame={frame + 5000} count={10} opacity={0.75} />
    </AbsoluteFill>
  );
};

const BurstAt: React.FC<{ seed: number }> = ({ seed }) => {
  const frame = useCurrentFrame();
  return <Burst p={frame / (BEAT * 2)} seed={seed} />;
};
