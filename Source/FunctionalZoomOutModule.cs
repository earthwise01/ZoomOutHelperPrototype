using Celeste.Mod.FunctionalZoomOut.Interop;
using MonoMod.ModInterop;

namespace Celeste.Mod.FunctionalZoomOut;

public class FunctionalZoomOutModule : EverestModule {

    // mostly unused atm, just here in case i need to add backwards compatibility stuff in the future
    internal const int ModVersionNumber = 2;

    #region Instance/Settings/Session/SaveData

    public static Module Instance { get; private set; }

    public override Type SettingsType => typeof(FunctionalZoomOutSettings);
    public static FunctionalZoomOutSettings Settings => (FunctionalZoomOutSettings)Instance._Settings;

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

        typeof(MotionSmoothingImports).ModInterop();

        Everest.Events.Level.OnLoadEntity += Event_OnLoadEntity;
    }

    public override void Unload() {
        HookHelper.Uninitialize();

        Everest.Events.Level.OnLoadEntity -= Event_OnLoadEntity;

        // reset the distort shader before unloading
        SwapVanillaEffects(false);
        orig_FxDistort = null;

        FxDistortScalable?.Dispose();
        FxDistortScalable = null;
    }

    public override void LoadContent(bool firstLoad) {
        base.LoadContent(firstLoad);

        FxDistortScalable ??= new(Engine.Graphics.GraphicsDevice, Everest.Content.Get("ZoomOutHelperPrototype:/Effects/FunctionalZoomOut/ZoomableDistort.fxb").Data) {
            Name = "Effects/FunctionalZoomOut/ZoomableDistort"
        };

        orig_FxDistort ??= GFX.FxDistort;
    }

    public override void PrepareMapDataProcessors(MapDataFixup context) {
        base.PrepareMapDataProcessors(context);

        context.Add<FunctionalZoomOutMapDataProcessor>();
    }

    private static bool Event_OnLoadEntity(Level level, LevelData levelData, Vector2 offset, EntityData entityData) {
        if (entityData.Name == "ZoomOutHelperPrototype/GlobalZoomController")
            return true;

        return false;
    }

    #endregion

    #region Current Zoom Status

    // only allows camera scales which result in an even camera width
    // technically makes zooming out more jittery but i feel like almost has the opposite effect since now the camera always expands by 1px on each side
    // in theory if you want the *smoothest* possible zooming you need to make the camera be able to visually take a subpixel position!
    // ...however extended camera dynamics already tried this at first and i feel like it ended up not being worth the issues it caused oops (especially styleground jitter i swear like i saw so many ppl switching over to hi res parallax to work around that)
    public static bool PixelPerfectZooming = true;

    internal static float TargetCameraScale;
    internal static float CurrentCameraScale;
    public static bool CameraScaleChanged => CurrentCameraScale != TargetCameraScale;

    public static bool HooksActive { get; private set; } // if currently in a map containing zoom out and hooks are loaded
    public static bool ZoomOutActive => HooksActive && CurrentCameraScale != 1f; // if hooks are both loaded and zoom out is in use
    public static float CameraScale {
        get {
            return CurrentCameraScale;
        }
        set {
            if (!HooksActive)
                return;

            if (Settings.CameraScaleMaximum > 0f)
                value = Math.Min(value, Settings.CameraScaleMaximum);

            if (PixelPerfectZooming)
                value = MathF.Round(value * 160f) / 160f;

            TargetCameraScale = value;
        }
    }

    public static int CanvasScale => (int)MathF.Ceiling(CurrentCameraScale);
    public static int GameplayBufferWidth => Celeste.GameWidth * CanvasScale;
    public static int GameplayBufferHeight => Celeste.GameHeight * CanvasScale;

    public static Vector2 CenterFixOffset => new(MathF.Ceiling(Celeste.GameWidth / 2f * CameraScale) - Celeste.GameWidth / 2f, MathF.Ceiling(Celeste.GameHeight / 2f * CameraScale) - Celeste.GameHeight / 2f);

    public static void ResetStaticFields() {
        TargetCameraScale = 1f;
        CurrentCameraScale = 1f;
    }

    // zoom getter methods
    public static float GetFixedCameraSize(float orig) {
        if (!ZoomOutActive)
            return orig;

        return MathF.Ceiling(orig * CurrentCameraScale);
    }
    public static float GetFixedCanvasSizeFloat(float orig) {
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
    public static int GetFixedCanvasSizeInt(int orig) => (int)GetFixedCanvasSizeFloat(orig);

    public static float GetFixedCameraSizePadded(float orig, int padding) => GetFixedCameraSize(orig - padding) + padding;
    public static int GetFixedCameraSizeIntPadded(int orig, int padding) => (int)GetFixedCameraSize(orig - padding) + padding;

    public static bool EnsureBufferDimensions(VirtualRenderTarget target, int padding = 0) {
        if (target == null)
            return false;

        target = MotionSmoothingImports.GetResizableBuffer?.Invoke(target) ?? target;

        if (target.IsDisposed || (target.Width + padding == GameplayBufferWidth + padding && target.Height + padding == GameplayBufferHeight + padding))
            return false;

        target.Width = GameplayBufferWidth + padding;
        target.Height = GameplayBufferHeight + padding;
        target.Reload();
        return true;
    }

    #endregion

    #region Loading/Unloading

    private static void UpdateMainHooks(bool loadHooks) {
        if (HooksActive == loadHooks) {
            Logger.Debug("ZoomOutHelperPrototype", $"already in correct hook state ({(HooksActive ? "loaded" : "unloaded")}).");
            return;
        }

        if (HooksActive) {
            Logger.Info("ZoomOutHelperPrototype", "unloading main hooks...");
            HookHelper.UnloadTag("mainZoomHooks");
            SwapVanillaEffects(false);
        }

        HooksActive = loadHooks;

        if (loadHooks) {
            Logger.Info("ZoomOutHelperPrototype", "loading main hooks...");
            HookHelper.LoadTag("mainZoomHooks");
        }
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

    #region Updating

    [OnHook(typeof(Level), nameof(Level.Begin), tag: "mainZoomHooks")]
    private static void On_Level_Begin(On.Celeste.Level.orig_Begin orig, Level self) {
        orig(self);

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
    internal static void UpdateLevelZoomOut(Level level) {
        var player = level.Tracker.GetEntity<Player>();
        if (player is null) {
            // Logger.Info("ZoomOutHelperPrototype", "can't update zoom level with a null player!");
            return;
        }

        // var data = DynamicData.For(level);

        // float? levelScale = (float?)data.Get("ZoomOutHelperPrototype_CurrentZoom");
        if (1f / level.Zoom != TargetCameraScale && (CurrentCameraScale != 1f || TargetCameraScale != 1f)) {
            // grab stuff from the current state
            var camera = level.Camera;
            var diffFromTarget = camera.Position - player.CameraTarget;
            var cameraCenter = camera.Position + new Vector2(camera.Viewport.Width / 2f, camera.Viewport.Height / 2f);

            // update the scale variables
            CurrentCameraScale = TargetCameraScale;

            // var prev = levelScale ?? TargetCameraScale;
            // levelScale = CurrentCameraScale = prev + (TargetCameraScale - prev) * (1f - (float)Math.Pow(0.01f / 4f, Engine.DeltaTime));

            // update the camera size for culling, etc
            camera.Viewport.Width = GetFixedCameraSizeInt(Celeste.GameWidth);
            camera.Viewport.Height = GetFixedCameraSizeInt(Celeste.GameHeight);

            // update level.Zoom
            level.Zoom = level.ZoomTarget = 1f / CurrentCameraScale;
            level.ZoomFocusPoint = new Vector2(level.Camera.Viewport.Width / 2f, level.Camera.Viewport.Height / 2f);

            // update the camera position
            // tbf in general i could probably handle this better
            // enforce level bounds
            if (!level.Transitioning && player.EnforceLevelBounds) {
                camera.Position = player.CameraTarget + diffFromTarget;
                camera.X = MathHelper.Clamp(camera.X, level.Bounds.Left, level.Bounds.Right - camera.Viewport.Width);
                camera.Y = MathHelper.Clamp(camera.Y, level.Bounds.Top, level.Bounds.Bottom - camera.Viewport.Height);
            } else if (!level.Transitioning) {
                camera.Position = cameraCenter - new Vector2(camera.Viewport.Width / 2f, camera.Viewport.Height / 2f);
            }

            camera.UpdateMatrices();
        }

        // data.Set("ZoomOutHelperPrototype_CurrentZoom", levelScale);
        // i really *really* should make it so that Level.Zoom / Level.ZoomTarget are used or at least set so that mod compat works with excameradynamics better but i am   so fkn exhausted right now bleh

        EnsureVanillaBuffers();
    }

    [OnHook(typeof(Level), nameof(Level.orig_LoadLevel), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Level_LoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader) {
        orig(self, playerIntro, isFromLoader);
        UpdateLevelZoomOut(self);
    }

    private static void EnsureVanillaBuffers() {
        bool needToReloadLargeTextures = false;

        needToReloadLargeTextures |= EnsureBufferDimensions(GameplayBuffers.Gameplay);
        needToReloadLargeTextures |= EnsureBufferDimensions(GameplayBuffers.Level);
        EnsureBufferDimensions(GameplayBuffers.Light);
        EnsureBufferDimensions(GameplayBuffers.Displacement);
        EnsureBufferDimensions(GameplayBuffers.ResortDust);
        needToReloadLargeTextures |= EnsureBufferDimensions(GameplayBuffers.TempA);
        needToReloadLargeTextures |= EnsureBufferDimensions(GameplayBuffers.TempB);
        // ??
        EnsureBufferDimensions(GameplayBuffers.MirrorSources, 64);
        EnsureBufferDimensions(GameplayBuffers.MirrorMasks, 64);

        UpdateEffectSwap();

        if (needToReloadLargeTextures)
            MotionSmoothingImports.ReloadLargeTextures?.Invoke();
    }

    #endregion

    #region FxDistort Swapping

    private static void UpdateEffectSwap() {
        if (CanvasScale != 1) {
            SwapVanillaEffects(true);
        } else {
            SwapVanillaEffects(false);
        }

        FxDistortScalable.Parameters["scale"].SetValue((float)CanvasScale);
    }

    private static bool effectsSwapped = false;
    internal static Effect orig_FxDistort;
    internal static Effect FxDistortScalable;

    internal static void SwapVanillaEffects(bool swap) {
        if (effectsSwapped == swap)
            return;
        //Logger.Info("ZoomOutHelperPrototype", $"effects are already swapped/unswapped ({swap})");

        if (swap) {
            if (GFX.FxDistort != orig_FxDistort)
                Logger.Warn("ZoomOutHelperPrototype", "GFX.FxDistort refers to an unexpected shader! (i.e. not equal to orig_FxDistort)");

            CopyEffectParams(orig_FxDistort, FxDistortScalable, "text", "map", "scale");
            GFX.FxDistort = FxDistortScalable;

            effectsSwapped = true;
        } else {
            if (GFX.FxDistort != FxDistortScalable)
                Logger.Warn("ZoomOutHelperPrototype", "GFX.FxDistort refers to an unexpected shader! (i.e. it is not equal to FxDistortScalable)");

            CopyEffectParams(FxDistortScalable, orig_FxDistort, "text", "map", "scale");
            GFX.FxDistort = orig_FxDistort;

            effectsSwapped = false;
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
            }
        }
    }

    #endregion

    #region Commands

    [Command($"fzo_get_camera_scale", "gets the FZO zoom out camera scale")]
    public static void CommandSetCameraScale() {
        if (!HooksActive) {
            Engine.Commands.Log($"FZO zoom out is not currently active.");
            return;
        }

        Engine.Commands.Log($"the current camera scale is {CameraScale:N2}x.");
    }

    [Command($"fzo_set_camera_scale", "sets the FZO zoom out camera scale")]
    public static void CommandSetCameraScale(float scale = 1f) {
        if (!HooksActive) {
            Engine.Commands.Log($"FZO zoom out is not active! make sure to place a Global Zoom Controller, or if you're using extended camera dynamics to use its own commands instead!");
            return;
        }

        if (scale <= 0f) {
            Engine.Commands.Log($"invalid scale! only scales above 0x are supported");
            return;
        }

        if (PixelPerfectZooming)
            scale = MathF.Round(scale * 160f) / 160f;
        var prevScale = CameraScale;
        CameraScale = scale;

        Engine.Commands.Log($"set the target camera scale to {scale:N2}x! previous was {prevScale:N2}x.");
    }

    [Command($"fzo_force_enable", "forcefully enables FZO zoom out, even if not intended by the current map")]
    public static void CommandForceEnable() {
        if (Engine.Scene is not Level) {
            Engine.Commands.Log("Cannot activate FZO zoom out while not in a Level!");
            return;
        }

        if (HooksActive) {
            Engine.Commands.Log($"FZO zoom out is already active!");
            return;
        }

        ResetStaticFields();
        UpdateMainHooks(true);
        Engine.Commands.Log($"enabled FZO zoom out! use the command `fzo_set_camera_scale` to adjust the camera scale");
    }

    [Command($"fzo_is_active", "checks if FZO zoom out is enabled")]
    public static void CommandIsActive() {
        if (HooksActive)
            Engine.Commands.Log("FZO zoom out is currently enabled!");
        else
            Engine.Commands.Log("FZO zoom out is currently disabled.");
    }

    #endregion
}
