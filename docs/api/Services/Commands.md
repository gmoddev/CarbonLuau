# Commands

Availability: experimental API `0.3.0-experimental`.

`Commands:Register(Name: string, Options: {permission: string?, description: string?}, Callback: (CommandContext) -> ()) -> ()`

Obtain with `game:GetService("Commands")`. Register during entrypoint/module
initialization only; calls after generation commit raise an error. Registration
returns no handle. The generation owns removal; individual command unregistration,
runtime registration, console/RCON callers and additional options are not supported.

Names are 1–32 ASCII bytes, start with a lowercase letter, and otherwise contain
lowercase letters, digits, underscore or hyphen. Dots are not allowed. Carbon,
Oxide and RCON prefixes, and `c`, `quit`, `restart`, `server` are reserved. Existing
host command collisions fail candidate publication; duplicate names in one
candidate fail immediately. Maximum 64 commands per generation. Wrong types,
unknown options, invalid names, duplicates and exceeded limits raise errors.

Description is optional, at most 256 UTF-8 bytes, no NUL. Permission is optional;
absent or empty means public to connected players. A nonempty value follows the
[Player permission-name rules](../Types/Player.md). Carbon checks permission before
admission and again immediately before Lua callback entry. There is no implicit
admin bypass added by CarbonLuau and no script permission grant/revoke API.

After commit, the host registers missing permission metadata through Carbon. This
is capped at 256 new names per plugin lifetime, including previous generations;
existing permissions may be referenced without taking ownership. Host failure or
that metadata cap is diagnosed and never bypasses permission checks. Administrators
manage grants through Carbon. Plugin unload lets Carbon release plugin-owned
permission registrations; account grant persistence remains Carbon's policy.

```lua
local Commands = game:GetService("Commands")
Commands:Register("hello", {
    permission = "carbonluau.example.hello",
    description = "Send a greeting",
}, function(Context)
    Context.Player:SendMessage("Hello from Luau")
end)
```

Players invoke `/hello` through Carbon chat prefixes. No fake Player is made for a
server/console caller. A callback receives a [CommandContext](../Types/CommandContext.md),
runs later under scheduler budgets and cannot yield. Error isolation and timeout
recovery follow [Signal](../Types/Signal.md)'s generation rules.

Registration is provisional until the candidate succeeds. A failing candidate
cannot steal active commands. Successful commit atomically replaces CarbonLuau's
chat registrations while preserving foreign entries. Previously selected or queued
old-generation callbacks are rejected rather than retargeted to the new callback.
Unload removes all owned chat registrations. See [limits](../Compatibility.md).
