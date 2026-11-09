namespace D3B.Hooks.Tests;

public sealed class ProjectPathTests
{
    [Fact]
    public void APathInsideTheProjectIsRelativeWithForwardSlashes()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "src", "D3B.Core", "A.cs");

        Assert.Equal("src/D3B.Core/A.cs", ProjectPath.Relative(folder.Path, path));
    }

    [Fact]
    public void ARelativePathIsReadFromTheProjectsRoot()
    {
        using var folder = new TemporaryFolder();

        Assert.Equal("docs/a.md", ProjectPath.Relative(folder.Path, "docs/a.md"));
    }

    [Fact]
    public void ADotSegmentIsResolved()
    {
        using var folder = new TemporaryFolder();

        Assert.Equal("a.md", ProjectPath.Relative(folder.Path, "docs/../a.md"));
    }

    [Fact]
    public void APathOutsideTheProjectHasNoRelativeForm()
    {
        using var folder = new TemporaryFolder();
        using var other = new TemporaryFolder();

        Assert.Null(ProjectPath.Relative(folder.Path, Path.Combine(other.Path, "a.cs")));
        Assert.Null(ProjectPath.Relative(folder.Path, "../a.cs"));
    }

    [Fact]
    public void AGitBashDriveIsReadAsAWindowsDrive()
    {
        Assert.Equal("c:/Users/x/p", ProjectPath.FromGitBash("/c/Users/x/p", windows: true));
        Assert.Equal("/c/Users/x/p", ProjectPath.FromGitBash("/c/Users/x/p", windows: false));
        Assert.Equal("/usr/lib", ProjectPath.FromGitBash("/usr/lib", windows: true));
    }
}
