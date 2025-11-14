using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Transactions.Abstractions;
using OrleansX.Grains;
using OrleansX.Abstractions;

namespace Tutorial.TransactionalGrain.Grains;

public class AccountGrain : TransactionalGrainBase<AccountState>, IAccountGrain
{
    private readonly IGrainInvoker _invoker;

    public AccountGrain(
        [TransactionalState("state")] ITransactionalState<AccountState> state,
        ILogger<AccountGrain> logger,
        IGrainInvoker invoker)
        : base(state, logger)
    {
        _invoker = invoker;
    }

    public async Task<decimal> GetBalanceAsync()
    {
        return await ReadStateAsync(state => state.Balance);
    }

    public async Task DepositAsync(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Deposit amount must be positive", nameof(amount));
        }

        await UpdateStateAsync(state =>
        {
            state.Balance += amount;
            state.UpdatedAt = DateTime.UtcNow;

            Logger.LogInformation("Account {AccountId} deposited {Amount}. New balance: {Balance}",
                this.GetPrimaryKeyString(), amount, state.Balance);
        });
    }

    public async Task WithdrawAsync(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Withdrawal amount must be positive", nameof(amount));
        }

        await UpdateStateAsync(state =>
        {
            if (state.Balance < amount)
            {
                Logger.LogWarning("Account {AccountId} has insufficient funds. Balance: {Balance}, Requested: {Amount}",
                    this.GetPrimaryKeyString(), state.Balance, amount);
                throw new InsufficientFundsException(
                    $"Insufficient funds. Balance: {state.Balance}, Requested: {amount}");
            }

            state.Balance -= amount;
            state.UpdatedAt = DateTime.UtcNow;

            Logger.LogInformation("Account {AccountId} withdrew {Amount}. New balance: {Balance}",
                this.GetPrimaryKeyString(), amount, state.Balance);
        });
    }

    [Transaction(TransactionOption.Create)]
    public async Task TransferToAsync(string targetAccountId, decimal amount)
    {
        Logger.LogInformation("Starting transfer from {From} to {To} amount {Amount}",
            this.GetPrimaryKeyString(), targetAccountId, amount);

        // 현재 계좌에서 출금
        await WithdrawAsync(amount);

        // 대상 계좌에 입금
        var targetAccount = _invoker.GetGrain<IAccountGrain>(targetAccountId);
        await targetAccount.DepositAsync(amount);

        Logger.LogInformation("Transfer completed from {From} to {To} amount {Amount}",
            this.GetPrimaryKeyString(), targetAccountId, amount);
    }
}
