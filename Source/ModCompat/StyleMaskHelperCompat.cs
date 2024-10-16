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

public class StyleMaskHelperCompat : ModCompatBase {
    public override string Name => "StyleMaskHelper";

    #region Hook Fields

    private static ILHook hook_Mask_GetVisibleRect;

    private static ILHook hook_StylegroundLightingHandler_GameplayBuffers_Create;

    private static ILHook hook_BloomMask_GameplayBuffers_Create;

    // no colorgrade mask support yet

    private static ILHook hook_StylegroundMaskRenderer_GetBuffer;
    private static ILHook hook_StylegroundMaskRenderer_IsEntityInView;

    #endregion

    #region Load and Unload

    public override void Load() {
        if (Mask_GetVisibleRect is not null)
            hook_Mask_GetVisibleRect = new(Mask_GetVisibleRect, IL_Mask_GetVisibleRect);

        if (StylegroundLightingHandler_GameplayBuffers_Create is not null)
            hook_StylegroundLightingHandler_GameplayBuffers_Create = new(StylegroundLightingHandler_GameplayBuffers_Create, IL_StylegroundLightingHandler_GameplayBuffers_Create);

        if (BloomMask_GameplayBuffers_Create is not null)
            hook_BloomMask_GameplayBuffers_Create = new(BloomMask_GameplayBuffers_Create, IL_BloomMask_GameplayBuffers_Create);

        if (StylegroundMaskRenderer_GetBuffer is not null)
            hook_StylegroundMaskRenderer_GetBuffer = new(StylegroundMaskRenderer_GetBuffer, IL_StylegroundMaskRenderer_GetBuffer);
        if (StylegroundMaskRenderer_IsEntityInView is not null)
            hook_StylegroundMaskRenderer_IsEntityInView = new(StylegroundMaskRenderer_IsEntityInView, IL_StylegroundMaskRenderer_IsEntityInView);
    }

    public override void Unload() {
        hook_Mask_GetVisibleRect?.Dispose();
        hook_Mask_GetVisibleRect = null;

        hook_StylegroundLightingHandler_GameplayBuffers_Create?.Dispose();
        hook_StylegroundLightingHandler_GameplayBuffers_Create = null;

        hook_BloomMask_GameplayBuffers_Create?.Dispose();
        hook_BloomMask_GameplayBuffers_Create = null;

        hook_StylegroundMaskRenderer_GetBuffer?.Dispose();
        hook_StylegroundMaskRenderer_GetBuffer = null;
        hook_StylegroundMaskRenderer_IsEntityInView?.Dispose();
        hook_StylegroundMaskRenderer_IsEntityInView = null;
    }

    #endregion

    #region Hooks

    private static void IL_Mask_GetVisibleRect(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsInt();
    }

