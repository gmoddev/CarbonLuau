-- Generated from implementation-owned CarbonLuau API declarations. Do not edit.
export type SignalWith<Callback> = {
    read Connect: (self: SignalWith<Callback>, Callback: Callback) -> (Connection),
}

declare extern type Addon with
    read Id: string
    function IsDependencyAvailable(self, Id: string): (boolean)
    read Version: string
end

declare extern type Color3 with
    read B: number
    read G: number
    read R: number
end

export type CommandContext = {
    read Arguments: {string},
    read Name: string,
    read Player: Player,
}

export type CommandOptions = {
    read description: string?,
    read permission: string?,
}

declare extern type Commands with
    function Register(self, Name: string, Options: CommandOptions, Callback: (CommandContext) -> ()): ()
end

declare extern type Connection with
    function Disconnect(self): ()
end

declare extern type DataStore with
    function GetAsync(self, Key: string, Callback: (PersistedValue?, string?) -> ()): ()
    function Query(self, Request: DataStoreQuery, Callback: (DataStoreQueryResult?, string?) -> ()): ()
    function RemoveAsync(self, Key: string, Callback: (boolean?, string?) -> ()): ()
    function SetAsync(self, Key: string, Value: PersistedValue, Callback: (boolean?, string?) -> ()): ()
end

export type DataStoreOptions = {Indexes: {string}?}

export type DataStoreQuery = {Field: string, Type: ("number" | "string" | "boolean")?, Equals: (number | string | boolean)?, Min: (number | string)?, Max: (number | string)?, Direction: ("Ascending" | "Descending")?, Limit: number?, Cursor: string?}

export type DataStoreQueryResult = {Items: {{Key: string, Value: PersistedValue}}, NextCursor: string?}

declare extern type DataStoreService with
    function GetDataStore(self, StoreName: string, Options: DataStoreOptions?): (DataStore)
end

declare extern type DropMode with
end

declare extern type Entity with
    read Id: string
    read Position: Vector3
    read Prefab: string
end

export type EntityDeathContext = {
    read DamageType: string?,
    read Id: string,
    read Killer: Player?,
    read KillerId: string?,
    read Position: Vector3?,
    read Prefab: string,
    read Weapon: string?,
}

export type EntityDestroyedContext = {
    read Id: string,
    read Position: Vector3?,
    read Prefab: string,
}

export type EntityDiscoveryOptions = {Prefab: string?, Limit: number?}

declare extern type Enum with
    read DropMode: {read Replace: DropMode}
end

declare extern type GuiNode with
    read ClassName: string
    function Clone(self): (GuiNode)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    function FindFirstChild(self, Name: string): (GuiNode?)
    function GetChildren(self): ({GuiNode})
    function IsA(self, ClassName: string): (boolean)
    Name: string
end

declare extern type GuiObject extends GuiNode with
    AnchorPoint: Vector2
    BackgroundColor3: Color3
    BackgroundTransparency: number
    function Clone(self): (GuiObject)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    function FindFirstChild(self, Name: string): (GuiNode?)
    function GetChildren(self): ({GuiNode})
    function IsA(self, ClassName: string): (boolean)
    LayoutOrder: number
    Parent: GuiNode?
    Position: UDim2
    Size: UDim2
    Visible: boolean
    ZIndex: number
end

declare extern type Frame extends GuiObject with
    ClipsDescendants: boolean
    function Clone(self): (Frame)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    function FindFirstChild(self, Name: string): (GuiNode?)
    function GetChildren(self): ({GuiNode})
    function IsA(self, ClassName: string): (boolean)
end

declare extern type Game with
    read ApiName: "CarbonLuau"
    read ApiStatus: "Experimental"
    read ApiVersion: "0.7.5-experimental"
    function GetService(self, Name: "Commands"): (Commands)
    function GetService(self, Name: "DataStoreService"): (DataStoreService)
    function GetService(self, Name: "Gui"): (Gui)
    function GetService(self, Name: "Items"): (Items)
    function GetService(self, Name: "Loot"): (Loot)
    function GetService(self, Name: "Players"): (Players)
    function GetService(self, Name: "Workspace"): (Workspace)
