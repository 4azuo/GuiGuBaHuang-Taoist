import { Composition } from "remotion";
import { Promo, PromoProps } from "./Promo";
import { FPS, beatsOf, frameOf, plans } from "./data";

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
  </>
);
