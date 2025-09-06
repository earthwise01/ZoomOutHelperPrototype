using Microsoft.Xna.Framework;
using Monocle;
using YamlDotNet.Serialization;

namespace Celeste.Mod.FunctionalZoomOut;

public class FunctionalZoomOutSettings : EverestModuleSettings {
    // public bool AlwaysEnableZoomOut { get; set; } = false;

    [YamlIgnore]
    public int CameraScaleMaximum { get; set; } = -1;
    public void CreateCameraScaleMaximumEntry(TextMenu menu, bool inGame) {
        var option = new TextMenu.Option<int>(Dialog.Clean("modsettings_zoomouthelper_camerascalemaximum_name")).Add("Uncapped", -1, CameraScaleMaximum == -1);
        for (int i = 10; i >= 1; i--)
            option.Add(i + "x", i, CameraScaleMaximum == i);

        option.OnValueChange = (i) => CameraScaleMaximum = i;

        menu.Add(option);

        option.AddDescription(menu, Dialog.Clean("modsettings_zoomouthelper_camerascalemaximum_desc"));
    }
}
