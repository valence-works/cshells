namespace CShells.Tests.TestHelpers;

public static class FeatureDependencyData
{
    public static IEnumerable<object[]> CircularDependencyCases() =>
    [
        new object[] { new[] { "A" }, new[] { "A:B", "B:A" } },
        new object[] { new[] { "A" }, new[] { "A:B", "B:C", "C:A" } },
        new object[] { new[] { "A" }, new[] { "A:A" } }
    ];

    public static IEnumerable<object[]> UnknownDependencyCases() =>
    [
        new object[] { new[] { "A" }, "NonExistent", new[] { "A:NonExistent" } },
        new object[] { new[] { "A" }, "NonExistent", new[] { "A:B", "B:NonExistent" } },
        new object[] { new[] { "A" }, "MissingFeature", new[] { "A:MissingFeature" } }
    ];

    public static IEnumerable<object[]> TransitiveDependencyCases() =>
    [
        new object[] { new[] { "A" }, new[] { "C", "B", "A" }, new[] { "A:B", "B:C", "C" } },
        new object[] { new[] { "A" }, new[] { "E", "D", "C", "B", "A" }, new[] { "A:B", "B:C", "C:D", "D:E", "E" } },
        new object[] { new[] { "A" }, new[] { "B", "C", "A" }, new[] { "A:B,C", "B", "C" } }
    ];

    public static IEnumerable<object[]> DiamondDependencyCases() =>
    [
        new object[] { new[] { "A" }, new[] { "D", "B", "C", "A" }, new[] { "A:B,C", "B:D", "C:D", "D" } }
    ];
}
