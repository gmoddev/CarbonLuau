-- Trusted, build-embedded facade. Host/userdata construction are private.
-- @carbonluau-api {"Owner":"Signal","Name":"Connect","Kind":"Method","Args":[["Callback","(Player) -> ()"]],"Returns":["Connection"],"Summary":"Connect a bounded Player callback.","SinceApi":"0.3.0-experimental","Binding":"    function Value.Connect(Self, Callback)"}
-- @carbonluau-api {"Owner":"Connection","Name":"Disconnect","Kind":"Method","Args":[],"Returns":[],"Summary":"Disconnect this subscription.","SinceApi":"0.3.0-experimental","Binding":"        function Connection.Disconnect(SelfConnection)"}
-- Structured API declarations are co-located with the trusted implementation.
-- @carbonluau-api {"Type":"Player","Kind":"Class","Summary":"Connection-bound player facade.","SinceApi":"0.3.0-experimental"}
-- @carbonluau-api {"Type":"Players","Kind":"Service","Summary":"Connected players and lifecycle signals.","SinceApi":"0.3.0-experimental","Binding":"    if Name == \"Players\" then return Players end"}
-- @carbonluau-api {"Type":"Commands","Kind":"Service","Summary":"Bounded permission-aware command registration.","SinceApi":"0.3.0-experimental","Binding":"    if Name == \"Commands\" then return Commands end"}
-- @carbonluau-api {"Type":"Gui","Kind":"Service","Summary":"Retained GUI construction.","Binding":"    if Name == \"Gui\" then return Gui end"}
-- @carbonluau-api {"Type":"Items","Kind":"Service","Summary":"Read-only item definition lookup.","Binding":"    if Name == \"Items\" then return Items end"}
-- @carbonluau-api {"Type":"Game","Kind":"Global","Summary":"Selected scripting API identity and services.","SinceApi":"0.3.0-experimental"}
-- @carbonluau-api {"Type":"Signal","Kind":"Class","Summary":"Bounded callback registration.","SinceApi":"0.3.0-experimental"}
-- @carbonluau-api {"Type":"Connection","Kind":"Class","Summary":"A disconnectable Signal subscription.","SinceApi":"0.3.0-experimental"}
-- @carbonluau-api {"Type":"CommandContext","Kind":"Class","Summary":"Immutable command callback context.","Representation":"Record","SinceApi":"0.3.0-experimental"}
-- @carbonluau-api {"Type":"CommandOptions","Kind":"Class","Summary":"Command registration options.","Representation":"Record","SinceApi":"0.3.0-experimental"}
-- @carbonluau-api {"Type":"Vector3","Kind":"Value","Summary":"Immutable finite spatial vector."}
-- @carbonluau-api {"Type":"Addon","Kind":"Global","Summary":"Current addon identity and declared dependency availability."}
-- @carbonluau-api {"Type":"Task","Kind":"Global","Summary":"Bounded host-admitted scheduling.","SinceApi":"0.2.0-experimental"}
-- @carbonluau-api {"Owner":"Player","Name":"Name","Kind":"Property","ValueType":"string","Writable":false,"Summary":"Player.Name.","SinceApi":"0.3.0-experimental","Binding":"            if Key == \"Name\" then return Info[1] == \"1\" and Info[2] or Name end"}
-- @carbonluau-api {"Owner":"Player","Name":"UserId","Kind":"Property","ValueType":"string","Writable":false,"Summary":"Player.UserId.","SinceApi":"0.3.0-experimental","Binding":"            if Key == \"UserId\" then return UserId end"}
-- @carbonluau-api {"Owner":"Player","Name":"IsConnected","Kind":"Property","ValueType":"boolean","Writable":false,"Summary":"Player.IsConnected.","SinceApi":"0.3.0-experimental","Binding":"            if Key == \"IsConnected\" then return Info[1] == \"1\" end"}
-- @carbonluau-api {"Owner":"Player","Name":"Position","Kind":"Property","ValueType":"Vector3","Writable":false,"Summary":"Player.Position.","Binding":"            if Key == \"Position\" then"}
-- @carbonluau-api {"Owner":"Player","Name":"Health","Kind":"Property","ValueType":"number","Writable":false,"Summary":"Player.Health.","Binding":"            if Key == \"Health\" then return tonumber(Host(23, Token, UserId)[1]) end"}
-- @carbonluau-api {"Owner":"Player","Name":"MaxHealth","Kind":"Property","ValueType":"number","Writable":false,"Summary":"Player.MaxHealth.","Binding":"            if Key == \"MaxHealth\" then return tonumber(Host(24, Token, UserId)[1]) end"}
-- @carbonluau-api {"Owner":"Game","Name":"ApiName","Kind":"Property","ValueType":"\"CarbonLuau\"","Writable":false,"Summary":"Game.ApiName.","SinceApi":"0.3.0-experimental","Binding":"local Game = {ApiName = \"CarbonLuau\", ApiVersion = \"0.4.0-experimental\", ApiStatus = \"Experimental\"}"}
-- @carbonluau-api {"Owner":"Game","Name":"ApiVersion","Kind":"Property","ValueType":"\"0.4.0-experimental\"","Writable":false,"Summary":"Game.ApiVersion.","SinceApi":"0.3.0-experimental","Binding":"local Game = {ApiName = \"CarbonLuau\", ApiVersion = \"0.4.0-experimental\", ApiStatus = \"Experimental\"}"}
-- @carbonluau-api {"Owner":"Game","Name":"ApiStatus","Kind":"Property","ValueType":"\"Experimental\"","Writable":false,"Summary":"Game.ApiStatus.","SinceApi":"0.3.0-experimental","Binding":"local Game = {ApiName = \"CarbonLuau\", ApiVersion = \"0.4.0-experimental\", ApiStatus = \"Experimental\"}"}
-- @carbonluau-api {"Owner":"Vector3","Name":"X","Kind":"Property","ValueType":"number","Writable":false,"Summary":"Vector3.X.","Binding":"    return Value(\"Vector3\", {X = X, Y = Y, Z = Z, Magnitude = math.sqrt(X * X + Y * Y + Z * Z)})"}
-- @carbonluau-api {"Owner":"Vector3","Name":"Y","Kind":"Property","ValueType":"number","Writable":false,"Summary":"Vector3.Y.","Binding":"    return Value(\"Vector3\", {X = X, Y = Y, Z = Z, Magnitude = math.sqrt(X * X + Y * Y + Z * Z)})"}
-- @carbonluau-api {"Owner":"Vector3","Name":"Z","Kind":"Property","ValueType":"number","Writable":false,"Summary":"Vector3.Z.","Binding":"    return Value(\"Vector3\", {X = X, Y = Y, Z = Z, Magnitude = math.sqrt(X * X + Y * Y + Z * Z)})"}
-- @carbonluau-api {"Owner":"Vector3","Name":"Magnitude","Kind":"Property","ValueType":"number","Writable":false,"Summary":"Vector3.Magnitude.","Binding":"    return Value(\"Vector3\", {X = X, Y = Y, Z = Z, Magnitude = math.sqrt(X * X + Y * Y + Z * Z)})"}
-- @carbonluau-api {"Owner":"CommandContext","Name":"Player","Kind":"Property","ValueType":"Player","Writable":false,"Summary":"CommandContext.Player.","SinceApi":"0.3.0-experimental","Binding":"        Callback(table.freeze({Player = Player, Name = Fields[6], Arguments = table.freeze(Arguments)}))"}
-- @carbonluau-api {"Owner":"CommandContext","Name":"Name","Kind":"Property","ValueType":"string","Writable":false,"Summary":"CommandContext.Name.","SinceApi":"0.3.0-experimental","Binding":"        Callback(table.freeze({Player = Player, Name = Fields[6], Arguments = table.freeze(Arguments)}))"}
-- @carbonluau-api {"Owner":"CommandContext","Name":"Arguments","Kind":"Property","ValueType":"{string}","Writable":false,"Summary":"CommandContext.Arguments.","SinceApi":"0.3.0-experimental","Binding":"        Callback(table.freeze({Player = Player, Name = Fields[6], Arguments = table.freeze(Arguments)}))"}
-- @carbonluau-api {"Owner":"CommandOptions","Name":"permission","Kind":"Property","ValueType":"string?","Writable":false,"Summary":"CommandOptions.permission.","SinceApi":"0.3.0-experimental","Binding":"        if Key ~= \"permission\" and Key ~= \"description\" then Fail(\"unsupported command option\") end"}
-- @carbonluau-api {"Owner":"CommandOptions","Name":"description","Kind":"Property","ValueType":"string?","Writable":false,"Summary":"CommandOptions.description.","SinceApi":"0.3.0-experimental","Binding":"        if Key ~= \"permission\" and Key ~= \"description\" then Fail(\"unsupported command option\") end"}
-- @carbonluau-api {"Owner":"Players","Name":"PlayerAdded","Kind":"Signal","Args":[["Player","Player"]],"Returns":[],"Summary":"Fires when a connection is added.","SinceApi":"0.3.0-experimental","Binding":"local Players = {PlayerAdded = Signal(\"added\"), PlayerRemoving = Signal(\"removing\")}"}
-- @carbonluau-api {"Owner":"Players","Name":"PlayerRemoving","Kind":"Signal","Args":[["Player","Player"]],"Returns":[],"Summary":"Fires when a connection is removed.","SinceApi":"0.3.0-experimental","Binding":"local Players = {PlayerAdded = Signal(\"added\"), PlayerRemoving = Signal(\"removing\")}"}
local Host, MakeUserdata, SharedValueEqual = ...
local Callbacks = {}
local Proxies = setmetatable({}, {__mode = "v"})
local Records = setmetatable({}, {__mode = "k"})
local function Fail(Message) error(Message, 3) end
local function String(Value, Label)
    if type(Value) ~= "string" then Fail("expected string for " .. Label) end
    return Value
