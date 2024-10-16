using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using Celeste.Mod.FunctionalZoomOut.Utils;
using Microsoft.Xna.Framework.Graphics;

namespace Celeste.Mod.FunctionalZoomOut;

public class FunctionalZoomOutModule : EverestModule {

    // mostly unused atm, just here in case i need to add backwards compatibility stuff in the future
    internal const int ModVersionNumber = 1;

    #region Instance/Settings/Session/SaveData

    public static FunctionalZoomOutModule Instance { get; private set; }

    public override Type SettingsType => typeof(FunctionalZoomOutModuleSettings);
    public static FunctionalZoomOutModuleSettings Settings => (FunctionalZoomOutModuleSettings)Instance._Settings;

    public override Type SessionType => typeof(FunctionalZoomOutModuleSession);
    public static FunctionalZoomOutModuleSession Session => (FunctionalZoomOutModuleSession)Instance._Session;

    public override Type SaveDataType => typeof(FunctionalZoomOutModuleSaveData);
    public static FunctionalZoomOutModuleSaveData SaveData => (FunctionalZoomOutModuleSaveData)Instance._SaveData;

    #endregion

    public FunctionalZoomOutModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel("ZoomOutHelperPrototype", LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel("ZoomOutHelperPrototype", LogLevel.Info);
#endif
    }

    #region Load and Unload

    public override void Load() {
        HookHelper.Initialize(typeof(FunctionalZoomOutModule).Assembly);
        HookHelper.LoadTag("loader");
    }

    public override void Unload() {
        HookHelper.Uninitialize();

        // reset the distort shader before unloading
        if (EffectsSwapped && GFX.FxDistort is not null) {
            GFX.FxDistort = orig_FxDistort;
        }
        EffectsSwapped = false;
    }

    public override void LoadContent(bool firstLoad) {
        base.LoadContent(firstLoad);

        // ??= makes it only load once and not hot reload with the code mod but also means i don't have to worry abt it like creating extra copies when hot reloading either so
        FxDistortScalable ??= new(Engine.Graphics.GraphicsDevice, Everest.Content.Get("ZoomOutHelperPrototype:/Effects/FunctionalZoomOut/ZoomableDistort.fxb").Data) {
            Name = "Effects/FunctionalZoomOut/ZoomableDistort"
        };

        orig_FxDistort ??= GFX.FxDistort;
    }

    internal static Effect orig_FxDistort;
    internal static Effect FxDistortScalable;

    #endregion

    #region Current Zoom Status

    private static float TargetCameraScale { get; set; }
    private static float CurrentCameraScale { get; set; }

    public static bool LevelContainsZoomOut { get; private set; }
    public static bool ZoomOutActive => LevelContainsZoomOut && CurrentCameraScale != 1f;
    public static float CameraScale {
        get {
            return CurrentCameraScale;
        }
        set {
            if (!LevelContainsZoomOut)
                return;

            TargetCameraScale = value;
        }
    }
    public static float CanvasScale => (int)MathF.Ceiling(CurrentCameraScale);

    public static void ResetStaticFields() {
        TargetCameraScale = 1f;
        CurrentCameraScale = 1f;
        LevelContainsZoomOut = false;
    }

    // zoom getter methods
    public static float GetFixedCameraSize(float orig) {
        if (!ZoomOutActive)
            return orig;

        return MathF.Ceiling(orig * CurrentCameraScale);
    }
    public static float GetFixedCanvasSize(float orig) {
        if (!ZoomOutActive)
            return orig;

        return MathF.Ceiling(orig * CanvasScale);
    }
    public static float GetFixedHDUpscale(float orig) {
        if (!ZoomOutActive)
            return orig;

        return orig * (Celeste.TargetWidth / GetFixedCameraSize(Celeste.GameWidth) / 6f);
    }

    public static int GetFixedCameraSizeInt(int orig) => (int)GetFixedCameraSize(orig);
    public static int GetFixedCanvasSizeInt(int orig) => (int)GetFixedCanvasSize(orig);

    public static float GetFixedCameraSizePadded(float orig, int padding) => GetFixedCameraSize(orig - padding) + padding;
    public static int GetFixedCameraSizeIntPadded(int orig, int padding) => (int)GetFixedCameraSize(orig - padding) + padding;

    #endregion

    #region Loading/Unloading Zoom Out
    // (e.g. hooks, shaders, gameplay buffers both global and not, etc. just anything related to loading stuff)

    internal static bool MainHooksLoaded { get; private set; }

