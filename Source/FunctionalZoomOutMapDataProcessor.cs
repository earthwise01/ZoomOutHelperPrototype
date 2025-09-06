namespace Celeste.Mod.FunctionalZoomOut;
public class FunctionalZoomOutMapDataProcessor : EverestMapDataProcessor {

    public static Dictionary<(int, AreaMode), Dictionary<string, float>> RoomZoomControllerValues { get; private set; } = [];
    private string levelName;

    public override Dictionary<string, Action<BinaryPacker.Element>> Init() {
        void roomZoomController(BinaryPacker.Element entityData) {
            var cameraScale = entityData.AttrFloat("cameraScale", 1f);
            if (!RoomZoomControllerValues.TryGetValue((AreaKey.ID, AreaKey.Mode), out var currentMapValues))
                RoomZoomControllerValues[(AreaKey.ID, AreaKey.Mode)] = currentMapValues = [];

            currentMapValues[levelName] = cameraScale;

            Logger.Info("ZoomOutHelperPrototype", $"[MapDataProcessor] found a RoomZoomController with camera scale {cameraScale} in room {levelName} in map {AreaKey.SID} ({AreaKey.Mode})!");
        }

        return new Dictionary<string, Action<BinaryPacker.Element>> {
            {
                "level", level => {
                    // be sure to write the level name down.
                    levelName = level.Attr("name").Split(':')[0];
                    if (levelName.StartsWith("lvl_")) {
                        levelName = levelName[4..];
                    }
                }
            },
            // room zoom controller
            {
                "entity:ZoomOutHelperPrototype/RoomZoomController", roomZoomController
            },
        };
    }

    public override void Reset() {
        RoomZoomControllerValues.Remove((AreaKey.ID, AreaKey.Mode));
    }

    public override void End() {

    }
}