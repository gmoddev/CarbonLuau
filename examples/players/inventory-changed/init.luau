-- Development API 0.6.5-experimental; not in published package v0.5.0.
-- Sampled observations only: temporary transfers may appear, balanced changes
-- may be missed, and dropped observations are not replayed.
local Players = game:GetService("Players")

local function Subscribe(Player: Player)
    local Ok, Error = pcall(function()
        -- GetInventory does not scan or subscribe. Connect stages demand until
        -- publication succeeds; its first accepted sample is a silent baseline.
        local Inventory = Player:GetInventory()
        Inventory.ItemChanged:Connect(function(Context: InventoryChangeContext)
            print("[Inventory:Sample]", Player.UserId, Context.Item,
                Context.Before, Context.After, Context.Delta)
        end)
    end)
    if not Ok then
        print("[Inventory:Sample] Subscription unavailable", Error)
    end
end

-- PlayerAdded is future-only; also subscribe to already connected Players.
-- Owner retirement or exact Player disconnect suppresses pending callbacks.
Players.PlayerAdded:Connect(Subscribe)
for _, Player in Players:GetPlayers() do
    Subscribe(Player)
end
