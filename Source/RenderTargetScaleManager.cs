
namespace Celeste.Mod.FunctionalZoomOut;

// legacy but need to keep around until stylemask helper gets native zoomout support
internal static class RenderTargetScaleManager {
    private class ScalableVRTWrapper {
        public VirtualRenderTarget RenderTarget { get; private set; }
        public readonly int BaseWidth, BaseHeight;
        public readonly int Padding;

        private float _scale = 1f;
        public float Scale {
            get => Scale;
            set {
                if (_scale == value || IsDisposed)
                    return;

                _scale = value;
                int width = (int)MathF.Ceiling(BaseWidth * _scale) + Padding;
                int height = (int)MathF.Ceiling(BaseHeight * _scale) + Padding;

                RenderTarget.Width = width;
                RenderTarget.Height = height;
                RenderTarget.Reload();

                // Logger.Verbose("ZoomOutHelperPrototype", $"resized render target {_scale:N2}x to {width}x{height} (base is {BaseWidth + Padding}x{BaseHeight + Padding})");
            }
        }

        public ScalableVRTWrapper(VirtualRenderTarget target, int padding = 0) {
            RenderTarget = target;
            Padding = padding;
            BaseWidth = target.Width - Padding;
            BaseHeight = target.Height - Padding;
        }

        public bool IsDisposed => RenderTarget is null || RenderTarget.IsDisposed;

        public void Dispose() {
            Scale = 1f; // attempt to reset the scale before detatching from the render target
            RenderTarget = null;
        }
    }

    private static readonly HashSet<ScalableVRTWrapper> Tracked = [];

    public static void Track(VirtualRenderTarget vrt, int padding = 0) {
        var scalableTarget = Tracked.FirstOrDefault(trackedTarget => trackedTarget.RenderTarget == vrt);
        if (scalableTarget is not null) {
            Logger.Warn("ZoomOutHelperPrototype", "can't track an already tracked render target!");
            return;
        }

        scalableTarget = new ScalableVRTWrapper(vrt, padding);
        Tracked.Add(scalableTarget);

        scalableTarget.Scale = Module.CanvasScale;
    }

    public static void Track(params VirtualRenderTarget[] vrts) {
        foreach (var vrt in vrts) {
            Track(vrt);
        }
    }

    private static void Untrack(ScalableVRTWrapper scalableTarget) {
        if (!Tracked.Contains(scalableTarget)) {
            Logger.Verbose("ZoomOutHelperPrototype", "trying to untrack a render target that isn't tracked!");
            return;
        }

        scalableTarget.Dispose();
        Tracked.Remove(scalableTarget);
        Logger.Verbose("ZoomOutHelperPrototype", "untracked rt!");
    }

    public static void Untrack(VirtualRenderTarget vrt) {
        var scalableTarget = Tracked.FirstOrDefault(trackedTarget => trackedTarget.RenderTarget == vrt);
        if (scalableTarget is null) {
            Logger.Verbose("ZoomOutHelperPrototype", "trying to untrack a render target that isn't tracked!");
            return;
        }

        Untrack(scalableTarget);
    }

    internal static void UntrackAll() {
        foreach (var trackedTarget in Tracked) {
            Untrack(trackedTarget);
        }

        Tracked.Clear();
        // Module.SwapVanillaEffects(false);
    }

    internal static void Update() {
        foreach (var scalableTarget in Tracked) {
            // clean up any targets that got disposed
            if (scalableTarget.IsDisposed) {
                Untrack(scalableTarget);
                continue;
            }

            // update the scale
            scalableTarget.Scale = Module.CanvasScale;
        }

        // // maybe not the best place for this but it works
        // if (Module.CanvasScale != 1) {
        //     Module.SwapVanillaEffects(true);
        // } else {
        //     Module.SwapVanillaEffects(false);
        // }

        // Module.FxDistortScalable.Parameters["scale"].SetValue(Module.CanvasScale);
    }

#if DEBUG
    [Command($"zoomout_tracked_vrt_count", "shows how many render targets are being tracked by the render target scale manager")]
    internal static void ShowCountCommand() {
        Engine.Commands.Log($"there are currently {Tracked.Count} tracked render targets");
    }
#endif

}
