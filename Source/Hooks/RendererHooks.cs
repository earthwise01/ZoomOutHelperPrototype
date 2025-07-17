namespace Celeste.Mod.FunctionalZoomOut.Hooks;

internal static class RendererHooks {
    // no safety checks yet bc im lazy so if it explodes it explodes, but it'd explode if it didn't work anyway so !
    [ILHook(typeof(Level), nameof(Level.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Level_Render(ILContext il) {
        var cursor = new ILCursor(il);

        // maybe not the best place for this..? these are also set before Level.Update in UpdateLevelZoom, but juust in case Level.Zoom is changed after that    umm i guess this is here
        // seems to at least work for the case of a badeline boost when zoom out is active not changing the zoom, and when zoom out is inactive working like normal
        cursor.EmitLdarg0();
        cursor.EmitDelegate(makeSureCameraScaleIsCopiedToLevelZoom);

        static void makeSureCameraScaleIsCopiedToLevelZoom(Level level) {
            if (Module.CameraScale == 1f)
                return;

            level.Zoom = level.ZoomTarget = 1f / Module.CameraScale;
            level.ZoomFocusPoint = new Vector2(level.Camera.Viewport.Width / 2f, level.Camera.Viewport.Height / 2f);
        }

        // patch the size of the rectangle used for screen flashes
        // - Draw.Rect(-1f, -1f, 322f, 182f, flashColor * flash);
        // + Draw.Rect(-1f, -1f, GetFixedCameraSizePadded(322f, 2), GetFixedCameraSizePadded(182f, 2), flashColor * flash);
        cursor.GotoNext(instr => instr.MatchLdfld<Level>(nameof(Level.flash)));
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(322f));
        cursor.EmitFixCameraSizeFloatPadded(2);
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(182f));
        cursor.EmitFixCameraSizeFloatPadded(2);

        // jump to where rendering to the screen starts
        // cursor.GotoNextBestFit(MoveType.Before, instr => instr.MatchLdnull(), instr => instr.MatchCallOrCallvirt<GraphicsDevice>("SetRenderTarget"));
        cursor.GotoNext(instr => instr.MatchCallOrCallvirt<Matrix>(nameof(Matrix.CreateScale)));

        // apply the scale (might also work but unused)
        // - Matrix matrix = Matrix.CreateScale(6f) * Engine.ScreenMatrix;
        // + Matrix matrix = Matrix.CreateScale(6f / CameraScale) * Engine.ScreenMatrix;
        // cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(Celeste.TargetWidth / Celeste.GameWidth));
        // cursor.EmitDelegate(applyScale);

        // fix the screen center position for the vanilla zoomTarget
        // i dont even know if this works at all but sure why not ig
        // - Vector2 vector = new Vector2(320f, 180f);
        // + Vector2 vector = new Vector2(GetFixedCameraSize(320f), GetFixedCameraSize(180f));
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(320f));
        // cursor.EmitFixCameraSizeFloat();
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(180f));
        // cursor.EmitFixCameraSizeFloat();

        // grab the scale local to jump to later
        int scaleLocal = -1;
        cursor.GotoNext(instr => instr.MatchLdfld<Level>("ZoomTarget"));
        cursor.GotoNext(instr => instr.MatchLdfld<Level>("Zoom"));
        cursor.GotoNext(instr => instr.MatchStloc(out scaleLocal));

        // grab the padding local
        int paddingLocal = -1;
        cursor.GotoNext(instr => instr.MatchLdloca(out paddingLocal),
            instr => instr.MatchLdarg(out _),
            instr => instr.MatchLdfld<Level>("ScreenPadding"));

        // no mirror mode support yet,
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdfld<Assists>(nameof(Assists.MirrorMode)));
        // Console.WriteLine(il);
        // cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(160f));
        // cursor.EmitDelegate(first160);
        // cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(160f));
        // cursor.EmitDelegate(second160);

        // jump to when rendering the buffer to the screen
        cursor.GotoNext(instr => instr.MatchCallOrCallvirt<SpriteBatch>(nameof(SpriteBatch.Begin)));

        // cursor.GotoNext(MoveType.Before, instr => instr.MatchLdsfld(typeof(GameplayBuffers), nameof(GameplayBuffers.Level)), instr => instr.MatchCallOrCallvirt<VirtualRenderTarget>("get_Bounds"));
        // cursor.EmitDelegate((Vector2 orig) => {
        //     var cameraW = 320 * Module.CameraScale;
        //     var cameraH = 180 * Module.CameraScale;
        //     return new Vector2(MathF.Ceiling(cameraW) - cameraW, MathF.Ceiling(cameraH) - cameraH) * -0.5f;
        // });

        // apply the scale
        // - Draw.SpriteBatch.Draw(... scale ...);
        // + Draw.SpriteBatch.Draw(... scale / CurrentCameraScale ...);
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdloc(scaleLocal));
        // cursor.EmitDelegate(applyScale);

        // draw black bars around the edges, since otherwise watchtowers can let stuff offscreen leak in
        //   Draw.SpriteBatch.End();
        // + drawBlackBars(vector4, scale);
        cursor.GotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<SpriteBatch>(nameof(SpriteBatch.End)));
        cursor.EmitLdloc(paddingLocal);
        cursor.EmitDelegate(drawBlackBars);

        // vector3.X = 160f* - (vector3.X - 160f);
        // static float first160(float orig) {
        //     return 1000; //-0.0f * ((320 * FunctionalZoomOutModule.CanvasScale) - (320 * FunctionalZoomOutModule.CameraScale));
        // }

        // // vector3.X = 160f - (vector3.X - 160f*);
        // static float second160(float orig) {
        //     return 0;
        // }

        // static float applyScale(float orig) {
        //     if (!Module.ZoomOutActive)
        //         return orig;

        //     return orig / Module.CameraScale;
        // }

        static void drawBlackBars(Vector2 padding) {
            if (!Module.ZoomOutActive || padding == Vector2.Zero)
                return;

            var scale = (320f - padding.X * 2f) / 320f;
            // mirror mode
            // padding.X = MathF.Abs(padding.X);
            // padding.Y = MathF.Abs(padding.Y);

            // draws black bars around the edges because otherwise watchtower padding zoomout etc would reveal stuff offscreen
            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Engine.ScreenMatrix);

            Draw.Rect(-2f, -2f, 1924f, padding.Y * 6f + 2f, Color.Black);
            Draw.Rect(-2f, padding.Y * 6f + 1080f * scale, 1924f, padding.Y * 6f + 2f, Color.Black);
            Draw.Rect(-2f, -2f, padding.X * 6f + 2f, 1084f, Color.Black);
            Draw.Rect(padding.X * 6f + 1920f * scale, -2f, padding.X * 6f + 2f, 1084f, Color.Black);

            Draw.SpriteBatch.End();
        }
    }

    [ILHook(typeof(BloomRenderer), nameof(BloomRenderer.Apply), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void BloomRenderer_Apply(ILContext il) {
        var cursor = new ILCursor(il);
        cursor.FixAllCameraDimensionsFloatPadded(20);
    }

    [ILHook(typeof(LightingRenderer), nameof(LightingRenderer.BeforeRender), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void LightingRenderer_BeforeRender(ILContext il) {
        var cursor = new ILCursor(il);
        cursor.FixAllCameraDimensionsFloat();
    }
}
