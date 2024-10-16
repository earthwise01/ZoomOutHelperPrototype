using System;
using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod;
using Celeste.Mod.FunctionalZoomOut.Utils;
using Celeste.Mod.Entities;

namespace Celeste.Mod.FunctionalZoomOut.Triggers;

[CustomEntity("ZoomOutHelperPrototype/CameraScaleFadeTrigger")]
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

        if (!FunctionalZoomOutModule.LevelContainsZoomOut)
            Logger.Warn("ZoomOutHelperPrototype", "warning! camera scale fade triggers won't work without a zoom controller present!");
    }

    public override void OnStay(Player player) {
        base.OnStay(player);

        FunctionalZoomOutModule.CameraScale = MathHelper.Lerp(scaleFrom, scaleTo, Ease.SineInOut(GetPositionLerp(player, positionMode)));
    }
}
