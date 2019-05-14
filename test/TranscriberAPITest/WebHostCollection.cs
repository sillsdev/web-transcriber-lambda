using Xunit;

namespace TranscriberAPI.Tests
{
    [CollectionDefinition("WebHostCollection")]
    public class WebHostCollection
        : ICollectionFixture<TestFixture<TestStartup>>
    { }
}
