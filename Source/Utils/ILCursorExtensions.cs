using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using static Celeste.Mod.FunctionalZoomOut.FunctionalZoomOutModule;

namespace Celeste.Mod.FunctionalZoomOut.Utils;

internal static class ILCursorExtensions {
    #region Game Scale Fixers

    internal static void EmitFixCameraSizeFloat(this ILCursor cursor) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching float camera size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitDelegate(GetFixedCameraSize);
    }
    internal static void EmitFixCameraSizeInt(this ILCursor cursor) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching integer camera size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitDelegate(GetFixedCameraSizeInt);
    }

    internal static void EmitFixCanvasSizeFloat(this ILCursor cursor) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching float canvas size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitDelegate(GetFixedCanvasSize);
    }
    internal static void EmitFixCanvasSizeInt(this ILCursor cursor) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching integer canvas size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitDelegate(GetFixedCanvasSizeInt);
    }

    internal static void EmitFixCameraSizeFloatPadded(this ILCursor cursor, int padding) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching padded float camera size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitLdcI4(padding);
        cursor.EmitDelegate(GetFixedCameraSizePadded);
    }
    internal static void EmitFixCameraSizeIntPadded(this ILCursor cursor, int padding) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching padded integer camera size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitLdcI4(padding);
        cursor.EmitDelegate(GetFixedCameraSizeIntPadded);
    }

    internal static void EmitFixHDUpscale(this ILCursor cursor) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching hd upscaling value at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitDelegate(GetFixedHDUpscale);
    }

    #endregion

    #region Search and Replace

    private static void SearchAndReplace(this ILCursor cursor, Action<ILCursor> replacerMethod, params Func<Instruction, bool>[] predicates) {
        int searchStartIndex = cursor.Index;

        while (cursor.TryGotoNext(MoveType.After, predicates)) {
            replacerMethod(cursor);
        }

        cursor.Index = searchStartIndex;
    }

    // very random but i find it funny in a kinda ironic way that the mod specifically meant to "uninline" the gamewidth/height consts is itself inlining the gamewidth/height consts
    internal static void FixCameraDimensionsFloat(this ILCursor cursor) =>
        cursor.SearchAndReplace(EmitFixCameraSizeFloat, instr => instr.MatchLdcR4(Celeste.GameWidth) || instr.MatchLdcR4(Celeste.GameHeight));
    internal static void FixCameraDimensionsInt(this ILCursor cursor) =>
        cursor.SearchAndReplace(EmitFixCameraSizeInt, instr => instr.MatchLdcI4(Celeste.GameWidth) || instr.MatchLdcI4(Celeste.GameHeight));

    internal static void FixCameraDimensionsFloatHalf(this ILCursor cursor) =>
        cursor.SearchAndReplace(EmitFixCameraSizeFloat, instr => instr.MatchLdcR4(Celeste.GameWidth / 2f) || instr.MatchLdcR4(Celeste.GameHeight / 2f));
    internal static void FixCameraDimensionsIntHalf(this ILCursor cursor) =>
        cursor.SearchAndReplace(EmitFixCameraSizeInt, instr => instr.MatchLdcI4(Celeste.GameWidth / 2) || instr.MatchLdcI4(Celeste.GameHeight / 2));

    private static void SearchAndReplacePadded(this ILCursor cursor, Action<ILCursor, int> replacerMethod, int padding, params Func<Instruction, bool>[] predicates) {
        int searchStartIndex = cursor.Index;

        while (cursor.TryGotoNext(MoveType.After, predicates)) {
            replacerMethod(cursor, padding);
        }

        cursor.Index = searchStartIndex;
    }
    internal static void FixCameraDimensionsFloatPadded(this ILCursor cursor, int padding) =>
        cursor.SearchAndReplacePadded(EmitFixCameraSizeFloatPadded, padding, instr => instr.MatchLdcR4(Celeste.GameWidth + padding) || instr.MatchLdcR4(Celeste.GameHeight + padding));
    internal static void FixCameraDimensionsIntPadded(this ILCursor cursor, int padding) =>
        cursor.SearchAndReplacePadded(EmitFixCameraSizeIntPadded, padding, instr => instr.MatchLdcI4(Celeste.GameWidth + padding) || instr.MatchLdcI4(Celeste.GameHeight + padding));

    #endregion
}
