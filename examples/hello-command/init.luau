local Commands = game:GetService("Commands")
Commands:Register("hello", {
    permission = "carbonluau.example.hello",
    description = "Send a greeting",
}, function(Context)
    Context.Player:SendMessage("Hello from Luau.")
end)
