using CitySim.Core;

namespace CitySim.Tests;

public sealed class EnvironmentSmokeTests
{
    [Fact]
    public void CoreProjectIsReferencedByTests()
    {
        Assert.Equal("CitySimLab", ProjectAnchor.Name);
    }
}
