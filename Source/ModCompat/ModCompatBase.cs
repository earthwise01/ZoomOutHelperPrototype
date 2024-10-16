using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using Celeste.Mod.FunctionalZoomOut.Utils;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;

namespace Celeste.Mod.FunctionalZoomOut.ModCompat;

public abstract class ModCompatBase {
    public EverestModule Module => Everest.Modules.FirstOrDefault(module => module.Metadata.Name == Name);

    public abstract string Name { get; }

    public abstract void Load();
    public abstract void Unload();
    public abstract void InitReflection();

    /// <summary>
    /// does nothing, just exists to be used to force the jit to recompile methods because of Inlining(tm)
    /// </summary>
    protected static void IL_DummyHook(ILContext il) {
        _ = new ILCursor(il);
    }

}
