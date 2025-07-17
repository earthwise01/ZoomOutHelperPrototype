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
    public bool ModLoaded => Module is not null;

    public EverestModule Module => Everest.Modules.FirstOrDefault(module => module.Metadata.Name == Name);
    public Assembly Assembly => Module.GetType().Assembly;
    public Version LoadedVersion => Module.Metadata.Version;
    public bool ShouldLoadForVersion => (MinVersion == null || MinVersion <= LoadedVersion) && (MaxVersion == null || MaxVersion >= LoadedVersion);

    public abstract string Name { get; }
    public virtual Version MinVersion { get => null; }
    public virtual Version MaxVersion { get => null; }


    public bool Loaded { get; private set; } = false;
    protected abstract void LoadHooks();
    protected abstract void UnloadHooks();
    protected abstract void LoadReflection();

    public void Load() {
        if (Loaded || !ModLoaded || !ShouldLoadForVersion)
            return;

        Logger.Info("ZoomOutHelperPrototype", $"loading cross helper hooks for {Name} v{LoadedVersion}...");

        LoadReflection();
        LoadHooks();

        Loaded = true;
    }

    public void Unload() {
        if (!Loaded)
            return;

        UnloadHooks();

        Loaded = false;
    }

    protected Type GetModdedType(string name) {
        var result = Assembly.GetType(name);

        if (result is null)
            Logger.Warn("ZoomOutHelperPrototype", $"[{Name} Mod Compat] failed to find type {name} in assembly!");

        return result;
    }

    /// <summary>
    /// does nothing, just exists to be used to force the jit to recompile methods because of Inlining(tm)
    /// </summary>
    protected static void IL_DummyHook(ILContext il) {
        _ = new ILCursor(il);
    }

}