end
local function Number(Value, Label, Minimum, Maximum)
    if type(Value) ~= "number" or Value ~= Value or Value == math.huge or Value == -math.huge then Fail("expected finite number for " .. Label) end
    if Value < Minimum or Value > Maximum then Fail(Label .. " is outside its allowed range") end
    return Value == 0 and 0 or Value
end
local function FiniteNumber(Value, Label)
    if type(Value) ~= "number" or Value ~= Value or Value == math.huge or Value == -math.huge then Fail("expected finite number for " .. Label) end
    return Value == 0 and 0 or Value
end
local MaxHostFloat = 3.4028234663852886e38
local ValueRecords = setmetatable({}, {__mode = "k"})
if SharedValueEqual == nil then
    SharedValueEqual = function(Left, Right)
        local Kind = tostring(Left)
        if Kind ~= tostring(Right) then return false end
        if Kind == "UDim" then return Left.Scale == Right.Scale and Left.Offset == Right.Offset end
        if Kind == "UDim2" then return Left.X == Right.X and Left.Y == Right.Y end
        if Kind == "Vector2" then return Left.X == Right.X and Left.Y == Right.Y end
        if Kind == "Vector3" then return Left.X == Right.X and Left.Y == Right.Y and Left.Z == Right.Z end
        if Kind == "Color3" then return Left.R == Right.R and Left.G == Right.G and Left.B == Right.B end
        if Kind == "ImageSource" then
            if Left.Kind ~= Right.Kind then return false end
            if Left.Kind == "None" then return true end
            if Left.Kind == "Sprite" then return Left.Name == Right.Name end
            if Left.Kind == "Png" then return Left.Id == Right.Id end
            if Left.Kind == "Item" then return Left.ItemId == Right.ItemId and Left.SkinId == Right.SkinId end
            if Left.Kind == "SteamAvatar" then return Left.UserId == Right.UserId end
            return false
        end
        if string.sub(Kind, 1, 8) == "GuiFont." then return true end
        if Kind == "GiveItemBehavior.InventoryOnly" then return true end
        return false
    end
end
local ValueMetas = {}
local function ValueMeta(Kind, Configure)
    local Meta = {}
    Meta.__index = function(Value, Key)
        local RecordValue = ValueRecords[Value]
        if not RecordValue then Fail("invalid " .. Kind .. " value") end
        local Result = RecordValue[Key]
        if Result == nil then Fail("unknown " .. Kind .. " field") end
        return Result
    end
    Meta.__newindex = function() Fail(Kind .. " fields are read-only") end
    Meta.__tostring = function() return Kind end
    Meta.__eq = SharedValueEqual
    Meta.__metatable = "CarbonLuau." .. Kind
    if Configure then Configure(Meta) end
    ValueMetas[Kind] = table.freeze(Meta)
end
for _, Kind in {"UDim", "UDim2", "Vector2", "Color3", "ImageSource"} do ValueMeta(Kind) end
local function Value(Kind, RecordValue)
    local Result = MakeUserdata(ValueMetas[Kind])
    ValueRecords[Result] = RecordValue
    return Result
end
local UDim = {}
-- @carbonluau-api {"Owner":"UDim","Name":"new","Kind":"Constructor","Args":[["Scale","number"],["Offset","number"]],"Returns":["UDim"],"Summary":"UDim.new.","Preview":"Pure","Binding":"function UDim.new(Scale, Offset)"}
function UDim.new(Scale, Offset)
    return Value("UDim", {Scale = Number(Scale, "UDim.Scale", -8, 8), Offset = Number(Offset, "UDim.Offset", -32768, 32768)})
