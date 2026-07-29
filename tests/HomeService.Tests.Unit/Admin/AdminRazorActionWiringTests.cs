using System.Text.RegularExpressions;

namespace HomeService.Tests.Unit.Admin;

public sealed class AdminRazorActionWiringTests
{
    [Fact]
    public void AdminPages_DoNotExposeButtonsWithoutActions()
    {
        var pagesDirectory = FindRepositoryRoot()
            .GetDirectories("src", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("HomeService.Admin", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("Components", SearchOption.TopDirectoryOnly)
            .Single()
            .GetDirectories("Pages", SearchOption.TopDirectoryOnly)
            .Single();

        var missingActions = pagesDirectory
            .EnumerateFiles("*.razor", SearchOption.TopDirectoryOnly)
            .SelectMany(FindButtonsWithoutAction)
            .ToList();

        Assert.True(
            missingActions.Count == 0,
            "Admin buttons without @onclick or submit behavior:" + Environment.NewLine + string.Join(Environment.NewLine, missingActions));
    }

    private static IEnumerable<string> FindButtonsWithoutAction(FileInfo file)
    {
        var content = File.ReadAllText(file.FullName);
        var lineStarts = GetLineStarts(content);

        foreach (Match match in Regex.Matches(content, "<button\\b(?<attributes>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var attributes = match.Groups["attributes"].Value;
            if (attributes.Contains("@onclick", StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(attributes, "type\\s*=\\s*[\"']submit[\"']", RegexOptions.IgnoreCase))
            {
                continue;
            }

            var lineNumber = GetLineNumber(lineStarts, match.Index);
            yield return $"{file.Name}:{lineNumber}";
        }
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "HomeService.Admin"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "HomeService.Tests.Unit")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found from test output directory.");
    }

    private static List<int> GetLineStarts(string content)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        return starts;
    }

    private static int GetLineNumber(IReadOnlyList<int> lineStarts, int position)
    {
        var lineIndex = 0;
        for (var index = 0; index < lineStarts.Count; index++)
        {
            if (lineStarts[index] > position)
            {
                break;
            }

            lineIndex = index;
        }

        return lineIndex + 1;
    }
}
