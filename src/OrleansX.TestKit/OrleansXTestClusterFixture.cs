using Orleans.TestingHost;
using Xunit;

namespace OrleansX.TestKit;

/// <summary>
/// OrleansX 테스트 클러스터 Fixture
/// </summary>
public class OrleansXTestClusterFixture : IDisposable
{
    public TestCluster Cluster { get; private set; }

    public OrleansXTestClusterFixture()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster?.StopAllSilos();
        Cluster?.Dispose();
    }

    private class TestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorage("Default");
            siloBuilder.AddMemoryGrainStorage("PubSubStore");
            siloBuilder.AddMemoryStreams("Default");
        }
    }
}

/// <summary>
/// 컬렉션 정의 (xUnit에서 Fixture를 공유하기 위함)
/// </summary>
[CollectionDefinition("OrleansXCluster")]
public class OrleansXTestClusterCollection : ICollectionFixture<OrleansXTestClusterFixture>
{
}




