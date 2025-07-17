using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.FunctionalZoomOut;

public class FunctionalZoomOutSettings : EverestModuleSettings {
    // public bool AlwaysEnableZoomOut { get; set; } = false;

    public int CameraScaleMaximum { get; set; } = -1;
    public void CreateCameraScaleMaximumEntry(TextMenu menu, bool inGame) {
        var option = new TextMenu.Option<int>(Dialog.Clean("modsettings_zoomouthelper_camerascalemaximum_name")).Add("Uncapped", -1, CameraScaleMaximum == -1);
        for (int i = 1; i <= 10; i++)
            option.Add(i + "x", i, CameraScaleMaximum == i);

        option.OnValueChange = (i) => CameraScaleMaximum = i;

        menu.Add(option);
    }
}
