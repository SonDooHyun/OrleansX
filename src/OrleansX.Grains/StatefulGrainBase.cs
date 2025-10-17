using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

namespace OrleansX.Grains;

/// <summary>
/// 상태를 가진 Grain의 베이스 클래스
/// </summary>
/// <typeparam name="TState">Grain의 상태 타입</typeparam>
public abstract class StatefulGrainBase<TState> : Grain where TState : class, new()
{
    private readonly IPersistentState<TState> _state;
    protected readonly ILogger Logger;

    protected StatefulGrainBase(
        [PersistentState("state")] IPersistentState<TState> state,
        ILogger logger)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Grain의 현재 상태
    /// </summary>
    protected TState State => _state.State;

    /// <summary>
    /// 상태가 로드되었는지 여부
    /// </summary>
    protected bool IsStateRecorded => _state.RecordExists;

    /// <summary>
    /// 상태의 ETag
    /// </summary>
    protected string? StateEtag => _state.Etag;

    /// <summary>
    /// 상태를 저장합니다.
    /// </summary>
    protected virtual async Task SaveStateAsync()
    {
        try
        {
            await _state.WriteStateAsync();
            Logger.LogDebug("State saved successfully for grain {GrainId}", this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save state for grain {GrainId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// 상태를 읽습니다.
    /// </summary>
    protected virtual async Task ReadStateAsync()
    {
        try
        {
            await _state.ReadStateAsync();
            Logger.LogDebug("State read successfully for grain {GrainId}", this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to read state for grain {GrainId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// 상태를 초기화합니다.
    /// </summary>
    protected virtual async Task ClearStateAsync()
    {
        try
        {
            await _state.ClearStateAsync();
            Logger.LogDebug("State cleared successfully for grain {GrainId}", this.GetPrimaryKeyString());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to clear state for grain {GrainId}", this.GetPrimaryKeyString());
            throw;
        }
    }

    /// <summary>
    /// 상태를 업데이트하고 저장합니다.
    /// </summary>
    protected virtual async Task UpdateStateAsync(Action<TState> updateAction)
    {
        if (updateAction == null)
            throw new ArgumentNullException(nameof(updateAction));

        updateAction(State);
        await SaveStateAsync();
    }

    /// <summary>
    /// Grain이 활성화될 때 호출됩니다.
    /// </summary>
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Grain {GrainId} activated", this.GetPrimaryKeyString());
        return base.OnActivateAsync(cancellationToken);
    }

    /// <summary>
    /// Grain이 비활성화될 때 호출됩니다.
    /// </summary>
    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Grain {GrainId} deactivated. Reason: {Reason}", 
            this.GetPrimaryKeyString(), reason);
        return base.OnDeactivateAsync(reason, cancellationToken);
    }
}