    private static void UpdateMainHooks(bool loadHooks) {
        if (MainHooksLoaded == loadHooks) {
            Logger.Info("ZoomOutHelperPrototype", $"already in correct hook state ({(MainHooksLoaded ? "loaded" : "unloaded")}).");
            return;
        }

        if (MainHooksLoaded) {
            Logger.Info("ZoomOutHelperPrototype", "unloading main hooks...");
            HookHelper.UnloadTag("mainZoomHooks");
            HookHelper.UnloadTag("modHooks");
            RenderTargetScaleManager.UntrackAll();
        }

        MainHooksLoaded = loadHooks;

        if (loadHooks) {
            Logger.Info("ZoomOutHelperPrototype", "loading main hooks...");
            HookHelper.LoadTag("mainZoomHooks");
            HookHelper.LoadTag("modHooks");
        }

        LevelContainsZoomOut = MainHooksLoaded;
    }

    private static bool SessionHasZoomOut(Session session, out float initialScale, out int version) {
        var zoomOutEntities = session.MapData.Levels.SelectMany(levelData => levelData.Entities).Where(entityData => entityData.Name.StartsWith("ZoomOutHelperPrototype"));

        var globalController = zoomOutEntities.FirstOrDefault(entityData => entityData.Name.EndsWith("GlobalZoomController"));
        if (globalController is not null) {
            initialScale = globalController.Float("initialCameraScale", 1f);
            version = globalController.Int("_modVersion", 1);
            return true;
        }

        initialScale = 1f;
        version = 1;
        return false;
    }

    [OnHook(typeof(LevelLoader), "ctor", hookType: HookTypes.Constructor, parameters: [typeof(Session), typeof(Vector2?)], tag: "loader")]
    private static void On_LevelLoader_ctor(On.Celeste.LevelLoader.orig_ctor orig, LevelLoader self, Session session, Vector2? startPosition) {
        ResetStaticFields();
        bool hasZoomOut = SessionHasZoomOut(session, out float initalScale, out int _);

        // if (Settings.AlwaysEnableZoomOut) {
        //     zoomOut = true;
        //     initialScale = 2f;
        //     canvasScale = 2;
        // }

        if (hasZoomOut) {
            UpdateMainHooks(true);
            CurrentCameraScale = TargetCameraScale = initalScale;
        } else {
            UpdateMainHooks(false);
        }

        orig(self, session, startPosition);
    }

    /*
    bwehh,,
    */

    [OnHook(typeof(OverworldLoader), "ctor", hookType: HookTypes.Constructor, parameters: [typeof(Overworld.StartMode), typeof(HiresSnow)], tag: "loader")]
    private static void On_OverworldLoader_ctor(On.Celeste.OverworldLoader.orig_ctor orig, OverworldLoader self, Overworld.StartMode startMode, HiresSnow hiresSnow) {
        orig(self, startMode, hiresSnow);

        // dont mistakenly unload the hooks when using a collabutils chapter panel
        if (startMode != (Overworld.StartMode)(-1))
            UpdateMainHooks(false);
    }

    #endregion

    #region Updating Zoom Out Status

    [OnHook(typeof(Level), nameof(Level.Begin), tag: "mainZoomHooks")]
    private static void On_Level_Begin(On.Celeste.Level.orig_Begin orig, Level self) {
        orig(self);

        if (LevelContainsZoomOut) {
            RenderTargetScaleManager.Track(
                GameplayBuffers.Gameplay,
                GameplayBuffers.Level,
                GameplayBuffers.ResortDust,
                GameplayBuffers.Light,
                GameplayBuffers.Displacement,
                GameplayBuffers.SpeedRings,
                GameplayBuffers.TempA,
                GameplayBuffers.TempB
            );
            RenderTargetScaleManager.Track(GameplayBuffers.MirrorSources, 64);
            RenderTargetScaleManager.Track(GameplayBuffers.MirrorMasks, 64);

            // SwapEffects(true);
        }

        // CurrentCameraScale = 1f;
        UpdateLevelZoomOut(self);
    }

    [OnHook(typeof(Level), nameof(Level.End), tag: "loader")]
    private static void On_Level_End(On.Celeste.Level.orig_End orig, Level self) {
        orig(self);
    }

    [OnHook(typeof(Level), nameof(Level.Update), tag: "mainZoomHooks")]
    private static void On_Level_Update(On.Celeste.Level.orig_Update orig, Level self) {
        UpdateLevelZoomOut(self);

        orig(self);
    }