end

declare extern type GiveItemBehavior with
end

declare extern type Gui with
    function Create(self, ClassName: "ScreenGui"): (ScreenGui)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
end

declare extern type GuiFont with
end

declare extern type ImageButton extends GuiObject with
    read Activated: Signal
    function Clone(self): (ImageButton)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    function FindFirstChild(self, Name: string): (GuiNode?)
    function GetChildren(self): ({GuiNode})
    Image: ImageSource
    ImageColor3: Color3
    ImageTransparency: number
    function IsA(self, ClassName: string): (boolean)
end

declare extern type ImageLabel extends GuiObject with
    function Clone(self): (ImageLabel)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    function FindFirstChild(self, Name: string): (GuiNode?)
    function GetChildren(self): ({GuiNode})
    Image: ImageSource
    ImageColor3: Color3
    ImageTransparency: number
    function IsA(self, ClassName: string): (boolean)
end

declare extern type ImageSource with
    read Id: string
    read ItemId: number
    read Kind: string
    read Name: string
    read SkinId: string
    read UserId: string
end

export type InventoryChangeContext = {
    read After: number,
    read Before: number,
    read Delta: number,
    read Item: string,
}

declare extern type Items with
    function Exists(self, ShortName: string): (boolean)
end

declare extern type Loot with
    function RegisterDropRule(self, Prefab: string, Mode: DropMode, Drops: {LootDropEntry}): ()
end

export type LootDropEntry = {Item: string, Min: number, Max: number}

export type PersistedValue = boolean | number | string | {PersistedValue} | {[string]: PersistedValue}

declare extern type Player with
    function GetInventory(self): (PlayerInventory)
    function HasPermission(self, Permission: string): (boolean)
    read Health: number
    read IsConnected: boolean
    read MaxHealth: number
    read Name: string
    read Position: Vector3
    function SendMessage(self, Message: string): ()
    function Teleport(self, Position: Vector3): ()
    read UserId: string
end

export type PlayerDeathContext = {
    read Killer: Player?,
    read KillerId: string?,
    read Position: Vector3?,
}

declare extern type PlayerInventory with
    function CountItem(self, ShortName: string): (number)
    function GiveItem(self, ShortName: string, Amount: number, Behavior: GiveItemBehavior?): (boolean)
    function HasItem(self, ShortName: string, Amount: number?): (boolean)
    read ItemChanged: SignalWith<(Context: InventoryChangeContext) -> ()>
    function TakeItem(self, ShortName: string, Amount: number): (boolean)
end

export type PlayerSpawnContext = {
    read Position: Vector3?,
}

declare extern type Players with
    function GetPlayerByUserId(self, UserId: string): (Player?)
    function GetPlayers(self): ({Player})
    read PlayerAdded: Signal
    read PlayerDied: SignalWith<(Player: Player, Context: PlayerDeathContext) -> ()>
    read PlayerRemoving: Signal
    read PlayerSpawned: SignalWith<(Player: Player, Context: PlayerSpawnContext) -> ()>
end

export type PrefabLoot = {
    read Barrel1: "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
    read Barrel2: "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab",
}

declare extern type ScreenGui extends GuiNode with
    function Clone(self): (ScreenGui)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    function FindFirstChild(self, Name: string): (GuiNode?)
    function GetChildren(self): ({GuiNode})
    function Hide(self, Player: Player): ()
    function IsA(self, ClassName: string): (boolean)
    function IsShown(self, Player: Player): (boolean)
    read Parent: GuiNode?
    function Show(self, Player: Player): ()
end

declare extern type ScrollingFrame extends GuiObject with
    CanvasSize: UDim2
    function Clone(self): (ScrollingFrame)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    function FindFirstChild(self, Name: string): (GuiNode?)
    function GetChildren(self): ({GuiNode})
    function IsA(self, ClassName: string): (boolean)
    function ScrollTo(self, Player: Player, Position: Vector2): ()
    function ScrollToBottom(self, Player: Player): ()
    function ScrollToTop(self, Player: Player): ()
    ScrollingDirection: "X" | "Y" | "XY"
    ScrollingEnabled: boolean