end
local UDim2 = {}
-- @carbonluau-api {"Owner":"UDim2","Name":"new","Kind":"Constructor","Args":[["XScale","number"],["XOffset","number"],["YScale","number"],["YOffset","number"]],"Returns":["UDim2"],"Summary":"UDim2.new.","Preview":"Pure","Binding":"function UDim2.new(XScale, XOffset, YScale, YOffset)"}
function UDim2.new(XScale, XOffset, YScale, YOffset)
    return Value("UDim2", {X = UDim.new(XScale, XOffset), Y = UDim.new(YScale, YOffset)})
end
-- @carbonluau-api {"Owner":"UDim2","Name":"fromScale","Kind":"Constructor","Args":[["XScale","number"],["YScale","number"]],"Returns":["UDim2"],"Summary":"UDim2.fromScale.","Preview":"Pure","Binding":"function UDim2.fromScale(XScale, YScale) return UDim2.new(XScale, 0, YScale, 0) end"}
function UDim2.fromScale(XScale, YScale) return UDim2.new(XScale, 0, YScale, 0) end
-- @carbonluau-api {"Owner":"UDim2","Name":"fromOffset","Kind":"Constructor","Args":[["XOffset","number"],["YOffset","number"]],"Returns":["UDim2"],"Summary":"UDim2.fromOffset.","Preview":"Pure","Binding":"function UDim2.fromOffset(XOffset, YOffset) return UDim2.new(0, XOffset, 0, YOffset) end"}
function UDim2.fromOffset(XOffset, YOffset) return UDim2.new(0, XOffset, 0, YOffset) end
local Vector2 = {}
-- @carbonluau-api {"Owner":"Vector2","Name":"new","Kind":"Constructor","Args":[["X","number"],["Y","number"]],"Returns":["Vector2"],"Summary":"Vector2.new.","Preview":"Pure","Binding":"function Vector2.new(X, Y)"}
function Vector2.new(X, Y)
    return Value("Vector2", {X = Number(X, "Vector2.X", -32768, 32768), Y = Number(Y, "Vector2.Y", -32768, 32768)})
end
local Vector3
local function IsVector3(Value)
    return typeof(Value) == "userdata" and tostring(Value) == "Vector3"
end
local function Vector3Components(Value)
    if not IsVector3(Value) then Fail("expected Vector3") end
    return Value.X, Value.Y, Value.Z
end
ValueMeta("Vector3", function(Meta)
-- @carbonluau-api {"Owner":"Vector3","Name":"__add","Kind":"Operator","Args":[["Left","Vector3"],["Right","Vector3"]],"Returns":["Vector3"],"Summary":"Immutable Vector3 __add operator.","Binding":"    Meta.__add = function(Left, Right)","Preview":"Pure"}
    Meta.__add = function(Left, Right)
        local LX, LY, LZ = Vector3Components(Left)
        local RX, RY, RZ = Vector3Components(Right)
        return Vector3.new(LX + RX, LY + RY, LZ + RZ)
    end
-- @carbonluau-api {"Owner":"Vector3","Name":"__sub","Kind":"Operator","Args":[["Left","Vector3"],["Right","Vector3"]],"Returns":["Vector3"],"Summary":"Immutable Vector3 __sub operator.","Binding":"    Meta.__sub = function(Left, Right)","Preview":"Pure"}
    Meta.__sub = function(Left, Right)
        local LX, LY, LZ = Vector3Components(Left)
        local RX, RY, RZ = Vector3Components(Right)
        return Vector3.new(LX - RX, LY - RY, LZ - RZ)
    end
-- @carbonluau-api {"Owner":"Vector3","Name":"__unm","Kind":"Operator","Args":[["Operand","Vector3"]],"Returns":["Vector3"],"Summary":"Immutable Vector3 __unm operator.","Binding":"    Meta.__unm = function(Operand)","Preview":"Pure"}
    Meta.__unm = function(Operand)
        local X, Y, Z = Vector3Components(Operand)
        return Vector3.new(-X, -Y, -Z)
    end
-- @carbonluau-api {"Owner":"Vector3","Name":"__mul","Kind":"Operator","Args":[["Left","Vector3"],["Right","number"]],"Returns":["Vector3"],"Summary":"Immutable Vector3 __mul operator.","Binding":"    Meta.__mul = function(Left, Right)","Preview":"Pure","Overloads":[[["Left","Vector3"],["Right","number"]],[["Left","number"],["Right","Vector3"]]]}
    Meta.__mul = function(Left, Right)
        if IsVector3(Left) then
            local X, Y, Z = Vector3Components(Left)
            local Scalar = FiniteNumber(Right, "Vector3 scalar")
            return Vector3.new(X * Scalar, Y * Scalar, Z * Scalar)
        end
        local Scalar = FiniteNumber(Left, "Vector3 scalar")
        local X, Y, Z = Vector3Components(Right)
        return Vector3.new(Scalar * X, Scalar * Y, Scalar * Z)
    end
-- @carbonluau-api {"Owner":"Vector3","Name":"__div","Kind":"Operator","Args":[["Left","Vector3"],["Right","number"]],"Returns":["Vector3"],"Summary":"Immutable Vector3 __div operator.","Binding":"    Meta.__div = function(Left, Right)","Preview":"Pure"}
    Meta.__div = function(Left, Right)
        local X, Y, Z = Vector3Components(Left)
        local Scalar = FiniteNumber(Right, "Vector3 scalar")
        if Scalar == 0 then Fail("Vector3 division by zero") end
        return Vector3.new(X / Scalar, Y / Scalar, Z / Scalar)
    end
end)
Vector3 = {}
-- @carbonluau-api {"Owner":"Vector3","Name":"new","Kind":"Constructor","Args":[["X","number"],["Y","number"],["Z","number"]],"Returns":["Vector3"],"Summary":"Vector3.new.","Preview":"Pure","Binding":"function Vector3.new(X, Y, Z)"}
function Vector3.new(X, Y, Z)
    X = Number(X, "Vector3.X", -MaxHostFloat, MaxHostFloat)
    Y = Number(Y, "Vector3.Y", -MaxHostFloat, MaxHostFloat)
    Z = Number(Z, "Vector3.Z", -MaxHostFloat, MaxHostFloat)
    return Value("Vector3", {X = X, Y = Y, Z = Z, Magnitude = math.sqrt(X * X + Y * Y + Z * Z)})
end
local Color3 = {}
-- @carbonluau-api {"Owner":"Color3","Name":"new","Kind":"Constructor","Args":[["R","number"],["G","number"],["B","number"]],"Returns":["Color3"],"Summary":"Color3.new.","Preview":"Pure","Binding":"function Color3.new(R, G, B)"}
function Color3.new(R, G, B)
    return Value("Color3", {R = Number(R, "Color3.R", 0, 1), G = Number(G, "Color3.G", 0, 1), B = Number(B, "Color3.B", 0, 1)})
