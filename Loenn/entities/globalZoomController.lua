local globalZoomController = {}

globalZoomController.name = "ZoomOutHelperPrototype/GlobalZoomController"
globalZoomController.texture = "@Internal@/northern_lights"
globalZoomController.placements = {
    name = "globalZoomController",
    data = {
        initialCameraScale = 1,
        _modVersion = 1 -- placeholder for potential backwards compatibility
    }
}

globalZoomController.fieldInformation = {
    initialCameraScale = {
        minimumValue = 0.01
    },
}
globalZoomController.ignoredFields = {"_name", "_id", "originX", "originY", "_modVersion"}

return globalZoomController