    // why am i even doing it like this
    private static void UpdateLevelZoomOut(Level level) {
        var player = level.Tracker.GetEntity<Player>();
        if (player is null) {
            // Logger.Info("ZoomOutHelperPrototype", "can't update zoom level with a null player!");
            return;
        }

        var data = DynamicData.For(level);

        float? levelScale = (float?)data.Get("ZoomOutHelperPrototype_CurrentZoom");
        if (levelScale != TargetCameraScale) {
            // grab stuff from the current state
            var camera = level.Camera;
            var cameraOffsetFromTarget = camera.Position - level.GetFullCameraTargetAt(player, player.Position);

            // update the scale variables
            levelScale = CurrentCameraScale = TargetCameraScale;

            // update the camera size for culling, etc
            camera.Viewport.Width = GetFixedCameraSizeInt(Celeste.GameWidth);
            camera.Viewport.Height = GetFixedCameraSizeInt(Celeste.GameHeight);

            // update the camera position
            // camera can clip oob when chaning zoom scale atm, need to work on that
            // tbf in general i could probably handle this better
            camera.Position = level.GetFullCameraTargetAt(player, player.Position) + cameraOffsetFromTarget;
            camera.UpdateMatrices();
        }

        data.Set("ZoomOutHelperPrototype_CurrentZoom", levelScale);

        RenderTargetScaleManager.Update();
    }

    [OnHook(typeof(Level), nameof(Level.orig_LoadLevel), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Level_LoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader) {
        orig(self, playerIntro, isFromLoader);
        UpdateLevelZoomOut(self);
    }

    #endregion

    #region stuff for swapping fxdistort

    private static bool EffectsSwapped = false;

    internal static void SwapVanillaEffects(bool swap) {
        if (EffectsSwapped == swap)
            return;
        //Logger.Info("ZoomOutHelperPrototype", $"effects are already swapped/unswapped ({swap})");

        if (swap) {
            EffectsSwapped = true;

            if (GFX.FxDistort != orig_FxDistort)
                Logger.Warn("ZoomOutHelperPrototype", "GFX.FxDistort refers to an unexpected shader! (i.e. not equal to orig_FxDistort)");

            CopyEffectParams(orig_FxDistort, FxDistortScalable, "text", "map", "scale");
            GFX.FxDistort = FxDistortScalable;
        } else {
            EffectsSwapped = false;

            if (GFX.FxDistort != FxDistortScalable)
                Logger.Warn("ZoomOutHelperPrototype", "GFX.FxDistort refers to an unexpected shader! (i.e. it is not equal to FxDistortScalable)");

            CopyEffectParams(FxDistortScalable, orig_FxDistort, "text", "map", "scale");
            GFX.FxDistort = orig_FxDistort;
        }
    }

    private static void CopyEffectParams(Effect source, Effect target, params string[] ignoreParams) {
        var ignoreHashset = ignoreParams.ToHashSet();

        Logger.Debug("ZoomOutHelperPrototype", $"copying effect params from source {source.Name} to target {target.Name}");

        foreach (var parameter in source.Parameters) {
            if (ignoreHashset.Contains(parameter.Name))
                continue;
            Logger.Verbose("ZoomOutHelperPrototype", $"source: {parameter.Name}, {parameter.ParameterClass}, {parameter.ParameterType}");

            if (target.Parameters.Any(targetParam => targetParam.Name == parameter.Name)) {
                Logger.Verbose("ZoomOutHelperPrototype", $"both the target and source effect contain the parameter {parameter.Name}!");
            } else {
                Logger.Verbose("ZoomOutHelperPrototype", $"the target effect does not contain the parameter {parameter.Name}!");
                continue;
            }

            switch (parameter.ParameterClass, parameter.ParameterType) {
                case (EffectParameterClass.Scalar, EffectParameterType.Single):
                    target.Parameters[parameter.Name].SetValue(parameter.GetValueSingle());
                    break;
                case (EffectParameterClass.Vector, EffectParameterType.Single):
                    target.Parameters[parameter.Name].SetValue(parameter.GetValueVector2());
                    break;
                default:
                    // not implemented yet
                    break;
            };
        }
    }

    #endregion

    [Command($"set_camera_scale", "sets the camera scale (for zoom out)")]
    public static void SetZoomOutCommand(float scale = 1f) {
        if (!LevelContainsZoomOut) {
            Engine.Commands.Log($"not in a valid level! (i.e. a level containing a global zoom controller)");
            return;
        }

        if (scale <= 0f) {
            Engine.Commands.Log($"invalid scale! only scales above 0.00 are supported");
            return;
        }

        CameraScale = scale;
        Engine.Commands.Log($"set the target camera scale to {scale:N2}");
    }
}
