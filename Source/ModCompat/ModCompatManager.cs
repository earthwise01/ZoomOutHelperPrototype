using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using Celeste.Mod.FunctionalZoomOut.Utils;
using Microsoft.Xna.Framework.Graphics;

namespace Celeste.Mod.FunctionalZoomOut.ModCompat;

// bweh i need to improve this still,
public static class ModCompatManager {
    private static readonly FrostHelperCompat FrostHelperCompat = new();
    private static readonly MaddieHelpingHandCompat MaddieHelpingHandCompat = new();
    private static readonly StyleMaskHelperCompat StyleMaskHelperCompat = new();

    [HookLoadCallback("modHooks")]
    public static void Load() {
        if (FrostHelperCompat.Module is not null) {
            FrostHelperCompat.InitReflection();
            FrostHelperCompat.Load();
            Logger.Info("ZoomOutHelperPrototype", "loaded cross helper hooks for frost helper!");
        }

        if (MaddieHelpingHandCompat.Module is not null) {
            MaddieHelpingHandCompat.InitReflection();
            MaddieHelpingHandCompat.Load();
            Logger.Info("ZoomOutHelperPrototype", "loaded cross helper hooks for maddie helping hand!");
        }

        if (StyleMaskHelperCompat.Module is not null) {
            StyleMaskHelperCompat.InitReflection();
            StyleMaskHelperCompat.Load();
            Logger.Info("ZoomOutHelperPrototype", "loaded cross helper hooks for style mask helper!");
        }
    }

    [HookUnloadCallback("modHooks")]
    public static void Unload() {
        FrostHelperCompat.Unload();
        MaddieHelpingHandCompat.Unload();
        StyleMaskHelperCompat.Unload();
    }

    public static Type GetExternalType(Assembly assembly, string name) {
        var result = assembly.GetType(name);
        if (result is null)
            Logger.Warn("ZoomOutHelperPrototype", $"failed to find type {name}!");

        return result;
    }

}