end
-- @carbonluau-api {"Owner":"Color3","Name":"fromRGB","Kind":"Constructor","Args":[["R","number"],["G","number"],["B","number"]],"Returns":["Color3"],"Summary":"Color3.fromRGB.","Preview":"Pure","Binding":"function Color3.fromRGB(R, G, B)"}
function Color3.fromRGB(R, G, B)
    R, G, B = Number(R, "Color3.RGB", 0, 255), Number(G, "Color3.RGB", 0, 255), Number(B, "Color3.RGB", 0, 255)
    if R % 1 ~= 0 or G % 1 ~= 0 or B % 1 ~= 0 then Fail("Color3.fromRGB expects integer components") end
    return Color3.new(R / 255, G / 255, B / 255)
end
local function DecimalId(Value, Label)
    Value = String(Value, Label)
    if #Value == 0 or #Value > 20 or (#Value > 1 and string.sub(Value, 1, 1) == "0") or string.find(Value, "[^0-9]") then
        Fail(Label .. " must be a canonical decimal string of at most 20 digits")
    end
    return Value
end
local function UnsignedDecimalId(Value, Label)
    Value = DecimalId(Value, Label)
    if #Value == 20 and Value > "18446744073709551615" then Fail(Label .. " exceeds the unsigned 64-bit range") end
    return Value
end
local ImageSource = {}
-- @carbonluau-api {"Owner":"ImageSource","Name":"None","Kind":"Constructor","Args":[],"Returns":["ImageSource"],"Summary":"ImageSource.None.","Preview":"Pure","Binding":"function ImageSource.None() return Value(\"ImageSource\", {Kind = \"None\"}) end"}
function ImageSource.None() return Value("ImageSource", {Kind = "None"}) end
-- @carbonluau-api {"Owner":"ImageSource","Name":"Sprite","Kind":"Constructor","Args":[["Name","string"]],"Returns":["ImageSource"],"Summary":"ImageSource.Sprite.","Preview":"Pure","Binding":"function ImageSource.Sprite(Name)"}
function ImageSource.Sprite(Name)
    Name = String(Name, "ImageSource.Sprite name")
    if #Name == 0 or #Name > 256 or string.sub(Name, 1, 1) == "/" or string.sub(Name, -1) == "/" or
        string.find(Name, "//", 1, true) or string.find(Name, "[^%w_./%-]") then Fail("ImageSource.Sprite name is not a canonical asset key") end
    for Segment in string.gmatch(Name, "[^/]+") do if Segment == "." or Segment == ".." then Fail("ImageSource.Sprite name is not a canonical asset key") end end
    return Value("ImageSource", {Kind = "Sprite", Name = Name})
end
-- @carbonluau-api {"Owner":"ImageSource","Name":"Png","Kind":"Constructor","Args":[["Id","string"]],"Returns":["ImageSource"],"Summary":"ImageSource.Png.","Preview":"Pure","Binding":"function ImageSource.Png(Id) return Value(\"ImageSource\", {Kind = \"Png\", Id = DecimalId(Id, \"PNG identifier\")}) end"}
function ImageSource.Png(Id) return Value("ImageSource", {Kind = "Png", Id = DecimalId(Id, "PNG identifier")}) end
-- @carbonluau-api {"Owner":"ImageSource","Name":"Item","Kind":"Constructor","Args":[["ItemId","number"],["SkinId","string?"]],"Returns":["ImageSource"],"Summary":"ImageSource.Item.","Preview":"Pure","Binding":"function ImageSource.Item(ItemId, SkinId)"}
function ImageSource.Item(ItemId, SkinId)
    ItemId = Number(ItemId, "ImageSource.Item ItemId", -2147483648, 2147483647)
    if ItemId % 1 ~= 0 then Fail("ImageSource.Item ItemId must be an integer") end
    return Value("ImageSource", {Kind = "Item", ItemId = ItemId,
        SkinId = SkinId == nil and "" or UnsignedDecimalId(SkinId, "skin identifier")})
end
-- @carbonluau-api {"Owner":"ImageSource","Name":"SteamAvatar","Kind":"Constructor","Args":[["UserId","string"]],"Returns":["ImageSource"],"Summary":"ImageSource.SteamAvatar.","Preview":"Pure","Binding":"function ImageSource.SteamAvatar(UserId)"}
function ImageSource.SteamAvatar(UserId)
    return Value("ImageSource", {Kind = "SteamAvatar", UserId = UnsignedDecimalId(UserId, "Steam user identifier")})
end
local GuiFontRecords = setmetatable({}, {__mode = "k"})
local GuiFontMeta = {}
GuiFontMeta.__index = function() Fail("GuiFont has no fields") end
GuiFontMeta.__newindex = function() Fail("GuiFont values are immutable") end
GuiFontMeta.__tostring = function(Value)
    local Identity = GuiFontRecords[Value]
    if not Identity then Fail("invalid GuiFont value") end
    return "GuiFont." .. Identity
end
GuiFontMeta.__eq = SharedValueEqual
GuiFontMeta.__metatable = "CarbonLuau.GuiFont"
table.freeze(GuiFontMeta)
local function Font(Identity)
    local Result = MakeUserdata(GuiFontMeta)
    GuiFontRecords[Result] = Identity
    return Result
end
local GuiFont = {
-- @carbonluau-api {"Owner":"GuiFont","Name":"RobotoCondensedRegular","Kind":"Singleton","ValueType":"GuiFont","Writable":false,"Summary":"Immutable font identity; client rasterization is unqualified.","Binding":"    RobotoCondensedRegular = Font(\"RobotoCondensedRegular\"),"}
    RobotoCondensedRegular = Font("RobotoCondensedRegular"),
-- @carbonluau-api {"Owner":"GuiFont","Name":"RobotoCondensedBold","Kind":"Singleton","ValueType":"GuiFont","Writable":false,"Summary":"Immutable font identity; client rasterization is unqualified.","Binding":"    RobotoCondensedBold = Font(\"RobotoCondensedBold\"),"}
    RobotoCondensedBold = Font("RobotoCondensedBold"),
-- @carbonluau-api {"Owner":"GuiFont","Name":"DroidSansMono","Kind":"Singleton","ValueType":"GuiFont","Writable":false,"Summary":"Immutable font identity; client rasterization is unqualified.","Binding":"    DroidSansMono = Font(\"DroidSansMono\"),"}
    DroidSansMono = Font("DroidSansMono"),
-- @carbonluau-api {"Owner":"GuiFont","Name":"PermanentMarker","Kind":"Singleton","ValueType":"GuiFont","Writable":false,"Summary":"Immutable font identity; client rasterization is unqualified.","Binding":"    PermanentMarker = Font(\"PermanentMarker\"),"}
    PermanentMarker = Font("PermanentMarker"),
}
table.freeze(UDim); table.freeze(UDim2); table.freeze(Vector2); table.freeze(Vector3); table.freeze(Color3); table.freeze(ImageSource); table.freeze(GuiFont)
-- @carbonluau-api {"Type":"GiveItemBehavior","Kind":"Enum","Summary":"Immutable inventory placement behavior."}
local GiveItemBehaviorMeta = table.freeze({
    __index = function() Fail("GiveItemBehavior has no fields") end,
    __newindex = function() Fail("GiveItemBehavior values are immutable") end,
    __tostring = function() return "GiveItemBehavior.InventoryOnly" end,
    __eq = SharedValueEqual,
    __metatable = "CarbonLuau.GiveItemBehavior",
})
-- @carbonluau-api {"Owner":"GiveItemBehavior","Name":"InventoryOnly","Kind":"Singleton","ValueType":"GiveItemBehavior","Writable":false,"Summary":"Place only in inventory; never intentionally drop overflow.","Binding":"local GiveItemBehavior = table.freeze({InventoryOnly = MakeUserdata(GiveItemBehaviorMeta)})"}
local GiveItemBehavior = table.freeze({InventoryOnly = MakeUserdata(GiveItemBehaviorMeta)})
local function Record(Player)
    local Value = Records[Player]
    if not Value then Fail("invalid Player proxy") end
    return Value
