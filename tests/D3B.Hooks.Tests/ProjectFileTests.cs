namespace D3B.Hooks.Tests;

public sealed class ProjectFileTests
{
    [Fact]
    public void ASourceFileBelongsToTheNearestProjectAboveIt()
    {
        using var folder = new TemporaryFolder();
        folder.Write("src/D3B.Core/D3B.Core.csproj", "<Project />");
        folder.Write("src/D3B.Core/Modules/BotBuilder.cs", "class A;");

        Assert.Equal("src/D3B.Core/D3B.Core.csproj", ProjectFile.Find(folder.Path, "src/D3B.Core/Modules/BotBuilder.cs"));
    }

    [Fact]
    public void AScriptBelongsToNoProject()
    {
        using var folder = new TemporaryFolder();
        folder.Write("scripts/client.cs", "class A;");
        folder.Write("src/D3B.Core/D3B.Core.csproj", "<Project />");

        Assert.Null(ProjectFile.Find(folder.Path, "scripts/client.cs"));
    }

    [Fact]
    public void AProjectOutsideTheRootIsNotLookedFor()
    {
        using var folder = new TemporaryFolder();
        folder.Write("outer.csproj", "<Project />");
        var root = Path.Combine(folder.Path, "inner");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "A.cs"), "class A;");

        Assert.Null(ProjectFile.Find(root, "A.cs"));
    }
}
