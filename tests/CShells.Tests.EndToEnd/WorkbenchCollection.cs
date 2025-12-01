namespace CShells.Tests.EndToEnd;

/// <summary>
/// Collection fixture to share the WorkbenchApplicationFactory across all test classes.
/// This ensures that the static configuration in ApplicationBuilderExtensions (ConfigureWebShellFeatures)
/// is only run once and all tests share the same application instance.
/// </summary>
[CollectionDefinition("Workbench")]
public class WorkbenchCollection : ICollectionFixture<WorkbenchApplicationFactory>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