end
local PlayerMethods = {}
-- @carbonluau-api {"Owner":"Player","Name":"SendMessage","Kind":"Method","Args":[["Message","string"]],"Returns":[],"Summary":"Send bounded chat text to this connected player.","Preview":"HostMutation","Binding":"function PlayerMethods.SendMessage(Player, Message)","SinceApi":"0.3.0-experimental"}
function PlayerMethods.SendMessage(Player, Message)
    local Value = Record(Player)
    Host(4, Value.Token, Value.UserId, String(Message, "message"))
end
-- @carbonluau-api {"Owner":"Player","Name":"HasPermission","Kind":"Method","Args":[["Permission","string"]],"Returns":["boolean"],"Summary":"Check a host permission for this exact player connection.","Preview":"ReadMock","Binding":"function PlayerMethods.HasPermission(Player, Permission)","SinceApi":"0.3.0-experimental"}
function PlayerMethods.HasPermission(Player, Permission)
    local Value = Record(Player)
    return Host(5, Value.Token, Value.UserId, String(Permission, "permission"))[1] == "1"
end
local function GetPlayer(Token, UserId, Name)
    if Proxies[Token] then return Proxies[Token] end
    local Value = {Token = Token, UserId = UserId, Name = Name}
    local Player = {}
    Records[Player] = Value
    local Meta = {
        __tostring = function() return "Player" end,
        __index = function(_, Key)
            local Info = Host(3, Token, UserId)
            if Key == "Name" then return Info[1] == "1" and Info[2] or Name end
            if Key == "UserId" then return UserId end
            if Key == "IsConnected" then return Info[1] == "1" end
            if Key == "Position" then
                local Position = Host(22, Token, UserId)
                return Vector3.new(tonumber(Position[1]), tonumber(Position[2]), tonumber(Position[3]))
            end
            if Key == "Health" then return tonumber(Host(23, Token, UserId)[1]) end
            if Key == "MaxHealth" then return tonumber(Host(24, Token, UserId)[1]) end
            return PlayerMethods[Key]
        end,
    }
    setmetatable(Player, Meta)
    table.freeze(Player)
    Meta.__metatable = "CarbonLuau.Player"
    table.freeze(Meta)
    Proxies[Token] = Player
    return Player
end
local function Signal(Kind)
    local Value = {}
    function Value.Connect(Self, Callback)
        if Self ~= Value or type(Callback) ~= "function" then Fail("Signal:Connect expects a function") end
        local Id = Host(6, Kind)[1]
        Callbacks[Id] = Callback
        local Connection = {}
        function Connection.Disconnect(SelfConnection)
            if SelfConnection ~= Connection then Fail("invalid Connection") end
            Host(7, Id)
            Callbacks[Id] = nil
        end
        return table.freeze(Connection)
    end
    return table.freeze(Value)
end
local Players = {PlayerAdded = Signal("added"), PlayerRemoving = Signal("removing")}
-- @carbonluau-api {"Owner":"Players","Name":"GetPlayers","Kind":"Method","Args":[],"Returns":["{Player}"],"Summary":"Return the currently connected Player proxies.","Preview":"ReadMock","Binding":"function Players.GetPlayers(Self)","SinceApi":"0.3.0-experimental"}
function Players.GetPlayers(Self)
    if Self ~= Players then Fail("invalid Players service") end
    local Fields = Host(1)
    local Result = {}
    for Index = 1, #Fields, 3 do Result[#Result + 1] = GetPlayer(Fields[Index], Fields[Index + 1], Fields[Index + 2]) end
    return Result
end
-- @carbonluau-api {"Owner":"Players","Name":"GetPlayerByUserId","Kind":"Method","Args":[["UserId","string"]],"Returns":["Player?"],"Summary":"Find a connected player by decimal user ID, or nil.","Preview":"ReadMock","Binding":"function Players.GetPlayerByUserId(Self, UserId)","SinceApi":"0.3.0-experimental"}
function Players.GetPlayerByUserId(Self, UserId)
    if Self ~= Players then Fail("invalid Players service") end
    local Fields = Host(2, String(UserId, "userId"))
    if #Fields == 0 then return nil end
    return GetPlayer(Fields[1], Fields[2], Fields[3])
end
table.freeze(Players)
local Commands = {}
-- @carbonluau-api {"Owner":"Commands","Name":"Register","Kind":"Method","Args":[["Name","string"],["Options","CommandOptions"],["Callback","(CommandContext) -> ()"]],"Returns":[],"Summary":"Register a bounded command with optional permission and description.","Preview":"Pure","Binding":"function Commands.Register(Self, Name, Options, Callback)","SinceApi":"0.3.0-experimental"}
function Commands.Register(Self, Name, Options, Callback)
    if Self ~= Commands or type(Options) ~= "table" or type(Callback) ~= "function" then Fail("Commands:Register expects name, options and callback") end
    for Key in Options do
        if Key ~= "permission" and Key ~= "description" then Fail("unsupported command option") end
    end
    Name = String(Name, "command name")
    local Permission = Options.permission == nil and "" or String(Options.permission, "permission")
    local Description = Options.description == nil and "" or String(Options.description, "description")
    local Id = Host(8, Name, Permission, Description)[1]
    Callbacks[Id] = Callback
end
table.freeze(Commands)
local GuiObjectRecords = setmetatable({}, {__mode = "k"})
local GuiObjectProxies = setmetatable({}, {__mode = "v"})
local GuiSignals = setmetatable({}, {__mode = "v"})
local GuiObjectMethods = {}
local GuiObjectMeta = {}
local GetGuiObject
local function GuiRecord(Value)
    local Result = GuiObjectRecords[Value]
    if not Result then Fail("invalid GUI object") end
    return Result
