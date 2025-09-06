using Mono.Cecil;

namespace Celeste.Mod.FunctionalZoomOut.Hooks;

internal static class BackdropHooks {
    private static Vector2 ApplyCenterFixOffset(Vector2 orig) {
        if (!Module.ZoomOutActive)
            return orig;

        return orig + Module.CenterFixOffset;
    }

    [ILHook(typeof(Parallax), nameof(Parallax.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    [ILHook(typeof(Parallax), nameof(Parallax.orig_Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Parallax_Render(ILContext il) {
        var cursor = new ILCursor(il);

        cursor.FixAllCameraDimensionsFloat();
        cursor.FixAllCameraDimensionsFloatHalf();

        cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld<Parallax>("CameraOffset"));
        cursor.EmitDelegate(ApplyCenterFixOffset);

        cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld<Backdrop>("Position"));
        cursor.EmitDelegate(ApplyCenterFixOffset);
    }

    // [ILHook(typeof(Parallax), nameof(Parallax.orig_Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    // private static void Parallax_Render(ILContext il) {
    //     var cursor = new ILCursor(il);

    //     cursor.FixAllCameraDimensionsFloat();
    //     cursor.FixAllCameraDimensionsFloatHalf();

    //     while (cursor.TryGotoNext(MoveType.After, i => i.MatchCallOrCallvirt<MTexture>("get_Width") || i.MatchCallOrCallvirt<MTexture>("get_Height"))) {
    //         cursor.EmitLdarg0();
    //         cursor.EmitDelegate((int i, Backdrop b) => (int)(i * Math.Max(1f, 1f + Module.CameraScale * (1F - b.Scroll.X))));
    //     }

    //     cursor.Index = -1;
    //     cursor.GotoPrev(MoveType.After, i => i.MatchLdcR4(1f));
    //     cursor.EmitLdarg0();
    //     cursor.EmitDelegate((float orig, Backdrop b) => Math.Max(1f, Module.CameraScale * (1f - b.Scroll.X)));
    // }

    [ILHook(typeof(Godrays), nameof(Godrays.Update), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Godrays_Update(ILContext il) {
        var cursor = new ILCursor(il);

        // resize vertex buffer
        // cursor.GotoNext(MoveType.AfterLabel, i => i.MatchRet());
        cursor.EmitLdarg0();
        cursor.EmitDelegate(resizeVertexBuffer);

        // fix parallax
        cursor.GotoNext(MoveType.After, i => i.MatchStloc(7));

        static float addCenterFixX(float x) => x + Module.CenterFixOffset.X;
        static float addCenterFixY(float y) => y + Module.CenterFixOffset.Y;

        cursor.GotoNext(MoveType.After, i => i.MatchCallOrCallvirt<Camera>("get_X"));
        cursor.EmitDelegate(addCenterFixX);
        cursor.GotoNext(MoveType.After, i => i.MatchSub());
        cursor.EmitDelegate(addCenterFixX);

        cursor.GotoNext(MoveType.After, i => i.MatchCallOrCallvirt<Camera>("get_Y"));
        cursor.EmitDelegate(addCenterFixY);
        cursor.GotoNext(MoveType.After, i => i.MatchSub());
        cursor.EmitDelegate(addCenterFixY);

        // makeshift for loop to repeat the godrays
        var loopXHeadLabel = cursor.DefineLabel();
        var loopXStartLabel = cursor.DefineLabel();
        var loopYHeadLabel = cursor.DefineLabel();
        var loopYStartLabel = cursor.DefineLabel();
        var loopX = new VariableDefinition(il.Import(typeof(int)));
        var loopY = new VariableDefinition(il.Import(typeof(int)));
        il.Body.Variables.Add(loopX);
        il.Body.Variables.Add(loopY);

        // loop start
        cursor.GotoNext(MoveType.After, i => i.MatchStloc(11));
        //
        cursor.EmitLdcI4(0);
        cursor.EmitStloc(loopY);
        cursor.EmitBr(loopYHeadLabel);
        cursor.MarkLabel(loopYStartLabel);

        cursor.EmitLdcI4(0);
        cursor.EmitStloc(loopX);
        cursor.EmitBr(loopXHeadLabel);
        cursor.MarkLabel(loopXStartLabel);

        // add loop variables
        cursor.GotoNext(MoveType.After, i => i.MatchConvI4());
        cursor.EmitLdloc(loopX);
        cursor.EmitAdd();
        cursor.GotoNext(MoveType.After, i => i.MatchConvI4());
        cursor.EmitLdloc(loopY);
        cursor.EmitAdd();

        // loop ends/heads
        cursor.Index = -1;
        cursor.GotoPrev(MoveType.After, i => i.MatchStelemAny(out _));

        // x end
        cursor.EmitLdloc(loopX);
        cursor.EmitLdcI4(320 + 64);
        cursor.EmitAdd();
        cursor.EmitStloc(loopX);
        // x head
        cursor.MarkLabel(loopXHeadLabel);
        cursor.EmitLdloc(loopX);
        cursor.EmitDelegate(loopXHead);
        cursor.EmitBrtrue(loopXStartLabel);

        // y end
        cursor.EmitLdloc(loopY);
        cursor.EmitLdcI4(180 + 64);
        cursor.EmitAdd();
        cursor.EmitStloc(loopY);
        // y head
        cursor.MarkLabel(loopYHeadLabel);
        cursor.EmitLdloc(loopY);
        cursor.EmitDelegate(loopYHead);
        cursor.EmitBrtrue(loopYStartLabel);

        static void resizeVertexBuffer(Godrays godrays) {
            int visibleScreens = (int)Math.Ceiling(Module.GetFixedCameraSize(Celeste.GameWidth) / 320f);
            visibleScreens *= visibleScreens;
            int expectedBufferLength = Godrays.RayCount * 6 * visibleScreens;
            if (godrays.vertices.Length != expectedBufferLength)
                godrays.vertices = new VertexPositionColor[expectedBufferLength];
        }

        static bool loopXHead(int x) {
            int visibleScreens = (int)Math.Ceiling(Module.GetFixedCameraSize(Celeste.GameWidth) / 320f);
            return x < 320 * visibleScreens + 32;
        }

        static bool loopYHead(int y) {
            int visibleScreens = (int)Math.Ceiling(Module.GetFixedCameraSize(Celeste.GameWidth) / 320f);
            return y < 180 * visibleScreens + 32;
        }
    }

    // Northern Lights

    [ILHook(typeof(NorthernLights), nameof(NorthernLights.BeforeRender), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void NorthernLights_BeforeRender(ILContext il) {
        var cursor = new ILCursor(il);

        // rescale the northern lights to match the camera scale (without making them seem to become higher resolution)

        cursor.FixNextCanvasWidthInt();
        cursor.FixNextCanvasHeightInt();
        cursor.GotoNext(MoveType.After, i => i.MatchStfld<NorthernLights>(nameof(NorthernLights.buffer))).MoveAfterLabels();
        cursor.EmitLdarg0();
        cursor.EmitDelegate(resizeBufferIfNeeded);

        static void resizeBufferIfNeeded(NorthernLights self) {
            Module.EnsureBufferDimensions(self.buffer);
        }

        // upscale the backdrop before drawing the particles
        cursor.Index = -1;
        cursor.GotoPrev(MoveType.Before, i => i.MatchCallOrCallvirt<SpriteBatch>(nameof(SpriteBatch.Begin)));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(upscaleBuffer);

        static void upscaleBuffer(NorthernLights self) {
            if (!Module.ZoomOutActive)
                return;

            Engine.Instance.GraphicsDevice.SetRenderTarget(self.buffer);

            Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            Draw.SpriteBatch.Draw(GameplayBuffers.TempB, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, Module.CameraScale, SpriteEffects.None, 0f);
            Draw.SpriteBatch.End();
        }

        // draw the northern lights to TempB rather than NorthernLights.buffer to prevent issues drawing a buffer to itself when upscaling
        cursor.GotoPrev(MoveType.After, i => i.MatchLdfld<NorthernLights>(nameof(NorthernLights.buffer))); // blur output
        cursor.EmitDelegate(swapBufferToTempB);
        cursor.Index -= 2;
        cursor.GotoPrev(MoveType.After, i => i.MatchLdfld<NorthernLights>(nameof(NorthernLights.buffer))); // blur input
        cursor.EmitDelegate(swapBufferToTempB);
        cursor.Index -= 2;
        cursor.GotoPrev(MoveType.After, i => i.MatchLdfld<NorthernLights>(nameof(NorthernLights.buffer))); // SetRenderTarget before drawing the vertices
        cursor.EmitDelegate(swapBufferToTempB);
        cursor.Index -= 2;

        static VirtualRenderTarget swapBufferToTempB(VirtualRenderTarget buffer) {
            if (Module.ZoomOutActive)
                return GameplayBuffers.TempB;

            return buffer;
        }

        // also apply looping to the particles
        Apply1x1ParticleLooping(il, true);
    }

    [ILHook(typeof(Snow), nameof(Snow.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    [ILHook(typeof(CoreStarsFG), nameof(CoreStarsFG.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    [ILHook(typeof(StardustFG), nameof(StardustFG.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Snow_CoreStarsFG_StardustFG_Render(ILContext il) => Apply1x1ParticleLooping(il, false);

    private static void Apply1x1ParticleLooping(ILContext il, bool hasParallax) {
        var cursor = new ILCursor(il);

        // regret
        var loopXHeadLabel = cursor.DefineLabel();
        var loopXStartLabel = cursor.DefineLabel();
        var loopYHeadLabel = cursor.DefineLabel();
        var loopYStartLabel = cursor.DefineLabel();
        var loopX = new VariableDefinition(il.Import(typeof(int)));
        var loopY = new VariableDefinition(il.Import(typeof(int)));
        il.Body.Variables.Add(loopX);
        il.Body.Variables.Add(loopY);

        // jump to where the loop should start (right after `Camera camera = (scene as Level).Camera;`)
        cursor.GotoNextBestFit(MoveType.After, i => i.MatchLdfld<Level>(nameof(Level.Camera)), i => i.MatchStloc(out _));
        // y start
        cursor.EmitLdcI4(0);
        cursor.EmitStloc(loopY);
        cursor.EmitBr(loopYHeadLabel);
        cursor.MarkLabel(loopYStartLabel);
        // x start
        cursor.EmitLdcI4(0);
        cursor.EmitStloc(loopX);
        cursor.EmitBr(loopXHeadLabel);
        cursor.MarkLabel(loopXStartLabel);

        // add loop variables and center fix offset (if needed)
        static float addCenterFixX(float x) => x + Module.CenterFixOffset.X;
        static float addCenterFixY(float y) => y + Module.CenterFixOffset.Y;

        if (hasParallax) {
            cursor.GotoNext(MoveType.After, i => i.MatchCallOrCallvirt<Camera>("get_X"));
            cursor.EmitDelegate(addCenterFixX);
            cursor.GotoNext(MoveType.After, i => i.MatchSub());
            cursor.EmitDelegate(addCenterFixX);
        }
        cursor.GotoNext(MoveType.After, i => i.OpCode == OpCodes.Callvirt && (i.Operand as MethodReference).Name == "mod");
        cursor.EmitLdloc(loopX);
        cursor.EmitConvR4();
        cursor.EmitAdd();

        if (hasParallax) {
            cursor.GotoNext(MoveType.After, i => i.MatchCallOrCallvirt<Camera>("get_Y"));
            cursor.EmitDelegate(addCenterFixY);
            cursor.GotoNext(MoveType.After, i => i.MatchSub());
            cursor.EmitDelegate(addCenterFixY);
        }
        cursor.GotoNext(MoveType.After, i => i.OpCode == OpCodes.Callvirt && (i.Operand as MethodReference).Name == "mod");
        cursor.EmitLdloc(loopY);
        cursor.EmitConvR4();
        cursor.EmitAdd();


        // jump to where the loop ends/heads should go
        cursor.GotoNext(MoveType.After, i => i.MatchBlt(out _));

        // x end
        cursor.EmitLdloc(loopX);
        cursor.EmitLdcI4(320);
        cursor.EmitAdd();
        cursor.EmitStloc(loopX);
        // x head
        cursor.MarkLabel(loopXHeadLabel);
        cursor.EmitLdloc(loopX);
        cursor.EmitDelegate(loopXHead);
        cursor.EmitBrtrue(loopXStartLabel);

        // y end
        cursor.EmitLdloc(loopY);
        cursor.EmitLdcI4(180);
        cursor.EmitAdd();
        cursor.EmitStloc(loopY);
        // y head
        cursor.MarkLabel(loopYHeadLabel);
        cursor.EmitLdloc(loopY);
        cursor.EmitDelegate(loopYHead);
        cursor.EmitBrtrue(loopYStartLabel);

        static bool loopXHead(int x) {
            int visibleScreens = (int)Math.Ceiling(Module.GetFixedCameraSize(Celeste.GameWidth) / 320f);
            return x < 320 * visibleScreens;
        }

        static bool loopYHead(int y) {
            int visibleScreens = (int)Math.Ceiling(Module.GetFixedCameraSize(Celeste.GameWidth) / 320f);
            return y < 180 * visibleScreens;
        }
    }

    // TODO:
    // - Wind Snow
    // - Blackhole
    // - Final Boss BG
    // - Planets
    // - Starfield
    // - Old SIte Stars
}