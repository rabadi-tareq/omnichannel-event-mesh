using DsgOmnichannel.IntegrationTests.Infrastructure;

namespace DsgOmnichannel.IntegrationTests;

/// <summary>
/// xUnit collection that shares a single <see cref="ApiFactory"/> instance across all
/// HTTP-level integration test classes. This ensures the SQL Server Testcontainer and
/// WebApplicationFactory are started once per test run rather than once per class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Integration Tests";
}
