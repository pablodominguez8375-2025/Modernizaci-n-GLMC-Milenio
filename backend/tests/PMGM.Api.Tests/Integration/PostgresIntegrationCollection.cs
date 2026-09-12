using Xunit;

namespace PMGM.Api.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresIntegrationCollection
{
    public const string Name = "PostgresIntegration";
}
