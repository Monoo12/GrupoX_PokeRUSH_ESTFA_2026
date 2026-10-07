import React from "react";
import { Composition, Folder } from "remotion";
import { DURACION } from "./datos/tiempos";
import { Storyboard } from "./Storyboard";
import { Trailer } from "./Trailer";

export const RemotionRoot: React.FC = () => {
  return (
    <>
      <Composition
        id="Trailer"
        component={Trailer}
        durationInFrames={DURACION}
        fps={30}
        width={1920}
        height={1080}
        defaultProps={{ usarCapturas: true }}
      />
      <Composition
        id="Storyboard"
        component={Storyboard}
        durationInFrames={DURACION}
        fps={30}
        width={1920}
        height={1080}
        defaultProps={{ usarCapturas: true }}
      />
      <Folder name="Reconstrucciones">
        <Composition
          id="Trailer-Reconstruccion"
          component={Trailer}
          durationInFrames={DURACION}
          fps={30}
          width={1920}
          height={1080}
          defaultProps={{ usarCapturas: false }}
        />
        <Composition
          id="Storyboard-Reconstruccion"
          component={Storyboard}
          durationInFrames={DURACION}
          fps={30}
          width={1920}
          height={1080}
          defaultProps={{ usarCapturas: false }}
        />
      </Folder>
    </>
  );
};
