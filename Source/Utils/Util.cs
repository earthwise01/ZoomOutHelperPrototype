using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.Entities;
using MonoMod;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Mono.Cecil.Cil;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Utils;
using System.Linq;

namespace Celeste.Mod.FunctionalZoomOut.Utils;

// not sure what the point of this is rn, mostly just random stuff i think might be nice for modinterop in the future but also that shd just go in a proper modinterop file
public static class Util {
    public static bool LevelContainsZoomOut => FunctionalZoomOutModule.HooksActive;
    public static bool ZoomOutActive => FunctionalZoomOutModule.ZoomOutActive;
    public static float CameraScale => FunctionalZoomOutModule.CameraScale;
    public static float CameraWidth => FunctionalZoomOutModule.GetFixedCameraSize(Celeste.GameWidth);
    public static float CameraHeight => FunctionalZoomOutModule.GetFixedCameraSize(Celeste.GameHeight);
    public static Vector2 CameraDimensions => new(CameraWidth, CameraHeight);

    public static void SetCameraScale(float value) => FunctionalZoomOutModule.CameraScale = value;
}
