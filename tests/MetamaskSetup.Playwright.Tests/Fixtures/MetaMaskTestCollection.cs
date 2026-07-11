namespace MetamaskSetup.Playwright.Tests.Fixtures;

/// <summary>
/// xUnit collection that shares a single <see cref="MetaMaskExtensionFixture"/> across all
/// integration test classes. MetaMask is downloaded (or located) only once per test run.
///
/// All test classes must be decorated with <c>[Collection(MetaMaskTestCollection.Name)]</c>
/// and accept <see cref="MetaMaskExtensionFixture"/> via constructor injection.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MetaMaskTestCollection : ICollectionFixture<MetaMaskExtensionFixture>
{
    public const string Name = "MetaMask Integration Tests";
}