end

declare extern type Signal with
    function Connect(self, Callback: (Player) -> ()): (Connection)
end

declare extern type Task with
end

declare extern type TextButton extends GuiObject with
    read Activated: Signal
    function Clone(self): (TextButton)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    function FindFirstChild(self, Name: string): (GuiNode?)
    Font: GuiFont
    function GetChildren(self): ({GuiNode})
    function IsA(self, ClassName: string): (boolean)
    Text: string
    TextColor3: Color3
    TextSize: number
    TextTransparency: number
    TextXAlignment: "Left" | "Center" | "Right"
    TextYAlignment: "Top" | "Center" | "Bottom"
end

declare extern type TextLabel extends GuiObject with
    function Clone(self): (TextLabel)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    function FindFirstChild(self, Name: string): (GuiNode?)
    Font: GuiFont
    function GetChildren(self): ({GuiNode})
    function IsA(self, ClassName: string): (boolean)
    Text: string
    TextColor3: Color3
    TextSize: number
    TextTransparency: number
    TextXAlignment: "Left" | "Center" | "Right"
    TextYAlignment: "Top" | "Center" | "Bottom"
end

declare extern type UDim with
    read Offset: number
    read Scale: number
end

declare extern type UDim2 with
    read X: UDim
    read Y: UDim
end

declare extern type UIGridLayout extends GuiNode with
    CellPadding: UDim2
    CellSize: UDim2
    function Clone(self): (UIGridLayout)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    FillDirection: "Vertical" | "Horizontal"
    FillDirectionMaxCells: number
    function FindFirstChild(self, Name: string): (GuiNode?)
    function GetChildren(self): ({GuiNode})
    HorizontalAlignment: "Left" | "Center" | "Right"
    function IsA(self, ClassName: string): (boolean)
    Parent: GuiNode?
    VerticalAlignment: "Top" | "Center" | "Bottom"
end

declare extern type UIListLayout extends GuiNode with
    function Clone(self): (UIListLayout)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    FillDirection: "Vertical" | "Horizontal"
    function FindFirstChild(self, Name: string): (GuiNode?)
    function GetChildren(self): ({GuiNode})
    HorizontalAlignment: "Left" | "Center" | "Right"
    function IsA(self, ClassName: string): (boolean)
    Padding: UDim
    Parent: GuiNode?
    VerticalAlignment: "Top" | "Center" | "Bottom"
end

declare extern type UIPadding extends GuiNode with
    function Clone(self): (UIPadding)
    function Create(self, ClassName: "Frame"): (Frame)
    function Create(self, ClassName: "TextLabel"): (TextLabel)
    function Create(self, ClassName: "TextButton"): (TextButton)
    function Create(self, ClassName: "UIListLayout"): (UIListLayout)
    function Create(self, ClassName: "UIPadding"): (UIPadding)
    function Create(self, ClassName: "ImageLabel"): (ImageLabel)
    function Create(self, ClassName: "ImageButton"): (ImageButton)
    function Create(self, ClassName: "ScrollingFrame"): (ScrollingFrame)
    function Create(self, ClassName: "UIGridLayout"): (UIGridLayout)
    function Destroy(self): ()
    function FindFirstChild(self, Name: string): (GuiNode?)
    function GetChildren(self): ({GuiNode})
    function IsA(self, ClassName: string): (boolean)
    PaddingBottom: UDim
    PaddingLeft: UDim
    PaddingRight: UDim
    PaddingTop: UDim
    Parent: GuiNode?
end

declare extern type Vector2 with
    read X: number
    read Y: number
end

declare extern type Vector3 with
    read Magnitude: number
    read X: number
    read Y: number
    read Z: number
    __add: ((Left: Vector3, Right: Vector3) -> (Vector3))
    __div: ((Left: Vector3, Right: number) -> (Vector3))
    __mul: ((Left: Vector3, Right: number) -> (Vector3)) & ((Left: number, Right: Vector3) -> (Vector3))
    __sub: ((Left: Vector3, Right: Vector3) -> (Vector3))
    __unm: ((Operand: Vector3) -> (Vector3))