end
-- @carbonluau-api {"Owner":"Player","Name":"Teleport","Kind":"Method","Args":[["Position","Vector3"]],"Returns":[],"Summary":"Teleport a connected player after committed-only admission.","Preview":"HostMutation","Binding":"function PlayerMethods.Teleport(Player, Position)","Qualification":"ClientUnqualified"}
function PlayerMethods.Teleport(Player, Position)
    local Value = Record(Player)
    local X, Y, Z = Vector3Components(Position)
    Host(28, Value.Token, Value.UserId, tostring(X), tostring(Y), tostring(Z))
end
-- @carbonluau-api {"Owner":"Player","Name":"CountItem","Kind":"Method","Args":[["ShortName","string"]],"Returns":["number"],"Summary":"Count observed physical inventory items by short name.","Preview":"ReadMock","Binding":"function PlayerMethods.CountItem(Player, ShortName)"}
function PlayerMethods.CountItem(Player, ShortName)
    local Value = Record(Player)
    return tonumber(Host(26, Value.Token, Value.UserId, String(ShortName, "item short name"))[1])
end
-- @carbonluau-api {"Owner":"Player","Name":"HasItem","Kind":"Method","Args":[["ShortName","string"],["Amount","number?"]],"Returns":["boolean"],"Summary":"Check physical inventory for an amount; the default is one.","Preview":"ReadMock","Binding":"function PlayerMethods.HasItem(Player, ShortName, Amount)"}
function PlayerMethods.HasItem(Player, ShortName, Amount)
    local Value = Record(Player)
    String(ShortName, "item short name")
    if Amount == nil then Amount = 1 end
    Number(Amount, "item amount", 1, 9007199254740991)
    if Amount ~= math.floor(Amount) then Fail("item amount must be an exact integer") end
    return Host(27, Value.Token, Value.UserId, ShortName, string.format("%.0f", Amount))[1] == "1"
end
-- @carbonluau-api {"Owner":"Player","Name":"TakeItem","Kind":"Method","Args":[["ShortName","string"],["Amount","number"]],"Returns":["boolean"],"Summary":"Remove a bounded amount with physical verification; indeterminate mutation raises an error.","Preview":"HostMutation","Binding":"function PlayerMethods.TakeItem(Player, ShortName, Amount)","Qualification":"ClientUnqualified"}
function PlayerMethods.TakeItem(Player, ShortName, Amount)
    local Value = Record(Player)
    String(ShortName, "item short name")
    Number(Amount, "item amount", 1, 2147483647)
    if Amount ~= math.floor(Amount) then Fail("item amount must be an exact integer") end
    return Host(29, Value.Token, Value.UserId, ShortName, string.format("%.0f", Amount))[1] == "1"
end
-- @carbonluau-api {"Owner":"Player","Name":"GiveItem","Kind":"Method","Args":[["ShortName","string"],["Amount","number"],["Behavior","GiveItemBehavior?"]],"Returns":["boolean"],"Summary":"Give a bounded amount with physical verification. Behavior defaults to InventoryOnly; indeterminate mutation raises an error.","Preview":"HostMutation","Binding":"function PlayerMethods.GiveItem(Player, ShortName, Amount, Behavior)","Qualification":"ClientUnqualified"}
function PlayerMethods.GiveItem(Player, ShortName, Amount, Behavior)
    local Value = Record(Player)
    String(ShortName, "item short name")
    Number(Amount, "item amount", 1, 2147483647)
    if Amount ~= math.floor(Amount) then Fail("item amount must be an exact integer") end
    if Behavior == nil then Behavior = GiveItemBehavior.InventoryOnly end
    if type(Behavior) ~= "userdata" or getmetatable(Behavior) ~= "CarbonLuau.GiveItemBehavior" or
        tostring(Behavior) ~= "GiveItemBehavior.InventoryOnly" then Fail("expected GiveItemBehavior.InventoryOnly") end
    return Host(30, Value.Token, Value.UserId, ShortName, string.format("%.0f", Amount), "InventoryOnly")[1] == "1"
end
local function EncodeGuiValue(Value, Integer)
    local Kind = type(Value)
    if Kind == "string" then return "string", Value end
    if Kind == "boolean" then return "boolean", Value and "1" or "0" end
    if Kind == "number" then
        Number(Value, "GUI property", -1.7976931348623157e308, 1.7976931348623157e308)
        if Integer and Value % 1 ~= 0 then Fail("GUI property expects an integer") end
        return Integer and "integer" or "number", tostring(Value == 0 and 0 or Value)
    end
    if typeof(Value) == "userdata" then
        local ValueKind = tostring(Value)
        if ValueKind == "UDim" then return "udim", tostring(Value.Scale), tostring(Value.Offset) end
        if ValueKind == "UDim2" then return "udim2", tostring(Value.X.Scale), tostring(Value.X.Offset), tostring(Value.Y.Scale), tostring(Value.Y.Offset) end
        if ValueKind == "Vector2" then return "vector2", tostring(Value.X), tostring(Value.Y) end
        if ValueKind == "Color3" then return "color3", tostring(Value.R), tostring(Value.G), tostring(Value.B) end
        if ValueKind == "ImageSource" then
            if Value.Kind == "None" then return "imagesource", "None" end
            if Value.Kind == "Sprite" then return "imagesource", "Sprite", Value.Name end
            if Value.Kind == "Png" then return "imagesource", "Png", Value.Id end
            if Value.Kind == "SteamAvatar" then return "imagesource", "SteamAvatar", Value.UserId end
            if Value.Kind == "Item" then return "imagesource", "Item", tostring(Value.ItemId), Value.SkinId end
        end
        if string.sub(ValueKind, 1, 8) == "GuiFont." then return "guifont", string.sub(ValueKind, 9) end
    end
    Fail("unsupported GUI property value")
end
local function DecodeGuiValue(Fields)
    local Kind = Fields[1]
    if Kind == "nil" then return nil end
    if Kind == "string" then return Fields[2] end
    if Kind == "boolean" then return Fields[2] == "1" end
    if Kind == "integer" or Kind == "number" then return tonumber(Fields[2]) end
    if Kind == "udim" then return UDim.new(tonumber(Fields[2]), tonumber(Fields[3])) end
    if Kind == "udim2" then return UDim2.new(tonumber(Fields[2]), tonumber(Fields[3]), tonumber(Fields[4]), tonumber(Fields[5])) end
    if Kind == "vector2" then return Vector2.new(tonumber(Fields[2]), tonumber(Fields[3])) end
    if Kind == "color3" then return Color3.new(tonumber(Fields[2]), tonumber(Fields[3]), tonumber(Fields[4])) end
    if Kind == "imagesource" then
        if Fields[2] == "None" then return ImageSource.None() end
        if Fields[2] == "Sprite" then return ImageSource.Sprite(Fields[3]) end
        if Fields[2] == "Png" then return ImageSource.Png(Fields[3]) end
        if Fields[2] == "Item" then return ImageSource.Item(tonumber(Fields[3]), Fields[4] == "" and nil or Fields[4]) end
        if Fields[2] == "SteamAvatar" then return ImageSource.SteamAvatar(Fields[3]) end
    end
    if Kind == "guifont" then
        local Result = GuiFont[Fields[2]]
        if Result == nil then Fail("invalid GUI font identity") end
        return Result
    end
    if Kind == "object" then return GetGuiObject(Fields[2], Fields[3]) end
    Fail("invalid GUI host value")
