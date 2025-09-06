-- local drawableSprite = require("structs.drawable_sprite")
-- local drawableText = require("structs.drawable_text")
-- local loadedState = require("loaded_state")

-- local globalZoomController = {}

-- globalZoomController.name = "ZoomOutHelperPrototype/RoomZoomController"
-- globalZoomController.texture = "@Internal@/northern_lights"
-- globalZoomController.placements = {
--     name = "roomZoomControllerUnfinished",
--     data = {
--         cameraScale = 1,
--         _modVersion = 1 -- placeholder for potential backwards compatibility
--     }
-- }

-- globalZoomController.fieldInformation = {
--     cameraScale = {
--         minimumValue = 0.01
--     },
-- }
-- globalZoomController.ignoredFields = {"_name", "_id", "originX", "originY", "_modVersion"}

-- local function isDuplicate(self, map)
--     map = map or loadedState.map
--     if not map then
--         return false
--     end

--     local name = self._name

--     for _, room in ipairs(map.rooms) do
--         for _, entity in ipairs(room.entities) do
--             if entity ~= self and entity._name == "name" then
--                 -- if entity._id  actually nvm
--                 return true
--             end
--         end
--     end

--     return false
-- end

-- function globalZoomController.sprite(room, entity)
--     local x, y = entity.x or 0, entity.y or 0

--     local controller = drawableSprite.fromTexture("editorSprites/ZoomOutHelperPrototype/zoomController", {x = x, y = y})
--     local global = drawableText.fromText("Global", x - 16, y - 21, 32, 8, nil, 1)

--     local sprites = { controller }

--     if isDuplicate(entity) then
--         local duplicate = drawableText.fromText("!Duplicate!", x - 24, y + 11, 48, 8, nil, 1, {1.0, 0.0, 0.0})
--         table.insert(sprites, duplicate)
--     end

--     return sprites
-- end

-- return globalZoomController