end

declare extern type Workspace with
    read EntityDestroyed: SignalWith<(Context: EntityDestroyedContext) -> ()>
    read EntityDied: SignalWith<(Context: EntityDeathContext) -> ()>
    read EntitySpawned: SignalWith<(Entity: Entity) -> ()>
    function GetEntitiesInRadiusAsync(self, Position: Vector3, Radius: number, Callback: ({Entity}?, string?) -> (), Options: EntityDiscoveryOptions?): ()
    function GetEntityById(self, Id: string): (Entity?)
end

declare Color3: {
    read fromRGB: (R: number, G: number, B: number) -> (Color3),
    read new: (R: number, G: number, B: number) -> (Color3),
}

declare Enum: {
    read DropMode: {read Replace: DropMode},
}

declare GiveItemBehavior: {
    read InventoryOnly: GiveItemBehavior,
}

declare GuiFont: {
    read DroidSansMono: GuiFont,
    read PermanentMarker: GuiFont,
    read RobotoCondensedBold: GuiFont,
    read RobotoCondensedRegular: GuiFont,
}

declare ImageSource: {
    read Item: (ItemId: number, SkinId: string?) -> (ImageSource),
    read None: () -> (ImageSource),
    read Png: (Id: string) -> (ImageSource),
    read Sprite: (Name: string) -> (ImageSource),
    read SteamAvatar: (UserId: string) -> (ImageSource),
}

