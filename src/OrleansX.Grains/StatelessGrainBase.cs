using Microsoft.Extensions.Logging;
using Orleans;

namespace OrleansX.Grains;

/// <summary>
/// 상태가 없는 Grain의 베이스 클래스
/// </summary>
public abstract class StatelessGrainBase : Grain
{
    protected readonly ILogger Logger;

    protected StatelessGrainBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Grain이 활성화될 때 호출됩니다.
    /// </summary>
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Stateless grain {GrainId} activated", this.GetPrimaryKeyString());
        return base.OnActivateAsync(cancellationToken);
    }

    /// <summary>
    /// Grain이 비활성화될 때 호출됩니다.
    /// </summary>
    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Stateless grain {GrainId} deactivated. Reason: {Reason}", 
            this.GetPrimaryKeyString(), reason);
        return base.OnDeactivateAsync(reason, cancellationToken);
    }
}

