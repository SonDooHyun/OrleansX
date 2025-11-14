using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using OrleansX.Abstractions;
using OrleansX.Abstractions.Options;
using OrleansX.Client.Idempotency;
using OrleansX.Client.Retry;

namespace OrleansX.Client.Extensions;

/// <summary>
/// IServiceCollection 확장 메서드
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// OrleansX Client를 기본 설정으로 등록합니다 (튜토리얼용).
    /// </summary>
    public static IServiceCollection AddOrleansXClient(this IServiceCollection services)
    {
        var defaultOptions = new OrleansClientOptions
        {
            ClusterId = "dev",
            ServiceId = "OrleansXService"
        };
        return AddOrleansXClient(services, defaultOptions);
    }

    /// <summary>
    /// OrleansX Client를 등록합니다.
    /// </summary>
    public static IServiceCollection AddOrleansXClient(
        this IServiceCollection services,
        OrleansClientOptions options)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        // Orleans Client 등록
        services.AddOrleansClient(builder =>
        {
            builder.Configure<ClusterOptions>(opts =>
            {
                opts.ClusterId = options.ClusterId;
                opts.ServiceId = options.ServiceId;
            });

            // 클러스터링 설정
            if (options.Db != null)
            {
                builder.UseAdoNetClustering(opts =>
                {
                    opts.Invariant = options.Db.DbInvariant;
                    opts.ConnectionString = options.Db.ConnectionString;
                });
            }
            else
            {
                // 기본값: localhost
                builder.UseLocalhostClustering();
            }

            // 연결 재시도 설정
            if (options.ConnectionRetry != null)
            {
                builder.Configure<GatewayOptions>(opts =>
                {
                    opts.GatewayListRefreshPeriod = TimeSpan.FromSeconds(
                        options.ConnectionRetry.RetryIntervalSeconds);
                });
            }
        });

        // GrainInvoker 등록
        services.AddSingleton<IGrainInvoker, GrainInvoker>();

        // Retry Policy 등록
        var retryOptions = options.Retry ?? new RetryOptions();
        services.AddSingleton<IRetryPolicy>(
            new ExponentialRetryPolicy(
                retryOptions.MaxAttempts,
                retryOptions.BaseDelayMs,
                retryOptions.MaxDelayMs));

        // Idempotency Key Provider 등록
        services.AddSingleton<IIdempotencyKeyProvider, AsyncLocalIdempotencyKeyProvider>();

        return services;
    }
}