    private static void IL_StylegroundLightingHandler_GameplayBuffers_Create(ILContext il) {
        ILCursor cursor = new(il);

        if (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStsfld("Celeste.Mod.StyleMaskHelper.StylegroundLightingHandler", "Buffer"))) {
            Logger.Verbose("ZoomOutHelperPrototype", $"making the styleground lighting handler buffer get tracked at {cursor.Index} in cil for {cursor.Method.Name}...");
            cursor.EmitDup();
            cursor.EmitDelegate(Util.RegisterRenderTarget);
        }
    }

    private static void IL_BloomMask_GameplayBuffers_Create(ILContext il) {
        ILCursor cursor = new(il);

        if (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStsfld("Celeste.Mod.StyleMaskHelper.Entities.BloomMask", "BloomBuffer"))) {
            Logger.Verbose("ZoomOutHelperPrototype", $"making the bloom mask bloom buffer get tracked at {cursor.Index} in cil for {cursor.Method.Name}...");
            cursor.EmitDup();
            cursor.EmitDelegate(Util.RegisterRenderTarget);
        }

        if (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchStsfld("Celeste.Mod.StyleMaskHelper.Entities.BloomMask", "FadeBuffer"))) {
            Logger.Verbose("ZoomOutHelperPrototype", $"making the bloom mask fade buffer get tracked at {cursor.Index} in cil for {cursor.Method.Name}...");
            cursor.EmitDup();
            cursor.EmitDelegate(Util.RegisterRenderTarget);
        }
    }

    private static void IL_StylegroundMaskRenderer_GetBuffer(ILContext il) {
        ILCursor cursor = new(il);

        if (cursor.TryGotoNext(MoveType.After, instr => instr.MatchCall(typeof(VirtualContent), nameof(VirtualContent.CreateRenderTarget)))) {
            Logger.Verbose("ZoomOutHelperPrototype", $"making styleground mask buffers get tracked at {cursor.Index} in cil for {cursor.Method.Name}...");
            cursor.EmitDup();
            cursor.EmitDelegate(Util.RegisterRenderTarget);
        }
    }

    private static void IL_StylegroundMaskRenderer_IsEntityInView(ILContext il) {
        ILCursor cursor = new(il);
        cursor.FixCameraDimensionsInt();
    }

    #endregion

    #region Reflection

    private static MethodInfo Mask_GetVisibleRect;
    private static MethodInfo StylegroundLightingHandler_GameplayBuffers_Create;
    private static MethodInfo BloomMask_GameplayBuffers_Create;
    private static MethodInfo StylegroundMaskRenderer_GetBuffer;
    private static MethodInfo StylegroundMaskRenderer_IsEntityInView;

    public override void InitReflection() {
        var assembly = Module.GetType().Assembly;

        Mask_GetVisibleRect = ModCompatManager.GetExternalType(assembly, "Celeste.Mod.StyleMaskHelper.Entities.Mask")?.GetMethod("GetVisibleRect", BindingFlags.Public | BindingFlags.Instance);
        if (Mask_GetVisibleRect is null)
            Logger.Warn("ZoomOutHelperPrototype", "Couldn't find method GetVisibleRect in Celeste.Mod.StyleMaskHelper.Entities.Mask!");

        StylegroundLightingHandler_GameplayBuffers_Create = ModCompatManager.GetExternalType(assembly, "Celeste.Mod.StyleMaskHelper.StylegroundLightingHandler")?.GetMethod("GameplayBuffers_Create", BindingFlags.NonPublic | BindingFlags.Static);
        if (StylegroundLightingHandler_GameplayBuffers_Create is null)
            Logger.Warn("ZoomOutHelperPrototype", "Couldn't find method GameplayBuffers_Create in Celeste.Mod.StyleMaskHelper.StylegroundLightingHandler!");

        BloomMask_GameplayBuffers_Create = ModCompatManager.GetExternalType(assembly, "Celeste.Mod.StyleMaskHelper.Entities.BloomMask")?.GetMethod("GameplayBuffers_Create", BindingFlags.NonPublic | BindingFlags.Static);
        if (BloomMask_GameplayBuffers_Create is null)
            Logger.Warn("ZoomOutHelperPrototype", "Couldn't find method GameplayBuffers_Create in Celeste.Mod.StyleMaskHelper.Entities.BloomMask!");

        var stylegroundMaskRendererType = ModCompatManager.GetExternalType(assembly, "Celeste.Mod.StyleMaskHelper.Entities.StylegroundMaskRenderer");
        StylegroundMaskRenderer_GetBuffer = stylegroundMaskRendererType?.GetMethod("GetBuffer", BindingFlags.Public | BindingFlags.Static);
        if (StylegroundMaskRenderer_GetBuffer is null)
            Logger.Warn("ZoomOutHelperPrototype", "Couldn't find method GetBuffer in Celeste.Mod.StyleMaskHelper.Entities.StylegroundMaskRenderer!");
        StylegroundMaskRenderer_IsEntityInView = stylegroundMaskRendererType?.GetMethod("IsEntityInView", BindingFlags.Public | BindingFlags.Static);
        if (StylegroundMaskRenderer_IsEntityInView is null)
            Logger.Warn("ZoomOutHelperPrototype", "Couldn't find method IsEntityInView in Celeste.Mod.StyleMaskHelper.Entities.StylegroundMaskRenderer!");
    }

    #endregion

}
