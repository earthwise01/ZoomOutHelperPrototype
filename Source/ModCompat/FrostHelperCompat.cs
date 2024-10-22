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

// only loaded for old frosthelper versions since modern frosthelper has support for resized gameplay buffers built in !
public class FrostHelperCompat : ModCompatBase {
    public override string Name => "FrostHelper";

    #region Hook Fields

    private static Hook hook_ModIntegration_HDlesteCompat_get_Scale;

    // custom spinner
    private static ILHook hook_CustomSpinner_InView;
    // InView is inlined (fun!) but luckily private and only used in a few methods, so it's possible to just hook those too in order to make sure it updates correctly
    private static ILHook hook_CustomSpinner_Awake;
    private static ILHook hook_CustomSpinner_Update;
    private static ILHook hook_CustomSpinner_Destroy;

    // cull helper
    private static ILHook hook_CameraCullHelper_IsRectVisible_Vector2_Rectangle;
    private static ILHook hook_CameraCullHelper_IsRectVisible_Vector2_float_float_float_float;

    #endregion

    #region Load and Unload

    public override void Load() {
        if (ModIntegration_HDlesteCompat_get_Scale is not null)
            hook_ModIntegration_HDlesteCompat_get_Scale = new(ModIntegration_HDlesteCompat_get_Scale, On_ModIntegration_HDlesteCompat_get_Scale);

        if (CustomSpinnerType is not null) {
            var InView = CustomSpinnerType.GetMethod("InView", BindingFlags.NonPublic | BindingFlags.Instance);
            var Awake = CustomSpinnerType.GetMethod("Awake", BindingFlags.Public | BindingFlags.Instance);
            var Update = CustomSpinnerType.GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
            var Destroy = CustomSpinnerType.GetMethod("Destroy", BindingFlags.Public | BindingFlags.Instance);

            if (InView is not null) {
                hook_CustomSpinner_InView = new(InView, IL_CustomSpinner_InView);

                // dummy hooks bc inlining moment
                if (Awake is not null)
                    hook_CustomSpinner_Awake = new(Awake, IL_DummyHook);
                if (Update is not null)
                    hook_CustomSpinner_Update = new(Update, IL_DummyHook);
                if (Destroy is not null)
                    hook_CustomSpinner_Destroy = new(Destroy, IL_DummyHook);
            }
        }

        if (CameraCullHelperType is not null) {
            var IsRectVisible_Vector2_Rectangle = CameraCullHelperType.GetMethod("IsRectVisible", BindingFlags.NonPublic | BindingFlags.Static, [typeof(Vector2), typeof(Rectangle)]);
            var IsRectVisible_Vector2_float_float_float_float = CameraCullHelperType.GetMethod("IsRectVisible", BindingFlags.NonPublic | BindingFlags.Static, [typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(float)]);

            if (IsRectVisible_Vector2_Rectangle is not null)
                hook_CameraCullHelper_IsRectVisible_Vector2_Rectangle = new(IsRectVisible_Vector2_Rectangle, IL_CameraCullHelper_IsRectVisible);
            if (IsRectVisible_Vector2_float_float_float_float is not null)
                hook_CameraCullHelper_IsRectVisible_Vector2_float_float_float_float = new(IsRectVisible_Vector2_float_float_float_float, IL_CameraCullHelper_IsRectVisible);
        }
    }

    public override void Unload() {
        hook_ModIntegration_HDlesteCompat_get_Scale?.Dispose();
        hook_ModIntegration_HDlesteCompat_get_Scale = null;

        hook_CustomSpinner_InView?.Dispose();
        hook_CustomSpinner_Awake?.Dispose();
        hook_CustomSpinner_Update?.Dispose();
        hook_CustomSpinner_Destroy?.Dispose();
        hook_CustomSpinner_InView = null;
        hook_CustomSpinner_Awake = null;
        hook_CustomSpinner_Update = null;
        hook_CustomSpinner_Destroy = null;

        hook_CameraCullHelper_IsRectVisible_Vector2_Rectangle?.Dispose();
        hook_CameraCullHelper_IsRectVisible_Vector2_float_float_float_float?.Dispose();
        hook_CameraCullHelper_IsRectVisible_Vector2_Rectangle = null;
        hook_CameraCullHelper_IsRectVisible_Vector2_float_float_float_float = null;
    }

    #endregion

    #region Hooks

    private delegate int orig_ModIntegration_HDlesteCompat_get_Scale();
    private static int On_ModIntegration_HDlesteCompat_get_Scale(orig_ModIntegration_HDlesteCompat_get_Scale orig) {
        // FrostHelper assumes that zoom out is HDleste so it needs to be patched to disable HDleste compat
        if (!FunctionalZoomOutModule.ZoomOutActive)
            return orig();

        return 1;
    }

    private static void IL_CustomSpinner_InView(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloatPadded(16);
    }

    private static void IL_CameraCullHelper_IsRectVisible(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsFloat();
    }

    #endregion

    #region Reflection

    private static MethodInfo ModIntegration_HDlesteCompat_get_Scale;
    private static Type CustomSpinnerType;
    private static Type CameraCullHelperType;

    public override void InitReflection() {
        var assembly = Module.GetType().Assembly;

        ModIntegration_HDlesteCompat_get_Scale = ModCompatManager.GetExternalType(assembly, "FrostHelper.ModIntegration.HDlesteCompat")?.GetMethod("get_Scale", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (ModIntegration_HDlesteCompat_get_Scale is null)
            Logger.Warn("ZoomOutHelperPrototype", "Couldn't find method get_Scale in FrostHelper.ModIntegration.HDlesteCompat!");

        CustomSpinnerType = ModCompatManager.GetExternalType(assembly, "FrostHelper.CustomSpinner");
        CameraCullHelperType = ModCompatManager.GetExternalType(assembly, "FrostHelper.CameraCullHelper");
    }

    #endregion

}
