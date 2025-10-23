using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Hosting;
using Orleans.Transactions;
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
        Action<OrleansXSiloOptionsBuilder> configure)
    {
        var optionsBuilder = new OrleansXSiloOptionsBuilder();
        configure(optionsBuilder);
        var options = optionsBuilder.Build();

        // 클러스터 설정
        builder.Configure<Orleans.Configuration.ClusterOptions>(opts =>
        {
            opts.ClusterId = options.ClusterId;
            opts.ServiceId = options.ServiceId;
        });

        // 포트 설정
        if (options.SiloPort.HasValue || options.GatewayPort.HasValue)
        {
            builder.ConfigureEndpoints(
                siloPort: options.SiloPort ?? 11111,
                gatewayPort: options.GatewayPort ?? 30000);
        }

        // 클러스터링 설정
        if (options.Clustering != null)
        {
            ConfigureClustering(builder, options.Clustering);
        }

        // 영속성 설정
        if (options.Persistence != null)
        {
            ConfigurePersistence(builder, options.Persistence);
        }

        // 스트림 설정
        if (options.Streams != null)
        {
            ConfigureStreams(builder, options.Streams);
        }

        // 트랜잭션 설정
        if (options.Transactions != null)
        {
            ConfigureTransactions(builder, options.Transactions);
        }

        // 대시보드 설정 (Orleans.Dashboard 패키지가 필요합니다)
        if (options.DashboardPort.HasValue)
        {
            builder.UseDashboard(opts =>
            {
                opts.Port = options.DashboardPort.Value;
            });
        }

        return builder;
    }

    private static void ConfigureClustering(ISiloBuilder builder, ClusteringOptions clustering)
    {
        switch (clustering)
        {
            case ClusteringOptions.Localhost:
                builder.UseLocalhostClustering();
                break;
            case ClusteringOptions.AdoNet adoNet:
                builder.UseAdoNetClustering(opts =>
                {
                    opts.Invariant = adoNet.DbInvariant;
                    opts.ConnectionString = adoNet.ConnectionString;
                });
                break;
            case ClusteringOptions.Redis redis:
                // Redis clustering은 추가 패키지 필요
                throw new NotSupportedException("Redis clustering is not yet implemented");
            default:
                throw new ArgumentException($"Unsupported clustering type: {clustering.GetType().Name}");
        }
    }

    private static void ConfigurePersistence(ISiloBuilder builder, PersistenceOptions persistence)
    {
        switch (persistence)
        {
            case PersistenceOptions.Memory:
                builder.AddMemoryGrainStorage("Default");
                builder.AddMemoryGrainStorageAsDefault();
                break;
            case PersistenceOptions.AdoNet adoNet:
                builder.AddAdoNetGrainStorage("Default", opts =>
                {
                    opts.Invariant = adoNet.DbInvariant;
                    opts.ConnectionString = adoNet.ConnectionString;
                });
                break;
            case PersistenceOptions.Redis redis:
                // Redis persistence는 추가 패키지 필요
                throw new NotSupportedException("Redis persistence is not yet implemented");
            default:
                throw new ArgumentException($"Unsupported persistence type: {persistence.GetType().Name}");
        }
    }

    private static void ConfigureStreams(ISiloBuilder builder, StreamsOptions streams)
    {
        switch (streams)
        {
            case StreamsOptions.Memory memory:
                builder.AddMemoryStreams(memory.StreamProvider);
                builder.AddMemoryGrainStorage($"{memory.StreamProvider}PubSubStore");
                break;
            case StreamsOptions.Kafka kafka:
                // Kafka streams는 추가 패키지 필요
                throw new NotSupportedException("Kafka streams is not yet implemented");
            case StreamsOptions.EventHubs eventHubs:
                // EventHubs streams는 추가 패키지 필요
                throw new NotSupportedException("EventHubs streams is not yet implemented");
            default:
                throw new ArgumentException($"Unsupported streams type: {streams.GetType().Name}");
        }
    }

    private static void ConfigureTransactions(ISiloBuilder builder, TransactionOptions transactions)
    {
        switch (transactions)
        {
            case TransactionOptions.Memory:
                builder.UseTransactions();
                break;
            case TransactionOptions.AzureStorage azureStorage:
                builder.UseTransactions();
                builder.AddAzureTableTransactionalStateStorage("TransactionStore", opts =>
                {
                    opts.ConfigureTableServiceClient(azureStorage.ConnectionString);
                });
                break;
            case TransactionOptions.AdoNet adoNet:
                builder.UseTransactions();
                // AdoNet Transaction Log는 추가 구현 필요
                throw new NotSupportedException("AdoNet transaction log is not yet implemented");
            default:
                throw new ArgumentException($"Unsupported transaction type: {transactions.GetType().Name}");
        }
    }
}

/// <summary>
/// OrleansX Silo 옵션 빌더
/// </summary>
public class OrleansXSiloOptionsBuilder
{
    private string? _clusterId;
    private string? _serviceId;
    private int? _siloPort;
    private int? _gatewayPort;
    private ClusteringOptions? _clustering;
    private PersistenceOptions? _persistence;
    private StreamsOptions? _streams;
    private TransactionOptions? _transactions;
    private int? _dashboardPort;

    public OrleansXSiloOptionsBuilder WithCluster(string clusterId, string serviceId)
    {
        _clusterId = clusterId;
        _serviceId = serviceId;
        return this;
    }

    public OrleansXSiloOptionsBuilder WithPorts(int siloPort, int gatewayPort)
    {
        _siloPort = siloPort;
        _gatewayPort = gatewayPort;
        return this;
    }

    public OrleansXSiloOptionsBuilder WithClustering(ClusteringOptions clustering)
    {
        _clustering = clustering;
        return this;
    }

    public OrleansXSiloOptionsBuilder WithPersistence(PersistenceOptions persistence)
    {
        _persistence = persistence;
        return this;
    }

    public OrleansXSiloOptionsBuilder WithStreams(StreamsOptions streams)
    {
        _streams = streams;
        return this;
    }

    public OrleansXSiloOptionsBuilder WithTransactions(TransactionOptions transactions)
    {
        _transactions = transactions;
        return this;
    }

    public OrleansXSiloOptionsBuilder WithDashboard(int port)
    {
        _dashboardPort = port;
        return this;
    }

    internal OrleansXSiloOptions Build()
    {
        if (string.IsNullOrEmpty(_clusterId))
            throw new InvalidOperationException("ClusterId must be set using WithCluster()");
        if (string.IsNullOrEmpty(_serviceId))
            throw new InvalidOperationException("ServiceId must be set using WithCluster()");

        return new OrleansXSiloOptions
        {
            ClusterId = _clusterId,
            ServiceId = _serviceId,
            SiloPort = _siloPort,
            GatewayPort = _gatewayPort,
            Clustering = _clustering,
            Persistence = _persistence,
            Streams = _streams,
            Transactions = _transactions,
            DashboardPort = _dashboardPort
        };
    }
}

