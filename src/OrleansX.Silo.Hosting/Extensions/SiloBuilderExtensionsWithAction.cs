using Orleans.Hosting;
using OrleansX.Abstractions.Options;

namespace OrleansX.Silo.Hosting.Extensions;

/// <summary>
/// ISiloBuilder 확장 메서드 (Action 기반)
/// </summary>
public static class SiloBuilderExtensionsWithAction
{
    /// <summary>
    /// OrleansX 기본 설정을 적용합니다. (Action 기반)
    /// </summary>
    public static ISiloBuilder UseOrleansXDefaults(
        this ISiloBuilder builder,
        Action<OrleansXSiloOptionsBuilder> configure)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var optionsBuilder = new OrleansXSiloOptionsBuilder();
        configure(optionsBuilder);
        var options = optionsBuilder.Build();

        return builder.UseOrleansXDefaults(options);
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
    private int? _dashboardPort;

    public OrleansXSiloOptionsBuilder WithCluster(string clusterId, string serviceId)
    {
        _clusterId = clusterId;
        _serviceId = serviceId;
        return this;
    }

    public OrleansXSiloOptionsBuilder WithPorts(int? siloPort = null, int? gatewayPort = null)
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

    public OrleansXSiloOptionsBuilder WithDashboard(int port)
    {
        _dashboardPort = port;
        return this;
    }

    public OrleansXSiloOptions Build()
    {
        if (string.IsNullOrWhiteSpace(_clusterId))
            throw new InvalidOperationException("ClusterId must be set");
        if (string.IsNullOrWhiteSpace(_serviceId))
            throw new InvalidOperationException("ServiceId must be set");

        return new OrleansXSiloOptions
        {
            ClusterId = _clusterId,
            ServiceId = _serviceId,
            SiloPort = _siloPort,
            GatewayPort = _gatewayPort,
            Clustering = _clustering,
            Persistence = _persistence,
            Streams = _streams,
            DashboardPort = _dashboardPort
        };
    }
}

