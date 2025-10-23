namespace OrleansX.Abstractions.Options;

/// <summary>
/// OrleansX Silo 호스팅 설정
/// </summary>
public class OrleansXSiloOptions
{
    /// <summary>
    /// 클러스터 ID
    /// </summary>
    public required string ClusterId { get; init; }

    /// <summary>
    /// 서비스 ID
    /// </summary>
    public required string ServiceId { get; init; }

    /// <summary>
    /// Silo 포트
    /// </summary>
    public int? SiloPort { get; init; }

    /// <summary>
    /// Gateway 포트
    /// </summary>
    public int? GatewayPort { get; init; }

    /// <summary>
    /// 클러스터링 설정
    /// </summary>
    public ClusteringOptions? Clustering { get; init; }

    /// <summary>
    /// 영속성 설정
    /// </summary>
    public PersistenceOptions? Persistence { get; init; }

    /// <summary>
    /// 스트림 설정
    /// </summary>
    public StreamsOptions? Streams { get; init; }

    /// <summary>
    /// 대시보드 포트
    /// </summary>
    public int? DashboardPort { get; init; }

    /// <summary>
    /// 트랜잭션 설정
    /// </summary>
    public TransactionOptions? Transactions { get; init; }
}

/// <summary>
/// 클러스터링 설정
/// </summary>
public abstract record ClusteringOptions
{
    private ClusteringOptions() { }

    public record Localhost : ClusteringOptions;

    public record AdoNet(string DbInvariant, string ConnectionString) : ClusteringOptions;

    public record Redis(string ConnectionString) : ClusteringOptions;
}

/// <summary>
/// 영속성 설정
/// </summary>
public abstract record PersistenceOptions
{
    private PersistenceOptions() { }

    public record Memory : PersistenceOptions;

    public record AdoNet(string DbInvariant, string ConnectionString) : PersistenceOptions;

    public record Redis(string ConnectionString) : PersistenceOptions;
}

/// <summary>
/// 스트림 설정
/// </summary>
public abstract record StreamsOptions
{
    private StreamsOptions() { }

    public record Memory(string StreamProvider) : StreamsOptions;

    public record Kafka(string BootstrapServers, string StreamProvider = "Kafka") : StreamsOptions;

    public record EventHubs(string ConnectionString, string StreamProvider = "EventHubs") : StreamsOptions;
}

/// <summary>
/// 트랜잭션 설정
/// </summary>
public abstract record TransactionOptions
{
    private TransactionOptions() { }

    /// <summary>
    /// 메모리 기반 트랜잭션 로그
    /// </summary>
    public record Memory : TransactionOptions;

    /// <summary>
    /// Azure Storage 기반 트랜잭션 로그
    /// </summary>
    public record AzureStorage(string ConnectionString) : TransactionOptions;

    /// <summary>
    /// AdoNet 기반 트랜잭션 로그
    /// </summary>
    public record AdoNet(string DbInvariant, string ConnectionString) : TransactionOptions;
}


