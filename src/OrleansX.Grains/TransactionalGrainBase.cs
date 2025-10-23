using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Transactions.Abstractions;

namespace OrleansX.Grains;

/// <summary>
/// 트랜잭션을 지원하는 Grain의 베이스 클래스
/// </summary>
/// <typeparam name="TState">Grain의 트랜잭션 상태 타입</typeparam>
public abstract class TransactionalGrainBase<TState> : Grain
    where TState : class, new()
{
    private readonly ITransactionalState<TState> _transactionalState;
    protected readonly ILogger Logger;

    protected TransactionalGrainBase(
        [TransactionalState("state")] ITransactionalState<TState> transactionalState,
        ILogger logger)
    {
        _transactionalState = transactionalState ?? throw new ArgumentNullException(nameof(transactionalState));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 트랜잭션 내에서 상태를 읽기 전용으로 가져옵니다.
    /// </summary>
    protected async Task<TState> GetStateAsync()
    {
        try
        {
            return await _transactionalState.PerformRead(state =>
            {
                Logger.LogDebug("Transaction read state for grain {GrainId}", this.GetPrimaryKeyString());
                return state;
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read transactional state for grain {GrainId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// 트랜잭션 내에서 상태를 업데이트합니다.
    /// </summary>
    /// <param name="updateAction">상태 업데이트 액션</param>
    protected async Task UpdateStateAsync(Action<TState> updateAction)
    {
        if (updateAction == null)
            throw new ArgumentNullException(nameof(updateAction));

        try
        {
            await _transactionalState.PerformUpdate(state =>
            {
                updateAction(state);
                Logger.LogDebug("Transaction update state for grain {GrainId}", this.GetPrimaryKeyString());
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update transactional state for grain {GrainId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// 트랜잭션 내에서 상태를 업데이트하고 결과를 반환합니다.
    /// </summary>
    /// <typeparam name="TResult">반환 타입</typeparam>
    /// <param name="updateFunc">상태 업데이트 함수</param>
    protected async Task<TResult> UpdateStateAsync<TResult>(Func<TState, TResult> updateFunc)
    {
        if (updateFunc == null)
            throw new ArgumentNullException(nameof(updateFunc));

        try
        {
            return await _transactionalState.PerformUpdate(state =>
            {
                var result = updateFunc(state);
                Logger.LogDebug("Transaction update state with result for grain {GrainId}", this.GetPrimaryKeyString());
                return result;
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update transactional state with result for grain {GrainId}", 
                this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// 트랜잭션 내에서 상태를 읽고 결과를 반환합니다.
    /// </summary>
    /// <typeparam name="TResult">반환 타입</typeparam>
    /// <param name="readFunc">상태 읽기 함수</param>
    protected async Task<TResult> ReadStateAsync<TResult>(Func<TState, TResult> readFunc)
    {
        if (readFunc == null)
            throw new ArgumentNullException(nameof(readFunc));

        try
        {
            return await _transactionalState.PerformRead(state =>
            {
                var result = readFunc(state);
                Logger.LogDebug("Transaction read state with result for grain {GrainId}", this.GetPrimaryKeyString());
                return result;
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read transactional state with result for grain {GrainId}", 
                this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// Grain이 활성화될 때 호출됩니다.
    /// </summary>
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Transactional grain {GrainId} activated", this.GetPrimaryKeyString());
        return base.OnActivateAsync(cancellationToken);
    }

    /// <summary>
    /// Grain이 비활성화될 때 호출됩니다.
    /// </summary>
    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Transactional grain {GrainId} deactivated. Reason: {Reason}",
            this.GetPrimaryKeyString(), reason);
        return base.OnDeactivateAsync(reason, cancellationToken);
    }
}
