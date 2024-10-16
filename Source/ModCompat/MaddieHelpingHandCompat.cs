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
using System.Linq;
using Celeste.Mod.FunctionalZoomOut.Utils;

namespace Celeste.Mod.FunctionalZoomOut.ModCompat;

public class MaddieHelpingHandCompat : ModCompatBase {
    public override string Name => "MaxHelpingHand";

    #region Hook Fields

    // offset border
    private static ILHook hook_CameraOffsetBorder_modCameraTarget;

    // flag switch gate
    private static ILHook hook_FlagSwitchGate_InView;
    // InView is inlined (fun!) but luckily private and only used in Render, so it's possible to just hook that too to make it recompile correctly
    private static ILHook hook_FlagSwitchGate_Render;

    #endregion

    #region Load and Unload

    public override void Load() {
        if (CameraOffsetBorder_modCameraTarget is not null)
            hook_CameraOffsetBorder_modCameraTarget = new(CameraOffsetBorder_modCameraTarget, IL_CameraOffsetBorder_modCameraTarget);

        if (FlagSwitchGateType is not null) {
            var InView = FlagSwitchGateType.GetMethod("InView", BindingFlags.NonPublic | BindingFlags.Instance);
            var Render = FlagSwitchGateType.GetMethod("Render", BindingFlags.Public | BindingFlags.Instance);

            if (InView is not null) {
                hook_FlagSwitchGate_InView = new(InView, IL_FlagSwitchGate_InView);

                // dummy hook bc inlining moment
                if (Render is not null)
                    hook_FlagSwitchGate_Render = new(Render, IL_DummyHook);
            }
        }
    }

    public override void Unload() {
        hook_CameraOffsetBorder_modCameraTarget?.Dispose();
        hook_CameraOffsetBorder_modCameraTarget = null;

        hook_FlagSwitchGate_InView?.Dispose();
        hook_FlagSwitchGate_Render?.Dispose();
        hook_FlagSwitchGate_InView = null;
        hook_FlagSwitchGate_Render = null;
    }

    #endregion

    #region Hooks

    private static void IL_CameraOffsetBorder_modCameraTarget(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsInt();
    }

    private static void IL_FlagSwitchGate_InView(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloat();

        // // forcefully prevent inlining(?)
        // for (var i = 0; i < 32; i++) {
        //     cursor.Emit(OpCodes.Nop);
        // }
    }

    #endregion

    #region Reflection

    private static MethodInfo CameraOffsetBorder_modCameraTarget;
    private static Type FlagSwitchGateType;

    public override void InitReflection() {
        var assembly = Module.GetType().Assembly;

        CameraOffsetBorder_modCameraTarget = ModCompatManager.GetExternalType(assembly, "Celeste.Mod.MaxHelpingHand.Triggers.CameraOffsetBorder")?.GetMethod("modCameraTarget", BindingFlags.NonPublic | BindingFlags.Static);
        if (CameraOffsetBorder_modCameraTarget is null)
            Logger.Warn("ZoomOutHelperPrototype", "Couldn't find method modCameraTarget in Celeste.Mod.MaxHelpingHand.Triggers.CameraOffsetBorder!");

        FlagSwitchGateType = ModCompatManager.GetExternalType(assembly, "Celeste.Mod.MaxHelpingHand.Entities.FlagSwitchGate");
    }

    #endregion

}
