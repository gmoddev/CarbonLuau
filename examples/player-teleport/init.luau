local Players = game:GetService("Players")

task.defer(function()
    local Player = Players:GetPlayers()[1]
    if Player then
        Player:Teleport(Vector3.new(100, 20, -50))
    end
end)
