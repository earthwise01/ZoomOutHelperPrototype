using MonoMod.ModInterop;

namespace Celeste.Mod.FunctionalZoomOut.Interop;

[ModImportName("MotionSmoothing")]
public static class MotionSmoothingImports
{
    public static Func<VirtualRenderTarget, VirtualRenderTarget> GetResizableBuffer;

	public static Action ReloadLargeTextures;
}
