using Orleans;

namespace OrleansX.Abstractions;

/// <summary>
/// 워커 Grain의 기본 인터페이스
/// 선택적으로 사용 가능합니다.
/// </summary>
public interface IWorkerGrain : IGrainWithStringKey
{
    /// <summary>
    /// 워커를 시작합니다.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// 워커를 중지합니다.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 워커의 현재 상태를 조회합니다.
    /// </summary>
    Task<WorkerStatusInfo> GetStatusAsync();

    /// <summary>
    /// 워커 통계를 초기화합니다.
    /// </summary>
    Task ResetStatisticsAsync();
}

/// <summary>
/// 워커 상태 정보 DTO
/// </summary>
[GenerateSerializer]
public class WorkerStatusInfo
{
    /// <summary>
    /// 실행 중 여부
    /// </summary>
    [Id(0)]
    public bool IsRunning { get; set; }

    /// <summary>
    /// 마지막 실행 시간
    /// </summary>
    [Id(1)]
    public DateTime? LastExecutionTime { get; set; }

    /// <summary>
    /// 다음 실행 예정 시간
    /// </summary>
    [Id(2)]
    public DateTime? NextExecutionTime { get; set; }

    /// <summary>
    /// 성공 횟수
    /// </summary>
    [Id(3)]
    public int SuccessCount { get; set; }

    /// <summary>
    /// 실패 횟수
    /// </summary>
    [Id(4)]
    public int FailureCount { get; set; }

    /// <summary>
    /// 실행 성공률 (%)
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var total = SuccessCount + FailureCount;
            return total == 0 ? 0 : (double)SuccessCount / total * 100;
        }
    }
}