end
local function GuiSignal(Object)
    local RecordValue = GuiRecord(Object)
    Host(20, "event", RecordValue.Id, "Activated")
    if GuiSignals[RecordValue.Id] then return GuiSignals[RecordValue.Id] end
    local SignalValue = {}
    function SignalValue.Connect(Self, Callback)
        if Self ~= SignalValue or type(Callback) ~= "function" then Fail("Signal:Connect expects a function") end
        local Id = Host(21, "connect", RecordValue.Id)[1]
        Callbacks[Id] = Callback
        local Active = true
        local Connection = {}
        function Connection.Disconnect(SelfConnection)
            if SelfConnection ~= Connection then Fail("invalid Connection") end
            if Active then Active = false; Host(21, "disconnect", RecordValue.Id, Id); Callbacks[Id] = nil end
        end
        return table.freeze(Connection)
    end
    GuiSignals[RecordValue.Id] = table.freeze(SignalValue)
    return GuiSignals[RecordValue.Id]
end
-- @carbonluau-api {"Owner":"$Gui","Name":"Create","Kind":"Method","Args":[["ClassName","string"]],"Returns":["GuiNode"],"Summary":"Create a retained GUI object of the named supported class.","Preview":"Pure","Binding":"function GuiObjectMethods.Create(Self, ClassName)"}
function GuiObjectMethods.Create(Self, ClassName)
    local RecordValue = GuiRecord(Self)
    local Fields = Host(21, "create", RecordValue.Id, String(ClassName, "GUI class name"))
    return GetGuiObject(Fields[1], Fields[2])
end
-- @carbonluau-api {"Owner":"$Gui","Name":"GetChildren","Kind":"Method","Args":[],"Returns":["{GuiNode}"],"Summary":"Return retained direct children in order.","Preview":"Pure","Binding":"function GuiObjectMethods.GetChildren(Self)"}
function GuiObjectMethods.GetChildren(Self)
    local Fields = Host(20, "children", GuiRecord(Self).Id)
    local Result = {}
    for Index = 1, #Fields, 2 do Result[#Result + 1] = GetGuiObject(Fields[Index], Fields[Index + 1]) end
    return Result
end
-- @carbonluau-api {"Owner":"$Gui","Name":"FindFirstChild","Kind":"Method","Args":[["Name","string"]],"Returns":["GuiNode?"],"Summary":"Find a named direct child, or nil.","Preview":"Pure","Binding":"function GuiObjectMethods.FindFirstChild(Self, Name)"}
function GuiObjectMethods.FindFirstChild(Self, Name)
    local Fields = Host(20, "find", GuiRecord(Self).Id, String(Name, "child name"))
    return #Fields == 0 and nil or GetGuiObject(Fields[1], Fields[2])
end
-- @carbonluau-api {"Owner":"$Gui","Name":"IsA","Kind":"Method","Args":[["ClassName","string"]],"Returns":["boolean"],"Summary":"Check the canonical GUI class inheritance.","Preview":"Pure","Binding":"function GuiObjectMethods.IsA(Self, ClassName)"}
function GuiObjectMethods.IsA(Self, ClassName)
    return Host(20, "isa", GuiRecord(Self).Id, String(ClassName, "GUI class name"))[1] == "1"
end
-- @carbonluau-api {"Owner":"$Gui","Name":"Clone","Kind":"Method","Args":[],"Returns":["GuiNode"],"Summary":"Clone this retained subtree within canonical bounds.","Preview":"Pure","Binding":"function GuiObjectMethods.Clone(Self)"}
function GuiObjectMethods.Clone(Self)
    local Fields = Host(21, "clone", GuiRecord(Self).Id)
    return GetGuiObject(Fields[1], Fields[2])
end
-- @carbonluau-api {"Owner":"$Gui","Name":"Destroy","Kind":"Method","Args":[],"Returns":[],"Summary":"Destroy this retained object and its owned subtree.","Preview":"Pure","Binding":"function GuiObjectMethods.Destroy(Self)"}
function GuiObjectMethods.Destroy(Self)
    local Fields = Host(21, "destroy", GuiRecord(Self).Id)
    for _, Id in Fields do Callbacks[Id] = nil end
end
-- @carbonluau-api {"Owner":"$Gui","Name":"Show","Kind":"Method","Args":[["Player","Player"]],"Returns":[],"Summary":"Show this screen to an exact connected player.","Preview":"Pure","Binding":"function GuiObjectMethods.Show(Self, Player)"}
function GuiObjectMethods.Show(Self, Player)
    local RecordValue = GuiRecord(Self); local PlayerValue = Record(Player)
    Host(21, "show", RecordValue.Id, PlayerValue.Token, PlayerValue.UserId)
end
-- @carbonluau-api {"Owner":"$Gui","Name":"Hide","Kind":"Method","Args":[["Player","Player"]],"Returns":[],"Summary":"Hide this screen from an exact connected player.","Preview":"Pure","Binding":"function GuiObjectMethods.Hide(Self, Player)"}
function GuiObjectMethods.Hide(Self, Player)
    local RecordValue = GuiRecord(Self); local PlayerValue = Record(Player)
    Host(21, "hide", RecordValue.Id, PlayerValue.Token, PlayerValue.UserId)
end
-- @carbonluau-api {"Owner":"$Gui","Name":"IsShown","Kind":"Method","Args":[["Player","Player"]],"Returns":["boolean"],"Summary":"Check whether this screen has a presentation for the player.","Preview":"Pure","Binding":"function GuiObjectMethods.IsShown(Self, Player)"}
function GuiObjectMethods.IsShown(Self, Player)
    local RecordValue = GuiRecord(Self); local PlayerValue = Record(Player)
    return Host(20, "shown", RecordValue.Id, PlayerValue.Token, PlayerValue.UserId)[1] == "1"
end
local function ScrollPlayer(Self, Player)
    local RecordValue = GuiRecord(Self)
    if RecordValue.ClassName ~= "ScrollingFrame" then Fail("scroll methods require ScrollingFrame") end
    return RecordValue, Record(Player)