declare Item: {
    read AmmoPaintball: "ammo.paintball",
    read AmmoPistol: "ammo.pistol",
    read AmmoPistolHv: "ammo.pistol.hv",
    read AmmoRifle: "ammo.rifle",
    read AmmoRifleHv: "ammo.rifle.hv",
    read AmmoRocketHv: "ammo.rocket.hv",
    read AmmoRocketSam: "ammo.rocket.sam",
    read AmmoShotgun: "ammo.shotgun",
    read Antiradpills: "antiradpills",
    read AppleSpoiled: "apple.spoiled",
    read ArrowBone: "arrow.bone",
    read ArrowFire: "arrow.fire",
    read ArrowHv: "arrow.hv",
    read ArrowWooden: "arrow.wooden",
    read AttireNesthat: "attire.nesthat",
    read Ball: "ball",
    read Barrelcostume: "barrelcostume",
    read BatterySmall: "battery.small",
    read BduPants: "bdu.pants",
    read BduShirt: "bdu.shirt",
    read BearmeatBurned: "bearmeat.burned",
    read Bicycle: "bicycle",
    read Bleach: "bleach",
    read Blood: "blood",
    read Bluedogtags: "bluedogtags",
    read Blueidtag: "blueidtag",
    read Blueprintbase: "blueprintbase",
    read BoneFragments: "bone.fragments",
    read BootsFrog: "boots.frog",
    read BottleVodka: "bottle.vodka",
    read BreadLoaf: "bread.loaf",
    read BurlapGloves: "burlap.gloves",
    read BurlapHeadwrap: "burlap.headwrap",
    read BurlapShirt: "burlap.shirt",
    read BurlapShoes: "burlap.shoes",
    read BurlapTrousers: "burlap.trousers",
    read Caboose: "caboose",
    read CanBeans: "can.beans",
    read CanBeansEmpty: "can.beans.empty",
    read CanTuna: "can.tuna",
    read CanTunaEmpty: "can.tuna.empty",
    read Candycane: "candycane",
    read Cannonball: "cannonball",
    read Captainslog: "captainslog",
    read Charcoal: "charcoal",
    read ChickenBurned: "chicken.burned",
    read ChickenSpoiled: "chicken.spoiled",
    read Chocolate: "chocolate",
    read ClatterHelmet: "clatter.helmet",
    read Cloth: "cloth",
    read Coal: "coal",
    read Coconut: "coconut",
    read Coolingtea: "coolingtea",
    read Cratecostume: "cratecostume",
    read CrudeOil: "crude.oil",
    read DartRadiation: "dart.radiation",
    read DartScatter: "dart.scatter",
    read DartWood: "dart.wood",
    read DeermeatBurned: "deermeat.burned",
    read DieselBarrel: "diesel_barrel",
    read DivingFins: "diving.fins",
    read DivingMask: "diving.mask",
    read DivingWetsuit: "diving.wetsuit",
    read Documents: "documents",
    read Dogtagneutral: "dogtagneutral",
    read DoorKey: "door.key",
    read Draculacape: "draculacape",
    read Draculamask: "draculamask",
    read Ducttape: "ducttape",
    read EasterGoldegg: "easter.goldegg",
    read Explosives: "explosives",
    read FatAnimal: "fat.animal",
    read Fertilizer: "fertilizer",
    read FishAnchovy: "fish.anchovy",
    read FishCatfish: "fish.catfish",
    read FishHerring: "fish.herring",
    read FishMinnows: "fish.minnows",
    read FishSalmon: "fish.salmon",
    read FishSardine: "fish.sardine",
    read FishSmallshark: "fish.smallshark",
    read FishSpoiled: "fish.spoiled",
    read FishTroutsmall: "fish.troutsmall",
    read FishingTackle: "fishing.tackle",
    read Gears: "gears",
    read Ghostsheet: "ghostsheet",
    read Gingerbreadsuit: "gingerbreadsuit",
    read Glue: "glue",
    read Granolabar: "granolabar",
    read Grayidtag: "grayidtag",
    read Greenidtag: "greenidtag",
    read Grub: "grub",
    read Gunpowder: "gunpowder",
    read Hab: "hab",
    read HalloweenCandy: "halloween.candy",
    read Harvestingtea: "harvestingtea",
    read HatBeenie: "hat.beenie",
    read HatBoonie: "hat.boonie",
    read HatBunnyhat: "hat.bunnyhat",
    read HatCap: "hat.cap",
    read HatDragonmask: "hat.dragonmask",
    read HatHorsemask: "hat.horsemask",
    read HatOxmask: "hat.oxmask",
    read HatRabbitmask: "hat.rabbitmask",
    read HatRatmask: "hat.ratmask",
    read HatSnakemask: "hat.snakemask",
    read HatTigermask: "hat.tigermask",
    read HatWellipets: "hat.wellipets",
    read HatWolf: "hat.wolf",
    read Healingtea: "healingtea",
    read HealingteaPure: "healingtea.pure",
    read Honey: "honey",
    read Honeycomb: "honeycomb",
    read Hoodie: "hoodie",
    read Horse: "horse",
    read Horsedung: "horsedung",
    read HqMetalOre: "hq.metal.ore",
    read JacketSnow: "jacket.snow",
    read JarPickle: "jar.pickle",
    read Kickgems: "kickgems",
    read Largemedkit: "largemedkit",
    read Lavenderidtag: "lavenderidtag",
    read Leather: "leather",
    read Locomotive: "locomotive",
    read Lowgradefuel: "lowgradefuel",
    read MaskBalaclava: "mask.balaclava",
    read MaskBandana: "mask.bandana",
    read Maxhealthtea: "maxhealthtea",
    read MetalFragments: "metal.fragments",
    read MetalOre: "metal.ore",
    read MetalRefined: "metal.refined",
    read Metalblade: "metalblade",
    read Metalpipe: "metalpipe",
    read Metalspring: "metalspring",
    read Minicopter: "minicopter",
    read Mintidtag: "mintidtag",
    read Mlrs: "mlrs",
    read Motorbike: "motorbike",
    read MrspiceCan: "mrspice.can",
    read Mummymask: "mummymask",
    read N2ModuleCar: "2module.car",
    read N3ModuleCar: "3module.car",
    read N4ModuleCar: "4module.car",
    read Note: "note",
    read Nucleus: "nucleus",
    read Orangeidtag: "orangeidtag",
    read Orchid: "orchid",
    read Oretea: "oretea",
    read OreteaAdvanced: "oretea.advanced",
    read OreteaPure: "oretea.pure",
    read Pants: "pants",
    read PantsShorts: "pants.shorts",
    read Paper: "paper",
    read Partyhat: "partyhat",
    read Pinkidtag: "pinkidtag",
    read Plantfiber: "plantfiber",
    read Prisonerhood: "prisonerhood",
    read Propanetank: "propanetank",
    read Ptboat: "ptboat",
    read Purecoolingtea: "purecoolingtea",
    read Purewarmingtea: "purewarmingtea",
    read Purpleidtag: "purpleidtag",
    read Reddogtags: "reddogtags",
    read Redidtag: "redidtag",
    read Researchpaper: "researchpaper",
    read Rhib: "rhib",
    read Riflebody: "riflebody",
    read RoadsignGloves: "roadsign.gloves",
    read Roadsigns: "roadsigns",
    read Rope: "rope",
    read Rose: "rose",
    read Santabeard: "santabeard",
    read Santahat: "santahat",
    read ScarecrowSuit: "scarecrow.suit",
    read Scarecrowhead: "scarecrowhead",
    read Scrap: "scrap",
    read Scraptea: "scraptea",
    read ScrapteaPure: "scraptea.pure",
    read Sedan: "sedan",
    read Semibody: "semibody",
    read Sewingkit: "sewingkit",
    read Sheetmetal: "sheetmetal",
    read ShirtCollared: "shirt.collared",
    read ShirtTanktop: "shirt.tanktop",
    read ShoesBoots: "shoes.boots",
    read SkullHuman: "skull.human",
    read SkullWolf: "skull.wolf",
    read Smgbody: "smgbody",
    read Snowmobile: "snowmobile",
    read SpeargunSpear: "speargun.spear",
    read SpoiledProduce: "spoiled.produce",
    read Spraycandecal: "spraycandecal",
    read Sticks: "sticks",
    read Stones: "stones",
    read Submarineduo: "submarineduo",
    read Submarinesolo: "submarinesolo",
    read Sulfur: "sulfur",
    read SulfurOre: "sulfur.ore",
    read Sunflower: "sunflower",
    read Supertea: "supertea",
    read TacticalGloves: "tactical.gloves",
    read Tarp: "tarp",
    read Techparts: "techparts",
    read Trike: "trike",
    read Tshirt: "tshirt",
    read TshirtLong: "tshirt.long",
    read TwitchHeadset: "twitch.headset",
    read VenomSnake: "venom.snake",
    read Wagon: "wagon",
    read Walkietalkie: "walkietalkie",
    read WallpaperWall: "wallpaper.wall",
    read Warmingtea: "warmingtea",
    read Wheat: "wheat",
    read Whiteidtag: "whiteidtag",
    read WolfmeatBurned: "wolfmeat.burned",
    read Wood: "wood",
    read Woodtea: "woodtea",
    read WoodteaPure: "woodtea.pure",
    read Wool: "wool",
    read Workcart: "workcart",
    read Worm: "worm",
    read Yellowidtag: "yellowidtag",
}

declare Prefab: {
    read Loot: PrefabLoot,
}

declare task: {
    read defer: (Callback: (...any) -> (), ...any) -> (),
    read delay: (Seconds: number, Callback: (...any) -> (), ...any) -> (),
    read spawn: (Callback: (...any) -> (), ...any) -> (),
}

declare UDim: {
    read new: (Scale: number, Offset: number) -> (UDim),
}

declare UDim2: {
    read fromOffset: (XOffset: number, YOffset: number) -> (UDim2),
    read fromScale: (XScale: number, YScale: number) -> (UDim2),
    read new: (XScale: number, XOffset: number, YScale: number, YOffset: number) -> (UDim2),
}

declare Vector2: {
    read new: (X: number, Y: number) -> (Vector2),
}

declare Vector3: {
    read new: (X: number, Y: number, Z: number) -> (Vector3),
}

declare game: Game
declare addon: Addon
