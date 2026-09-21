-- Generated from implementation-owned CarbonLuau API declarations. Do not edit.
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
    read ApiVersion: "0.4.0-experimental"
    function GetService(self, Name: "Commands"): (Commands)
    function GetService(self, Name: "Gui"): (Gui)
    function GetService(self, Name: "Items"): (Items)
    function GetService(self, Name: "Players"): (Players)
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

declare extern type Items with
    function Exists(self, ShortName: string): (boolean)
end

declare extern type Player with
    function CountItem(self, ShortName: string): (number)
    function GiveItem(self, ShortName: string, Amount: number, Behavior: GiveItemBehavior?): (boolean)
    function HasItem(self, ShortName: string, Amount: number?): (boolean)
    function HasPermission(self, Permission: string): (boolean)
    read Health: number
    read IsConnected: boolean
    read MaxHealth: number
    read Name: string
    read Position: Vector3
    function SendMessage(self, Message: string): ()
    function TakeItem(self, ShortName: string, Amount: number): (boolean)
    function Teleport(self, Position: Vector3): ()
    read UserId: string
end

declare extern type Players with
    function GetPlayerByUserId(self, UserId: string): (Player?)
    function GetPlayers(self): ({Player})
    read PlayerAdded: Signal
    read PlayerRemoving: Signal
end

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

declare Color3: {
    read fromRGB: (R: number, G: number, B: number) -> (Color3),
    read new: (R: number, G: number, B: number) -> (Color3),
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
