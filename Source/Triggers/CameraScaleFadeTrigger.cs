using Celeste.Mod.Entities;

namespace Celeste.Mod.FunctionalZoomOut.Triggers;

[CustomEntity("ZoomOutHelperPrototype/CameraScaleFadeTrigger")]
[Tracked]
public class CameraScaleFadeTrigger : Trigger {
    private readonly float scaleFrom, scaleTo;
    private readonly PositionModes positionMode;

    public CameraScaleFadeTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        scaleFrom = data.Float("scaleFrom", 1f);
        scaleTo = data.Float("scaleTo", 1f);
        positionMode = data.Enum("positionMode", PositionModes.NoEffect);
    }

    public override void Added(Scene scene) {
        base.Added(scene);

        if (!Module.HooksActive)
            Logger.Warn("ZoomOutHelperPrototype", "warning! camera scale fade triggers won't work without a zoom controller present!");
    }

    public override void OnStay(Player player) {
        base.OnStay(player);

        Module.CameraScale = MathHelper.Lerp(scaleFrom, scaleTo, Ease.SineInOut(GetPositionLerp(player, positionMode)));
    }

    // [HookLoadCallback("mainZoomHooks")]
    // internal static void LoadSpawnCameraScaleTrigger() {
    //     Everest.Events.Player.OnSpawn += Event_Player_Spawn;
    // }

    // [HookUnloadCallback("mainZoomHooks")]
    // internal static void UnloadSpawnCameraScaleTrigger() {
    //     Everest.Events.Player.OnSpawn -= Event_Player_Spawn;
    // }

    // private static void Event_Player_Spawn(Player player) {
    //     var trigger = player.CollideFirst<CameraScaleFadeTrigger>();
    //     trigger?.OnStay(player);
    // }
}
