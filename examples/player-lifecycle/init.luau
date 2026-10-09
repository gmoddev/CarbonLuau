-- Development API 0.6.5-experimental; not part of published v0.5.0.
local Players = game:GetService("Players")

Players.PlayerSpawned:Connect(function(Player, Context)
    print("spawned", Player.UserId, Context.Position)
end)

Players.PlayerDied:Connect(function(Player, Context)
    print("died", Player.UserId, "killer", Context.KillerId or "unattributed")
end)

-- Registration is future-only. Enumerate current connections explicitly:
for _, Player in Players:GetPlayers() do
    print("already connected", Player.UserId)
end
