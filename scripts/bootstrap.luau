-- Trusted, build-embedded facade. Host is private; authorization is managed.
local Host = ...
local Callbacks = {}
local Proxies = setmetatable({}, {__mode = "v"})
local Records = setmetatable({}, {__mode = "k"})
local function Fail(Message) error(Message, 3) end
local function String(Value, Label)
    if type(Value) ~= "string" then Fail("expected string for " .. Label) end
    return Value
end
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
local Game = {ApiName = "CarbonLuau", ApiVersion = "0.3.0-experimental", ApiStatus = "Experimental"}
function Game.GetService(Self, Name)
    if Self ~= Game then Fail("invalid game facade") end
    String(Name, "service name")
    if Name == "Players" then return Players end
    if Name == "Commands" then return Commands end
    Fail("unknown service (expected Players or Commands)")
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
return Game, Dispatch
