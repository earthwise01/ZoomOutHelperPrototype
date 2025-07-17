using Celeste.Mod.Entities;

namespace Celeste.Mod.FunctionalZoomOut.Triggers;

[CustomEntity("ZoomOutHelperPrototype/SpawnCameraScaleTrigger")]
[Tracked]
public class SpawnCameraScaleTrigger : Trigger {
    private readonly float scale;

    public SpawnCameraScaleTrigger(EntityData data, Vector2 offset) : base(data, offset) {
        scale = data.Float("scale", 1f);
    }

    [HookLoadCallback("mainZoomHooks")]
    internal static void LoadSpawnCameraScaleTrigger() {
        Everest.Events.Player.OnSpawn += Event_Player_Spawn;
    }

    [HookUnloadCallback("mainZoomHooks")]
    internal static void UnloadSpawnCameraScaleTrigger() {
        Everest.Events.Player.OnSpawn -= Event_Player_Spawn;
    }

    private static void Event_Player_Spawn(Player player) {
        var trigger = player.CollideFirst<SpawnCameraScaleTrigger>();
        if (trigger is not null)
            Module.CameraScale = trigger.scale;
    }
}
