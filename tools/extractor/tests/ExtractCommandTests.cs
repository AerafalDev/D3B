namespace D3B.Extractor.Tests;

public sealed class ExtractCommandTests
{
    [Fact]
    public void TheExtractorNamesItsVersion()
    {
        var (code, output, _) = Run();

        Assert.Equal(ExtractCommand.Done, code);
        Assert.Contains($"D3B extractor {ExtractCommand.Version}", output, StringComparison.Ordinal);
        Assert.StartsWith("0.1.0", ExtractCommand.Version, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownOptionExitsWithThree()
    {
        var (code, _, error) = Run("--clinet", "somewhere");

        Assert.Equal(ExtractCommand.Misused, code);
        Assert.Contains("--clinet", error, StringComparison.Ordinal);
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = ExtractCommand.Run(args, output, error);

        return (code, output.ToString(), error.ToString());
    }
}
