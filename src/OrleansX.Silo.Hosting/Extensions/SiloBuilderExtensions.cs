using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using OrleansX.Abstractions.Options;

namespace OrleansX.Silo.Hosting.Extensions;

/// <summary>
/// ISiloBuilder 확장 메서드
/// </summary>
public static class SiloBuilderExtensions
{
    /// <summary>
    /// OrleansX 기본 설정을 적용합니다.
    /// </summary>
    public static ISiloBuilder UseOrleansXDefaults(
        this ISiloBuilder builder,
        OrleansXSiloOptions options)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        // 클러스터 설정
        builder.Configure<ClusterOptions>(opts =>
        {
            opts.ClusterId = options.ClusterId;
            opts.ServiceId = options.ServiceId;
        });

        // 엔드포인트 설정
        builder.Configure<EndpointOptions>(opts =>
        {
            if (options.SiloPort.HasValue)
                opts.SiloPort = options.SiloPort.Value;
            if (options.GatewayPort.HasValue)
                opts.GatewayPort = options.GatewayPort.Value;
        });

        // 클러스터링 설정
        ConfigureClustering(builder, options.Clustering);

        // 영속성 설정
        ConfigurePersistence(builder, options.Persistence);

        // 스트림 설정
        ConfigureStreams(builder, options.Streams);

        return builder;
    }

    private static void ConfigureClustering(ISiloBuilder builder, ClusteringOptions? clusteringOptions)
    {
        switch (clusteringOptions)
        {
            case null:
            case ClusteringOptions.Localhost:
                builder.UseLocalhostClustering();
                break;

            case ClusteringOptions.AdoNet adonet:
                builder.UseAdoNetClustering(opts =>
                {
                    opts.Invariant = adonet.DbInvariant;
                    opts.ConnectionString = adonet.ConnectionString;
                });
                break;

            default:
                builder.UseLocalhostClustering();
                break;
        }
    }

    private static void ConfigurePersistence(ISiloBuilder builder, PersistenceOptions? persistenceOptions)
    {
        switch (persistenceOptions)
        {
            case null:
            case PersistenceOptions.Memory:
                builder.AddMemoryGrainStorage("Default");
                builder.AddMemoryGrainStorage("PubSubStore");
                break;

            case PersistenceOptions.AdoNet adonet:
                builder.AddAdoNetGrainStorage("Default", opts =>
                {
                    opts.Invariant = adonet.DbInvariant;
                    opts.ConnectionString = adonet.ConnectionString;
                });
                builder.AddMemoryGrainStorage("PubSubStore");
                break;

            default:
                builder.AddMemoryGrainStorage("Default");
                builder.AddMemoryGrainStorage("PubSubStore");
                break;
        }
    }

    private static void ConfigureStreams(ISiloBuilder builder, StreamsOptions? streamsOptions)
    {
        switch (streamsOptions)
        {
            case StreamsOptions.Memory memory:
                builder.AddMemoryStreams(memory.StreamProvider);
                break;

            case null:
                // 기본 스트림 설정 없음
                break;

            default:
                // 다른 스트림 옵션은 추후 구현
                break;
        }
    }
}

