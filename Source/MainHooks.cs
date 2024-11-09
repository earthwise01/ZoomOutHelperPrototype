using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.Entities;
using MonoMod;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Mono.Cecil.Cil;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Utils;
using System.Runtime.InteropServices;
using Celeste.Mod.FunctionalZoomOut.Utils;

namespace Celeste.Mod.FunctionalZoomOut;

public static class MainHooks {

    [HookLoadCallback("mainZoomHooks")]
    internal static void Load() {

    }

    [HookUnloadCallback("mainZoomHooks")]
    internal static void Unload() {

    }

    #region Level.Render + Bloom and Lighting

    // no safety checks yet bc im lazy so if it explodes it explodes, but it'd explode if it didn't work anyway so !
    [ILHook(typeof(Level), nameof(Level.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Level_Render(ILContext il) {
        ILCursor cursor = new(il);

        // patch the size of the rectangle used for screen flashes
        // - Draw.Rect(-1f, -1f, 322f, 182f, flashColor * flash);
        // + Draw.Rect(-1f, -1f, GetFixedCameraSizePadded(322f, 2), GetFixedCameraSizePadded(182f, 2), flashColor * flash);
        cursor.GotoNext(instr => instr.MatchLdfld<Level>(nameof(Level.flash)));
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(322f));
        cursor.EmitFixCameraSizeFloatPadded(2);
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(182f));
        cursor.EmitFixCameraSizeFloatPadded(2);

        // jump to where rendering to the screen starts
        cursor.GotoNext(instr => instr.MatchLdnull(), instr => instr.MatchCallOrCallvirt<GraphicsDevice>("SetRenderTarget"));

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
        cursor.EmitFixCameraSizeFloat();
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(180f));
        cursor.EmitFixCameraSizeFloat();

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

        // jump to when rendering the buffer to the screen
        cursor.GotoNext(instr => instr.MatchCallOrCallvirt<SpriteBatch>(nameof(SpriteBatch.Begin)));

        // apply the scale
        // - Draw.SpriteBatch.Draw(... scale ...);
        // + Draw.SpriteBatch.Draw(... scale / CurrentCameraScale ...);
        cursor.GotoNext(MoveType.After, instr => instr.MatchLdloc(scaleLocal));
        cursor.EmitDelegate(applyScale);

        // draw black bars around the edges, since otherwise watchtowers can let stuff offscreen leak in
        //   Draw.SpriteBatch.End();
        // + drawBlackBars(vector4, scale);
        cursor.GotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<SpriteBatch>(nameof(SpriteBatch.End)));
        cursor.EmitLdloc(paddingLocal);
        cursor.EmitLdloc(scaleLocal);
        cursor.EmitDelegate(drawBlackBars);

        static float applyScale(float orig) {
            if (!FunctionalZoomOutModule.ZoomOutActive)
                return orig;

            return orig / FunctionalZoomOutModule.CameraScale;
        }

        static void drawBlackBars(Vector2 padding, float scale) {
            if (!FunctionalZoomOutModule.ZoomOutActive || padding == Vector2.Zero)
                return;

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
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloatPadded(20);
    }

