using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using Celeste.Mod.FunctionalZoomOut.Utils;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Celeste.Mod.FunctionalZoomOut.ModCompat;

// bweh i need to improve this still,
public static class ModCompatManager {
    private static readonly List<ModCompatBase> ModCompatList = [
        //new StyleMaskHelperCompat()
    ];

    [HookLoadCallback("modHooks")]
    public static void Load() {
        foreach (var modCompat in ModCompatList) {
            modCompat.Load();
        }
    }

    [HookUnloadCallback("modHooks")]
    public static void Unload() {
        foreach (var modCompat in ModCompatList) {
            modCompat.Unload();
        }
    }
}
