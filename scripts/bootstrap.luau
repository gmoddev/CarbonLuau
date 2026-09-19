-- Trusted, build-embedded facade. Host/userdata construction are private.
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
local ValueRecords = setmetatable({}, {__mode = "k"})
if SharedValueEqual == nil then
    SharedValueEqual = function(Left, Right)
        local Kind = tostring(Left)
        if Kind ~= tostring(Right) then return false end
        if Kind == "UDim" then return Left.Scale == Right.Scale and Left.Offset == Right.Offset end
        if Kind == "UDim2" then return Left.X == Right.X and Left.Y == Right.Y end
        if Kind == "Vector2" then return Left.X == Right.X and Left.Y == Right.Y end
        if Kind == "Color3" then return Left.R == Right.R and Left.G == Right.G and Left.B == Right.B end
        return false
    end
end
local ValueMetas = {}
local function ValueMeta(Kind)
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
    ValueMetas[Kind] = table.freeze(Meta)
end
for _, Kind in {"UDim", "UDim2", "Vector2", "Color3"} do ValueMeta(Kind) end
local function Value(Kind, RecordValue)
    local Result = MakeUserdata(ValueMetas[Kind])
    ValueRecords[Result] = RecordValue
    return Result
end
local UDim = {}
function UDim.new(Scale, Offset)
    return Value("UDim", {Scale = Number(Scale, "UDim.Scale", -8, 8), Offset = Number(Offset, "UDim.Offset", -32768, 32768)})
end
local UDim2 = {}
function UDim2.new(XScale, XOffset, YScale, YOffset)
    return Value("UDim2", {X = UDim.new(XScale, XOffset), Y = UDim.new(YScale, YOffset)})
end
function UDim2.fromScale(XScale, YScale) return UDim2.new(XScale, 0, YScale, 0) end
function UDim2.fromOffset(XOffset, YOffset) return UDim2.new(0, XOffset, 0, YOffset) end
local Vector2 = {}
function Vector2.new(X, Y)
    return Value("Vector2", {X = Number(X, "Vector2.X", -32768, 32768), Y = Number(Y, "Vector2.Y", -32768, 32768)})
end
local Color3 = {}
function Color3.new(R, G, B)
    return Value("Color3", {R = Number(R, "Color3.R", 0, 1), G = Number(G, "Color3.G", 0, 1), B = Number(B, "Color3.B", 0, 1)})
end
function Color3.fromRGB(R, G, B)
    R, G, B = Number(R, "Color3.RGB", 0, 255), Number(G, "Color3.RGB", 0, 255), Number(B, "Color3.RGB", 0, 255)
    if R % 1 ~= 0 or G % 1 ~= 0 or B % 1 ~= 0 then Fail("Color3.fromRGB expects integer components") end
    return Color3.new(R / 255, G / 255, B / 255)
end
table.freeze(UDim); table.freeze(UDim2); table.freeze(Vector2); table.freeze(Color3)
local function Record(Player)
    local Value = Records[Player]
    if not Value then Fail("invalid Player proxy") end
    return Value
end
local PlayerMethods = {}
function PlayerMethods.SendMessage(Player, Message)
    local Value = Record(Player)
    Host(4, Value.Token, Value.UserId, String(Message, "message"))
end
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
function Players.GetPlayers(Self)
    if Self ~= Players then Fail("invalid Players service") end
    local Fields = Host(1)
    local Result = {}
    for Index = 1, #Fields, 3 do Result[#Result + 1] = GetPlayer(Fields[Index], Fields[Index + 1], Fields[Index + 2]) end
    return Result
end
function Players.GetPlayerByUserId(Self, UserId)
    if Self ~= Players then Fail("invalid Players service") end
    local Fields = Host(2, String(UserId, "userId"))
    if #Fields == 0 then return nil end
    return GetPlayer(Fields[1], Fields[2], Fields[3])
end
table.freeze(Players)
local Commands = {}
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
        if ValueKind == "UDim2" then return "udim2", tostring(Value.X.Scale), tostring(Value.X.Offset), tostring(Value.Y.Scale), tostring(Value.Y.Offset) end
        if ValueKind == "Vector2" then return "vector2", tostring(Value.X), tostring(Value.Y) end
        if ValueKind == "Color3" then return "color3", tostring(Value.R), tostring(Value.G), tostring(Value.B) end
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
function GuiObjectMethods.Create(Self, ClassName)
    local RecordValue = GuiRecord(Self)
    local Fields = Host(21, "create", RecordValue.Id, String(ClassName, "GUI class name"))
    return GetGuiObject(Fields[1], Fields[2])
end
function GuiObjectMethods.GetChildren(Self)
    local Fields = Host(20, "children", GuiRecord(Self).Id)
    local Result = {}
    for Index = 1, #Fields, 2 do Result[#Result + 1] = GetGuiObject(Fields[Index], Fields[Index + 1]) end
    return Result
end
function GuiObjectMethods.FindFirstChild(Self, Name)
    local Fields = Host(20, "find", GuiRecord(Self).Id, String(Name, "child name"))
    return #Fields == 0 and nil or GetGuiObject(Fields[1], Fields[2])
end
function GuiObjectMethods.IsA(Self, ClassName)
    return Host(20, "isa", GuiRecord(Self).Id, String(ClassName, "GUI class name"))[1] == "1"
end
function GuiObjectMethods.Clone(Self)
    local Fields = Host(21, "clone", GuiRecord(Self).Id)
    return GetGuiObject(Fields[1], Fields[2])
end
function GuiObjectMethods.Destroy(Self)
    local Fields = Host(21, "destroy", GuiRecord(Self).Id)
    for _, Id in Fields do Callbacks[Id] = nil end
end
table.freeze(GuiObjectMethods)
GuiObjectMeta.__index = function(Object, Key)
    local RecordValue = GuiRecord(Object)
    if Key == "Activated" then return GuiSignal(Object) end
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
    Host(21, "set", RecordValue.Id, Key, EncodeGuiValue(Value, Key == "ZIndex" or Key == "TextSize"))
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
function Gui.Create(Self, ClassName)
    if Self ~= Gui then Fail("invalid Gui service") end
    local Fields = Host(21, "create", "", String(ClassName, "GUI class name"))
    return GetGuiObject(Fields[1], Fields[2])
end
table.freeze(Gui)
local Game = {ApiName = "CarbonLuau", ApiVersion = "0.4.0-experimental", ApiStatus = "Experimental"}
function Game.GetService(Self, Name)
    if Self ~= Game then Fail("invalid game facade") end
    String(Name, "service name")
    if Name == "Players" then return Players end
    if Name == "Commands" then return Commands end
    if Name == "Gui" then return Gui end
    Fail("unknown service (expected Players, Commands or Gui)")
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
return Game, Dispatch, table.freeze({UDim = UDim, UDim2 = UDim2, Vector2 = Vector2, Color3 = Color3}), SharedValueEqual
