using Orleans;

namespace Tutorial.TransactionalGrain.Grains;

public interface IAccountGrain : IGrainWithStringKey
{
    [Transaction(TransactionOption.Join)]
    Task<decimal> GetBalanceAsync();

    [Transaction(TransactionOption.Join)]
    Task DepositAsync(decimal amount);

    [Transaction(TransactionOption.Join)]
    Task WithdrawAsync(decimal amount);

    [Transaction(TransactionOption.Create)]
    Task TransferToAsync(string targetAccountId, decimal amount);
}

[GenerateSerializer]
public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(string message) : base(message) { }
}
