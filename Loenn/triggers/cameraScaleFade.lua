local enums = require("consts.celeste_enums")

local cameraScaleFade = {}

cameraScaleFade.name = "ZoomOutHelperPrototype/CameraScaleFadeTrigger"
cameraScaleFade.category = "camera"
cameraScaleFade.fieldInformation = {
    positionMode = {
        options = enums.trigger_position_modes,
        editable = false
    },
    scaleFrom = {
        minimumValue = 0.01
    },
    scaleTo = {
        minimumValue = 0.01
    }
}
cameraScaleFade.placements = {
    name = "cameraScaleFade",
    alternativeName = "zoomOutFade",
    data = {
        scaleFrom = 1.0,
        scaleTo = 1.0,
        positionMode = "NoEffect",
    }
}

return cameraScaleFade