using Orleans.Hosting;

namespace OrleansX.Silo.Hosting;

/// <summary>
/// 튜토리얼용 간소화된 호스팅 확장 메서드
/// </summary>
public static class SimplifiedHostingExtensions
{
    /// <summary>
    /// 튜토리얼용 간소화된 OrleansX 설정을 적용합니다.
    /// ISiloBuilder를 직접 노출하여 Orleans 메서드를 호출할 수 있습니다.
    /// </summary>
    public static ISiloBuilder UseOrleansX(
        this ISiloBuilder builder,
        Action<ISiloBuilder> configure)
    {
        // Just pass the builder through to the configuration action
        // This allows tutorials to call Orleans methods directly
        configure(builder);
        return builder;
    }
}
