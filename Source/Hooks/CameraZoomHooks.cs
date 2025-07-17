namespace Celeste.Mod.FunctionalZoomOut.Hooks;

internal static class CameraZoomHooks {

    #region Camera Size

    [ILHook(typeof(Player), "get_" + nameof(Player.CameraTarget), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Player_get_CameraTarget(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloat();
        cursor.FixAllCameraDimensionsInt();
        cursor.FixAllCameraDimensionsFloatHalf();

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
        if (!Module.ZoomOutActive)
            return orig(self);

        Vector2 origCameraAnchor = self.CameraAnchor;
        self.CameraAnchor -= Module.CenterFixOffset;
        var result = orig(self);
        self.CameraAnchor = origCameraAnchor;
        return result;
    }


    [ILHook(typeof(Level), nameof(Level.EnforceBounds), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Level_EnforceBounds(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsInt();
    }

    [ILHook(typeof(Level), nameof(Level.ScreenToWorld), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    [ILHook(typeof(Level), nameof(Level.WorldToScreen), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Level_ScreenToWorld_WorldToScreen(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloat();

        cursor.GotoNext(MoveType.After, instr => instr.MatchLdcR4(6f));
        cursor.EmitFixHDUpscale();
    }

    [ILHook(typeof(Audio), nameof(Audio.Position), BindingFlags.Public | BindingFlags.Static, tag: "mainZoomHooks")]
    private static void Audio_Position(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloat();

        cursor.GotoNext(MoveType.Before, instr => instr.MatchStfld<FMOD.VECTOR>("x"));
        cursor.EmitDelegate(audioEdgeFadeFix);
        cursor.GotoNext(MoveType.Before, instr => instr.MatchStfld<FMOD.VECTOR>("y"));
        cursor.EmitDelegate(audioEdgeFadeFix);

        // different for how excameradynamics does it but more reliable i find for extremely large camera scales
        static float audioEdgeFadeFix(float orig) {
            if (!Module.ZoomOutActive)
                return orig;

            return orig / Module.CameraScale;
        }
    }

    // i shd probably handle more case by case stuff here but i   literally cannot be bothered atm catplush
    [ILHook(typeof(Lookout), nameof(Lookout.LookRoutine), BindingFlags.NonPublic | BindingFlags.Instance, HookTypes.IEnumerator, tag: "mainZoomHooks")]
    private static void Lookout_LookRoutine(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloatHalf();
        cursor.FixAllCameraDimensionsFloat();
        cursor.FixAllCameraDimensionsInt();
    }

    [ILHook(typeof(Lookout.Hud), nameof(Lookout.Hud.Update), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Lookout_Hud_Update(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsInt();
    }

    [ILHook(typeof(DustEdges), nameof(DustEdges.BeforeRender), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void DustEdges_BeforeRender(ILContext il) {
        ILCursor cursor = new(il);

        cursor.FixAllCameraDimensionsFloat();

        if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(1f / 320f))) {
            Logger.Verbose("ZoomOutHelperPrototype", $"patching pixel width value at {cursor.Index} in cil for {cursor.Method.Name}...");
            cursor.EmitDelegate(fixPixelSize);
        }

        if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchLdcR4(1f / 180f))) {
            Logger.Verbose("ZoomOutHelperPrototype", $"patching pixel height value at {cursor.Index} in cil for {cursor.Method.Name}...");
            cursor.EmitDelegate(fixPixelSize);
        }

        static float fixPixelSize(float orig) {
            if (Module.CanvasScale == 1)
                return orig;

            return orig / Module.CanvasScale;
        }
    }

    #endregion

    #region HUD

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

    #region Camera Culling

    [ILHook(typeof(Level), nameof(Level.IsInCamera), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Level_IsInCamera(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsInt();
    }

    [ILHook(typeof(CrystalStaticSpinner), nameof(CrystalStaticSpinner.InView), BindingFlags.NonPublic | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void CrystalStaticSpinner_InView(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloat();
    }

    [ILHook(typeof(Lightning), nameof(Lightning.InView), BindingFlags.NonPublic | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Lightning_InView(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloat();
    }

    [ILHook(typeof(LightningRenderer), nameof(LightningRenderer.OnRenderBloom), BindingFlags.NonPublic | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void LightningRenderer_OnRenderBloom(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloat();
    }

    [ILHook(typeof(FormationBackdrop), nameof(FormationBackdrop.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void FormationBackdrop_Render(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloatPadded(2);
    }

    #endregion

    #region Parallax Entities

    [ILHook(typeof(Decal), nameof(Decal.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void Decal_Render(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloatHalf();
    }

    [ILHook(typeof(BigWaterfall), nameof(BigWaterfall.RenderPosition), BindingFlags.NonPublic | BindingFlags.Instance, HookTypes.GetProperty, tag: "mainZoomHooks")]
    private static void BigWaterfall_RenderPosition(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloatHalf();
    }

    [ILHook(typeof(BigWaterfall), nameof(BigWaterfall.Render), BindingFlags.Public | BindingFlags.Instance, tag: "mainZoomHooks")]
    private static void BigWaterfall_Render(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloat();
    }

    [ILHook(typeof(SummitCloud), nameof(SummitCloud.RenderPosition), BindingFlags.NonPublic | BindingFlags.Instance, HookTypes.GetProperty, tag: "mainZoomHooks")]
    private static void SummitCloud_RenderPosition(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixAllCameraDimensionsFloatHalf();
    }

    #endregion
}