end
-- @carbonluau-api {"Owner":"$Gui","Name":"ScrollTo","Kind":"Method","Args":[["Player","Player"],["Position","Vector2"]],"Returns":[],"Summary":"Queue bounded presentation-local scroll intent for the player.","Preview":"Pure","Binding":"function GuiObjectMethods.ScrollTo(Self, Player, Position)"}
function GuiObjectMethods.ScrollTo(Self, Player, Position)
    local RecordValue, PlayerValue = ScrollPlayer(Self, Player)
    if typeof(Position) ~= "userdata" or tostring(Position) ~= "Vector2" then Fail("ScrollTo expects Vector2") end
    local X = Number(Position.X, "scroll X", 0, 1)
    local Y = Number(Position.Y, "scroll Y", 0, 1)
    Host(21, "scroll", RecordValue.Id, PlayerValue.Token, PlayerValue.UserId, tostring(X), tostring(Y))
end
-- @carbonluau-api {"Owner":"$Gui","Name":"ScrollToTop","Kind":"Method","Args":[["Player","Player"]],"Returns":[],"Summary":"Queue scroll intent to the top for the player.","Preview":"Pure","Binding":"function GuiObjectMethods.ScrollToTop(Self, Player)"}
function GuiObjectMethods.ScrollToTop(Self, Player)
    local RecordValue, PlayerValue = ScrollPlayer(Self, Player)
    Host(21, "scrolltop", RecordValue.Id, PlayerValue.Token, PlayerValue.UserId)
end
-- @carbonluau-api {"Owner":"$Gui","Name":"ScrollToBottom","Kind":"Method","Args":[["Player","Player"]],"Returns":[],"Summary":"Queue scroll intent to the bottom for the player.","Preview":"Pure","Binding":"function GuiObjectMethods.ScrollToBottom(Self, Player)"}
function GuiObjectMethods.ScrollToBottom(Self, Player)
    local RecordValue, PlayerValue = ScrollPlayer(Self, Player)
    Host(21, "scrollbottom", RecordValue.Id, PlayerValue.Token, PlayerValue.UserId)
end
table.freeze(GuiObjectMethods)
GuiObjectMeta.__index = function(Object, Key)
    local RecordValue = GuiRecord(Object)
    if Key == "Activated" then return GuiSignal(Object) end
    if (Key == "Show" or Key == "Hide" or Key == "IsShown") and RecordValue.ClassName ~= "ScreenGui" then
        return DecodeGuiValue(Host(20, "get", RecordValue.Id, String(Key, "GUI property name")))
    end
    if (Key == "ScrollTo" or Key == "ScrollToTop" or Key == "ScrollToBottom") and RecordValue.ClassName ~= "ScrollingFrame" then
        return DecodeGuiValue(Host(20, "get", RecordValue.Id, String(Key, "GUI property name")))
    end
    if GuiObjectMethods[Key] then return GuiObjectMethods[Key] end
    return DecodeGuiValue(Host(20, "get", RecordValue.Id, String(Key, "GUI property name")))
end
GuiObjectMeta.__newindex = function(Object, Key, Value)
    local RecordValue = GuiRecord(Object); Key = String(Key, "GUI property name")
    if Key == "Parent" then
        if Value == nil then Host(21, "set", RecordValue.Id, Key, "nil"); return end
        local Parent = GuiObjectRecords[Value]
        if not Parent then Fail("Parent expects a same-domain GUI object or nil") end
        Host(21, "set", RecordValue.Id, Key, "object", Parent.Id); return
    end
    Host(21, "set", RecordValue.Id, Key, EncodeGuiValue(Value, Key == "ZIndex" or Key == "TextSize" or Key == "LayoutOrder" or Key == "FillDirectionMaxCells"))
end
GuiObjectMeta.__tostring = function(Object) return GuiRecord(Object).ClassName end
GuiObjectMeta.__metatable = "CarbonLuau.GuiObject"
table.freeze(GuiObjectMeta)
GetGuiObject = function(Id, ClassName)
    if GuiObjectProxies[Id] then return GuiObjectProxies[Id] end
    local Object = MakeUserdata(GuiObjectMeta)
    GuiObjectRecords[Object] = {Id = Id, ClassName = ClassName}
    GuiObjectProxies[Id] = Object
    return Object
end
local Gui = {}
-- @carbonluau-api {"Owner":"Gui","Name":"Create","Kind":"Method","Args":[["ClassName","string"]],"Returns":["GuiNode"],"Summary":"Create a retained GUI object of the named supported class.","Preview":"Pure","Binding":"function Gui.Create(Self, ClassName)"}
function Gui.Create(Self, ClassName)
    if Self ~= Gui then Fail("invalid Gui service") end
    local Fields = Host(21, "create", "", String(ClassName, "GUI class name"))
    return GetGuiObject(Fields[1], Fields[2])
end
table.freeze(Gui)
local Items = {}
-- @carbonluau-api {"Owner":"Items","Name":"Exists","Kind":"Method","Args":[["ShortName","string"]],"Returns":["boolean"],"Summary":"Check whether a canonical item short name exists.","Preview":"ReadMock","Binding":"function Items.Exists(Self, ShortName)"}
function Items.Exists(Self, ShortName)
    if Self ~= Items then Fail("invalid Items service") end
    return Host(25, String(ShortName, "item short name"))[1] == "1"
end
table.freeze(Items)
local Game = {ApiName = "CarbonLuau", ApiVersion = "0.4.0-experimental", ApiStatus = "Experimental"}
-- @carbonluau-api {"Owner":"Game","Name":"GetService","Kind":"Method","Args":[["Name","string"]],"Returns":["Players | Commands | Gui | Items"],"Summary":"Get one of the explicitly supported CarbonLuau services.","Preview":"Pure","Binding":"function Game.GetService(Self, Name)","SinceApi":"0.3.0-experimental"}
function Game.GetService(Self, Name)
    if Self ~= Game then Fail("invalid game facade") end
    String(Name, "service name")
    if Name == "Players" then return Players end
    if Name == "Commands" then return Commands end
    if Name == "Gui" then return Gui end
    if Name == "Items" then return Items end
    Fail("unknown service (expected Players, Commands, Gui or Items)")
end
table.freeze(Game)
-- One host-admitted callback per scheduler work item; no dispatch fan-out in Lua.
local function Dispatch(Fields)
    local Callback = Callbacks[Fields[2]]
    if not Callback then return end
    local Player = GetPlayer(Fields[3], Fields[4], Fields[5])
    if Fields[1] == "command" then
        local Arguments = {}
        for Index = 7, #Fields do Arguments[#Arguments + 1] = Fields[Index] end
        Callback(table.freeze({Player = Player, Name = Fields[6], Arguments = table.freeze(Arguments)}))
    else Callback(Player) end
end
return Game, Dispatch, table.freeze({UDim = UDim, UDim2 = UDim2, Vector2 = Vector2, Vector3 = Vector3, Color3 = Color3, ImageSource = ImageSource, GuiFont = GuiFont, GiveItemBehavior = GiveItemBehavior}), SharedValueEqual
