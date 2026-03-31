namespace Celeste.Mod.FunctionalZoomOut;
public class FunctionalZoomOutMapDataProcessor : EverestMapDataProcessor {

    public static Dictionary<(int, AreaMode), Dictionary<string, float>> RoomZoomControllerValues { get; private set; } = [];
    private string _levelName;

    public override Dictionary<string, Action<BinaryPacker.Element>> Init() {
        void RoomZoomController(BinaryPacker.Element entityData) {
            float cameraScale = entityData.AttrFloat("cameraScale", 1f);
            if (!RoomZoomControllerValues.TryGetValue((AreaKey.ID, AreaKey.Mode), out Dictionary<string, float> currentMapValues))
                RoomZoomControllerValues[(AreaKey.ID, AreaKey.Mode)] = currentMapValues = [];

            currentMapValues[_levelName] = cameraScale;

            Logger.Info("ZoomOutHelperPrototype", $"[MapDataProcessor] found a RoomZoomController with camera scale {cameraScale} in room {_levelName} in map {AreaKey.SID} ({AreaKey.Mode})!");
        }

        return new Dictionary<string, Action<BinaryPacker.Element>> {
            {
                "level", level => {
                    // be sure to write the level name down.
                    _levelName = level.Attr("name").Split(':')[0];
                    if (_levelName.StartsWith("lvl_")) {
                        _levelName = _levelName[4..];
                    }
                }
            },
            // room zoom controller
            {
                "entity:ZoomOutHelperPrototype/RoomZoomController", RoomZoomController
            },
        };
    }

    public override void Reset() {
        RoomZoomControllerValues.Remove((AreaKey.ID, AreaKey.Mode));
    }

    public override void End() {

    }
}