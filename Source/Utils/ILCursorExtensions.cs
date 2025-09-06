namespace Celeste.Mod.FunctionalZoomOut.Utils;

internal static class ILCursorExtensions {
    #region Emitter Methods

    internal static void EmitFixCameraSizeFloat(this ILCursor cursor) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching float camera size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitDelegate(Module.GetFixedCameraSize);
    }
    internal static void EmitFixCameraSizeInt(this ILCursor cursor) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching integer camera size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitDelegate(Module.GetFixedCameraSizeInt);
    }

    internal static void EmitFixCanvasSizeFloat(this ILCursor cursor) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching integer canvas size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitDelegate(Module.GetFixedCanvasSizeFloat);
    }
    internal static void EmitFixCanvasSizeInt(this ILCursor cursor) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching integer canvas size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitDelegate(Module.GetFixedCanvasSizeInt);
    }

    internal static void EmitFixCameraSizeFloatPadded(this ILCursor cursor, int padding) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching padded float camera size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitLdcI4(padding);
        cursor.EmitDelegate(Module.GetFixedCameraSizePadded);
    }
    internal static void EmitFixCameraSizeIntPadded(this ILCursor cursor, int padding) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching padded integer camera size value ({cursor.Prev.Operand}) at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitLdcI4(padding);
        cursor.EmitDelegate(Module.GetFixedCameraSizeIntPadded);
    }

    internal static void EmitFixHDUpscale(this ILCursor cursor) {
        Logger.Verbose("ZoomOutHelperPrototype", $"patching hd upscaling value at {cursor.Index} in cil for {cursor.Method.Name}...");
        cursor.EmitDelegate(Module.GetFixedHDUpscale);
    }

    #endregion

    #region Search and Emit
    private static void SearchAndEmit(this ILCursor cursor, Action<ILCursor> emitterMethod, params Func<Instruction, bool>[] predicates) {
        if (cursor.TryGotoNext(MoveType.After, predicates))
            emitterMethod(cursor);
    }

    internal static void FixNextCameraWidthFloat(this ILCursor cursor) =>
        cursor.SearchAndEmit(EmitFixCameraSizeFloat, instr => instr.MatchLdcR4(Celeste.GameWidth));
    internal static void FixNextCameraHeightFloat(this ILCursor cursor) =>
        cursor.SearchAndEmit(EmitFixCameraSizeFloat, instr => instr.MatchLdcR4(Celeste.GameHeight));
    internal static void FixNextCameraWidthInt(this ILCursor cursor) =>
        cursor.SearchAndEmit(EmitFixCameraSizeInt, instr => instr.MatchLdcI4(Celeste.GameWidth));
    internal static void FixNextCameraHeightInt(this ILCursor cursor) =>
        cursor.SearchAndEmit(EmitFixCameraSizeInt, instr => instr.MatchLdcI4(Celeste.GameHeight));

    internal static void FixNextCameraDimensionsFloatHalf(this ILCursor cursor) =>
        cursor.SearchAndEmit(EmitFixCameraSizeFloat, instr => instr.MatchLdcR4(Celeste.GameWidth / 2f) || instr.MatchLdcR4(Celeste.GameHeight / 2f));
    internal static void FixNextCameraDimensionsIntHalf(this ILCursor cursor) =>
        cursor.SearchAndEmit(EmitFixCameraSizeInt, instr => instr.MatchLdcI4(Celeste.GameWidth / 2) || instr.MatchLdcI4(Celeste.GameHeight / 2));

    internal static void FixNextCanvasWidthInt(this ILCursor cursor) =>
        cursor.SearchAndEmit(EmitFixCanvasSizeInt, instr => instr.MatchLdcI4(Celeste.GameWidth));
    internal static void FixNextCanvasHeightInt(this ILCursor cursor) =>
        cursor.SearchAndEmit(EmitFixCanvasSizeInt, instr => instr.MatchLdcI4(Celeste.GameHeight));


    // mass find and replace

    private static void SearchAndEmitAll(this ILCursor cursor, Action<ILCursor> emitterMethod, params Func<Instruction, bool>[] predicates) {
        int searchStartIndex = cursor.Index;

        while (cursor.TryGotoNext(MoveType.After, predicates))
            emitterMethod(cursor);

        cursor.Index = searchStartIndex;
    }

    internal static void FixAllCameraDimensionsFloat(this ILCursor cursor) =>
        cursor.SearchAndEmitAll(EmitFixCameraSizeFloat, instr => instr.MatchLdcR4(Celeste.GameWidth) || instr.MatchLdcR4(Celeste.GameHeight));
    internal static void FixAllCameraDimensionsInt(this ILCursor cursor) =>
        cursor.SearchAndEmitAll(EmitFixCameraSizeInt, instr => instr.MatchLdcI4(Celeste.GameWidth) || instr.MatchLdcI4(Celeste.GameHeight));

    internal static void FixAllCameraDimensionsFloatHalf(this ILCursor cursor) =>
        cursor.SearchAndEmitAll(EmitFixCameraSizeFloat, instr => instr.MatchLdcR4(Celeste.GameWidth / 2f) || instr.MatchLdcR4(Celeste.GameHeight / 2f));
    internal static void FixAllCameraDimensionsIntHalf(this ILCursor cursor) =>
        cursor.SearchAndEmitAll(EmitFixCameraSizeInt, instr => instr.MatchLdcI4(Celeste.GameWidth / 2) || instr.MatchLdcI4(Celeste.GameHeight / 2));

    // padded
    private static void SearchAndEmitAllPadded(this ILCursor cursor, Action<ILCursor, int> replacerMethod, int padding, params Func<Instruction, bool>[] predicates) {
        int searchStartIndex = cursor.Index;

        while (cursor.TryGotoNext(MoveType.After, predicates))
            replacerMethod(cursor, padding);

        cursor.Index = searchStartIndex;
    }
    internal static void FixAllCameraDimensionsFloatPadded(this ILCursor cursor, int padding) =>
        cursor.SearchAndEmitAllPadded(EmitFixCameraSizeFloatPadded, padding, instr => instr.MatchLdcR4(Celeste.GameWidth + padding) || instr.MatchLdcR4(Celeste.GameHeight + padding));
    internal static void FixAllCameraDimensionsIntPadded(this ILCursor cursor, int padding) =>
        cursor.SearchAndEmitAllPadded(EmitFixCameraSizeIntPadded, padding, instr => instr.MatchLdcI4(Celeste.GameWidth + padding) || instr.MatchLdcI4(Celeste.GameHeight + padding));

    #endregion
}
