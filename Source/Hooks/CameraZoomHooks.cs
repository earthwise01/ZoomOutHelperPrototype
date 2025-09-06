using System.Data;
using Celeste.Mod.FunctionalZoomOut.Triggers;
using Mono.Cecil;

namespace Celeste.Mod.FunctionalZoomOut.Hooks;

internal static class CameraZoomHooks {

    [HookLoadCallback("mainZoomHooks")]
    internal static void LoadEvents() {
        Everest.Events.Level.OnLoadLevel += Event_LoadLevel;
    }

    [HookUnloadCallback("mainZoomHooks")]
    internal static void UnloadEvents() {
        Everest.Events.Level.OnLoadLevel -= Event_LoadLevel;
    }

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

    #region Transition Routine

    // forgot what this was supposed to be for (room zoom controller?)
    private static void Event_LoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        if (playerIntro == Player.IntroTypes.Transition)
            return;
    }

    // could definitely be better but works for now i suppose?
    // design details that still need to be figured out:
    // should room/initial zoom be treated differently to trigger set zoom? since right now camera scale persists between rooms, which makes sense when thinking of it like bloom where you control it with triggers but not so much a system where you place a controller in a room to give it a non-default zoom
    //
    [ILHook(typeof(Level), "orig_TransitionRoutine", BindingFlags.NonPublic | BindingFlags.Instance, HookTypes.IEnumerator, tag: "mainZoomHooks")]
    private static void Level_orig_TransitionRoutine(ILContext il) {
        var cursor = new ILCursor(il);

        // before: Vector2 cameraTo = GetFullCameraTargetAt(player, playerTo);
        cursor.GotoNext(MoveType.After, i => i.MatchCallOrCallvirt<Level>(nameof(Level.GetFullCameraTargetAt)));
        cursor.GotoPrev(MoveType.Before, i => i.MatchLdarg0());

        // get player and playerTo
        FieldReference playerField = null;
        FieldReference playerToField = null;
        cursor.FindNext(out _, i => i.MatchLdfld(out playerToField), i => i.MatchLdfld(out playerField));

        cursor.EmitLdarg0();
        cursor.EmitLdarg0();
        cursor.EmitLdfld(playerField);
        cursor.EmitLdarg0();
        cursor.EmitLdfld(playerToField);
        cursor.EmitDelegate(prepareCameraScales);

        static void prepareCameraScales(object ienumeratorObject, Player player, Vector2 playerTo) {
            var selfData = DynamicData.For(ienumeratorObject);
            var cameraScaleFrom = Module.CameraScale;
            var cameraScaleTo = getCameraScaleForRoom((player.Scene as Level).Session) ?? cameraScaleFrom;
            selfData.Set("FZO_cameraScaleFrom", cameraScaleFrom);
            selfData.Set("FZO_cameraScaleTo", cameraScaleTo);

            // temporarily override the current camera scale so that cameraTo ends up accounting for the new camera scale
            Module.CurrentCameraScale = cameraScaleTo;

            static float? getCameraScaleForRoom(Session session) {
                var id = session.MapData.Area.ID;
                var mode = session.MapData.Area.Mode;
                var levelName = session.Level;

                if (FunctionalZoomOutMapDataProcessor.RoomZoomControllerValues.TryGetValue((id, mode), out var mapRoomZooms) && mapRoomZooms.TryGetValue(levelName, out var roomZoom))
                    return roomZoom;

                return null;
            }
        }

        FieldReference cameraToField = null;
        cursor.GotoNext(MoveType.After, i => i.MatchStfld(out cameraToField));
        cursor.EmitLdarg0();
        cursor.EmitDelegate(resetCameraScaleOverride);

        // reset the camera scale back to what it should be
        static void resetCameraScaleOverride(object ienumeratorObject) {
            var selfData = DynamicData.For(ienumeratorObject);
            var cameraScaleFrom = selfData.Get<float>("FZO_cameraScaleFrom");
            Module.CurrentCameraScale = cameraScaleFrom;
        }

        cursor.GotoNextBestFit(MoveType.After, i => i.MatchLdfld(cameraToField), i => i.MatchCallOrCallvirt<Camera>("set_Position"));
        var setCameraPositionIndex = cursor.Index;
        FieldReference cameraAtField = null;
        cursor.GotoPrev(MoveType.Before, i => i.MatchCallOrCallvirt(typeof(Calc), nameof(Calc.Approach)), i => i.MatchStfld(out cameraAtField));
        cursor.Index = setCameraPositionIndex;
        cursor.EmitLdarg0();
        cursor.EmitLdloc1();
        cursor.EmitDelegate(setFinalCameraScale);

        static void setFinalCameraScale(object ienumeratorObject, Level level) {
            var selfData = DynamicData.For(ienumeratorObject);
            var cameraScaleTo = selfData.Get<float>("FZO_cameraScaleTo");
            Module.CameraScale = cameraScaleTo;
            Module.UpdateLevelZoomOut(level);
        }

        cursor.GotoNextBestFit(MoveType.After, i => i.MatchCallOrCallvirt<Vector2>(nameof(Vector2.Lerp)), i => i.MatchCallOrCallvirt<Camera>("set_Position"));
        cursor.EmitLdarg0();
        cursor.EmitLdarg0();
        cursor.EmitLdfld(cameraAtField);
        cursor.EmitLdloc1();
        cursor.EmitDelegate(setTransitionCameraScale);

        static void setTransitionCameraScale(object ienumeratorObject, float cameraAt, Level level) {
            var selfData = DynamicData.For(ienumeratorObject);
            var cameraScaleFrom = selfData.Get<float>("FZO_cameraScaleFrom");
            var cameraScaleTo = selfData.Get<float>("FZO_cameraScaleTo");
            Module.CameraScale = MathHelper.Lerp(cameraScaleFrom, cameraScaleTo, Ease.CubeOut(cameraAt));
            Module.UpdateLevelZoomOut(level);
        }

        // Console.WriteLine(il);
    }


    #endregion
}