    [ILHook(typeof(LightingRenderer), nameof(LightingRenderer.BeforeRender), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void LightingRenderer_BeforeRender(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloat();
    }

    #endregion

    #region Camera Size

    [ILHook(typeof(Player), "get_" + nameof(Player.CameraTarget), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Player_get_CameraTarget(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloat();
        cursor.FixCameraDimensionsInt();
        cursor.FixCameraDimensionsFloatHalf();

        // failed attempt at fixing min height rooms
        // cursor.GotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<Level>("get_Bounds"));
        // cursor.GotoNext(MoveType.After, instr => instr.MatchCall<Rectangle>("get_Bottom"));
        // cursor.EmitLdarg0();
        // cursor.EmitDelegate(fixBoundsCheckForMinHeightRooms);

        // static int fixBoundsCheckForMinHeightRooms(int roomBottom, Player player) {
        //     if (!FunctionalZoomOutModule.ZoomOutActive)
        //         return roomBottom;

        //     var level = player.level;
        //     var roomHeight = level.Bounds.Height;
        //     var cameraHeight = API.CameraHeight;

        //     int offsetFromBottom = 0;
        //     if (MathF.Ceiling(cameraHeight / 8f) == MathF.Ceiling(roomHeight / 8f))
        //         offsetFromBottom = (int)cameraHeight - roomHeight;

        //     Console.WriteLine($"{roomHeight}, {cameraHeight}, {offsetFromBottom}");
        //     return roomBottom + offsetFromBottom;
        // }
    }

    // messyy and shd be an il hook probly but works for now, fixes camera target triggers
    private delegate Vector2 orig_Player_get_CameraTarget(Player self);
    [OnHook(typeof(Player), "get_" + nameof(Player.CameraTarget), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static Vector2 On_Player_get_CameraTarget(orig_Player_get_CameraTarget orig, Player self) {
        if (!FunctionalZoomOutModule.ZoomOutActive)
            return orig(self);

        Vector2 origCameraAnchor = self.CameraAnchor;
        self.CameraAnchor -= Util.CenterFixOffset;
        var result = orig(self);
        self.CameraAnchor = origCameraAnchor;
        return result;
    }


    [ILHook(typeof(Level), nameof(Level.EnforceBounds), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Level_EnforceBounds(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsInt();
    }

    [ILHook(typeof(Level), nameof(Level.IsInCamera), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Level_IsInCamera(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsInt();
    }

    [ILHook(typeof(Level), nameof(Level.ScreenToWorld), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    [ILHook(typeof(Level), nameof(Level.WorldToScreen), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Level_ScreenToWorld_WorldToScreen(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloat();

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(6f));
        cursor.EmitFixHDUpscale();
    }

    [ILHook(typeof(FormationBackdrop), nameof(FormationBackdrop.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void FormationBackdrop_Render(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloatPadded(2);
    }

    [ILHook(typeof(Audio), nameof(Audio.Position), BindingFlags.Public | BindingFlags.Static, tag: "mainZoomHooks")]
    private static void Audio_Position(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloat();

        cursor.GotoNext(MoveType.Before, instr => instr.MatchStfld<FMOD.VECTOR>("z"));
        cursor.EmitDelegate(stolenAudioEdgeFadeFix);

        // https://github.com/Ikersfletch/ExCameraDynamics/blob/8baf1291f3f81bf99a07df3f8a56c6c42de47534/Code/Hooks/CameraZoomHooks.cs#L377
        // i really really want to avoid stealing stuff from excameradynamics typically but its probably for the best to have 100% parity for stuff like this
        static float stolenAudioEdgeFadeFix(float orig) {
            if (!FunctionalZoomOutModule.ZoomOutActive)
                return orig;

            return -MathF.Log(1f / FunctionalZoomOutModule.CameraScale);
        }
    }

    // i shd probably handle more case by case stuff here but i   literally cannot be bothered atm catplush
    [ILHook(typeof(Lookout), nameof(Lookout.LookRoutine), BindingFlags.NonPublic | BindingFlags.Instance, HookTypes.IEnumerator, tag: "mainZoomHooks")]
    private static void Lookout_LookRoutine(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloatHalf();
        cursor.FixCameraDimensionsFloat();
        cursor.FixCameraDimensionsInt();
    }

    [ILHook(typeof(Lookout.Hud), nameof(Lookout.Hud.Update), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Lookout_Hud_Update(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsInt();
    }

    [ILHook(typeof(CrystalStaticSpinner), nameof(CrystalStaticSpinner.InView), BindingFlags.NonPublic | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void CrystalStaticSpinner_InView(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloat();
    }

    [ILHook(typeof(DustEdges), nameof(DustEdges.BeforeRender), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void DustEdges_BeforeRender(ILContext il) {
        ILCursor cursor = new(il);

        cursor.FixCameraDimensionsFloat();

        if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(1f / 320f))) {
            Logger.Verbose("ZoomOutHelperPrototype", $"patching pixel width value at {cursor.Index} in cil for {cursor.Method.Name}...");
            cursor.EmitDelegate(fixPixelSize);
        }

        if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(1f / 180f))) {
            Logger.Verbose("ZoomOutHelperPrototype", $"patching pixel height value at {cursor.Index} in cil for {cursor.Method.Name}...");
            cursor.EmitDelegate(fixPixelSize);
        }

        static float fixPixelSize(float orig) {
            if (FunctionalZoomOutModule.CanvasScale == 1)
                return orig;

            return orig / FunctionalZoomOutModule.CanvasScale;
        }
    }

    [ILHook(typeof(Lightning), nameof(Lightning.InView), BindingFlags.NonPublic | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Lightning_InView(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloat();
    }

    [ILHook(typeof(LightningRenderer), nameof(LightningRenderer.OnRenderBloom), BindingFlags.NonPublic | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void LightningRenderer_OnRenderBloom(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloat();
    }

    #endregion

    #region HUD Scale/Position

    [ILHook(typeof(TalkComponent.TalkComponentUI), nameof(TalkComponent.TalkComponentUI.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void TalkComponentUI_Render(ILContext il) {
        ILCursor cursor = new(il);

        // 320 used for mirror mode
        if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(Celeste.GameWidth)))
            return;
        cursor.EmitFixCameraSizeFloat();

        // 6s used for upscaling
        for (int i = 0; i < 2; i++) {
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(Celeste.TargetWidth / Celeste.GameWidth)))
                return;
            cursor.EmitFixHDUpscale();
        }

        // floaty sine wave animation
        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchAdd(), instr => instr.MatchStindR4()))
            return;
        cursor.EmitFixHDUpscale();

        // scale
        for (int i = 0; i < 2; i++) {
            if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStloc(3)))
                return;
            cursor.EmitFixHDUpscale();
            cursor.Index++; // need to do this since otherwise it decides to apply the zoom twice to one of the stloc3s asdlkasjd
        }
    }

    // bwehhh the thingg doesnt actually scale with the camera but imm too lazy rnn its such a big method and there isnt like just. one specific variable id have to change aaa
    [ILHook(typeof(BirdTutorialGui), nameof(BirdTutorialGui.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void BirdTutorialGui_Render(ILContext il) {
        ILCursor cursor = new(il);

        // 320 used for mirror mode
        if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(Celeste.GameWidth)))
            return;
        cursor.EmitFixCameraSizeFloat();

        // 6s used for upscaling the initial position
        for (int i = 0; i < 2; i++) {
            if (!cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(Celeste.TargetWidth / Celeste.GameWidth)))
                return;
            cursor.EmitFixHDUpscale();
        }
    }

    // core text

    //

    [ILHook(typeof(SpotlightWipe), nameof(SpotlightWipe.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void SpotlightWipe_Render(ILContext il) {
        ILCursor cursor = new(il);

        while (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(6f))) {
            cursor.EmitFixHDUpscale();
        }
    }

    #endregion

    #region backdrops.

    // parallax still kinda Sucks in general but this is the best that i can do without scaling the images which i imagine wouldnt look too great
    [ILHook(typeof(Parallax), nameof(Parallax.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    [ILHook(typeof(Parallax), nameof(Parallax.orig_Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Parallax_Render(ILContext il) {
        ILCursor cursor = new(il);

        cursor.FixCameraDimensionsFloat();
        cursor.FixCameraDimensionsFloatHalf();

        cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld<Parallax>("CameraOffset"));
        cursor.EmitDelegate(offsetToCenter);

        cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdfld<Backdrop>("Position"));
        cursor.EmitDelegate(offsetToCenter);

        static Vector2 offsetToCenter(Vector2 orig) {
            if (!FunctionalZoomOutModule.ZoomOutActive)
                return orig;

            return orig + Util.CenterFixOffset;
        }
    }

    // [ILHook(typeof(Godrays), nameof(Godrays.Update), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    // private static void Godrays_Update(ILContext il) {
    //     ILCursor cursor = new(il);
    //     cursor.FixCameraDimensionsFloatPadded(64);
    // }

    // [ILHook(typeof(Godrays.Ray), nameof(Godrays.Ray.Reset), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    // private static void Godrays_Ray_Reset(ILContext il) {
    //     ILCursor cursor = new(il);
    //     cursor.FixCameraDimensionsFloatPadded(64);
    // }

    #endregion

    #region parallax entities

    [ILHook(typeof(Decal), nameof(Decal.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Decal_Render(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloatHalf();
    }

    [ILHook(typeof(BigWaterfall), nameof(BigWaterfall.RenderPosition), BindingFlags.NonPublic | BindingFlags.Instance, HookTypes.GetProperty, tag: "mainZoomHooks")]
    private static void BigWaterfall_RenderPosition(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloatHalf();
    }

    [ILHook(typeof(BigWaterfall), nameof(BigWaterfall.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void BigWaterfall_Render(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloat();
    }

    [ILHook(typeof(SummitCloud), nameof(SummitCloud.RenderPosition), BindingFlags.NonPublic | BindingFlags.Instance, HookTypes.GetProperty, tag: "mainZoomHooks")]
    private static void SummitCloud_RenderPosition(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloatHalf();
    }

    #endregion

}
