local Balance = 0

return {
    Add = function(Amount)
        Balance += Amount
        return Balance
    end,
    GetBalance = function()
        return Balance
    end,
}
