import { Composition } from "remotion";
import { Promo, PromoProps } from "./Promo";
import { FPS, beatsOf, frameOf, plans } from "./data";
import { AvenueLayer, LAYER_PAD } from "./Sakura";

export const Root: React.FC = () => (
  <>
    {/* every promo picture by chapter (Steam Workshop / Ko-fi page) */}
    <Composition
      id="TaoistPromo"
      component={Promo}
      durationInFrames={frameOf(beatsOf(plans.long))}
      fps={FPS}
      width={1920}
      height={1080}
      defaultProps={{ plan: plans.long, music: "music-long.wav" } satisfies PromoProps}
    />
    {/* a layer of the avenue's trees, still, transparent: rendered once to public/avenue/<layer>.png (scripts/render-layers.mjs) */}
    <Composition
      id="AvenueLayer"
      component={AvenueLayer}
      durationInFrames={1}
      fps={FPS}
      width={1920 + LAYER_PAD.x * 2}
      height={1080 + LAYER_PAD.y * 2}
      defaultProps={{ layer: "far" as const }}
    />
  </>
);
