local enums = require("consts.celeste_enums")

local spawnCameraScaleTrigger = {}

spawnCameraScaleTrigger.name = "ZoomOutHelperPrototype/SpawnCameraScaleTrigger"
spawnCameraScaleTrigger.category = "camera"
spawnCameraScaleTrigger.fieldInformation = {
    scale = {
        minimumValue = 0.01
    }
}
spawnCameraScaleTrigger.placements = {
    name = "spawnCameraScaleTrigger",
    alternativeName = "spawnZoomTrigger",
    data = {
        scale = 1.0
    }
}

return spawnCameraScaleTrigger