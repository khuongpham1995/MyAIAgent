using System.Xml.Linq;
using FluentAssertions;

namespace MyAIAgent.Orchestrator.Tests.Architecture;

public sealed class ProjectDependencyTests
{
    [Fact]
    public void ServiceProjects_ShouldOnlyReferenceAllowedLayers()
    {
        var sourceDirectory = FindSourceDirectory();
        var violations = new List<string>();

        foreach (var projectFile in Directory.GetFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFile);
            var segments = projectName.Split('.');
            if (segments.Length != 3 || segments[0] != "MyAIAgent" ||
                segments[1] is "Common" or "All" || segments[2] is "Tests")
                continue;

            var service = segments[1];
            var layer = segments[2];
            var allowed = layer switch
            {
                "Domain" => new[] { "MyAIAgent.Common.Domain" },
                "Application" => new[] { $"MyAIAgent.{service}.Domain", "MyAIAgent.Common.Application" },
                "Infrastructure" => new[]
                {
                    $"MyAIAgent.{service}.Domain", $"MyAIAgent.{service}.Application",
                    "MyAIAgent.Common.Infrastructure"
                },
                "Presentation" => new[]
                {
                    $"MyAIAgent.{service}.Domain", $"MyAIAgent.{service}.Application",
                    $"MyAIAgent.{service}.Infrastructure"
                },
                _ => throw new InvalidOperationException($"Unexpected layer: {layer}")
            };

            var references = XDocument.Load(projectFile)
                .Descendants("ProjectReference")
                .Select(reference => Path.GetFileNameWithoutExtension(
                    reference.Attribute("Include")!.Value.Replace('\\', '/')));

            violations.AddRange(references
                .Where(reference => !allowed.Contains(reference))
                .Select(reference => $"{projectName} references {reference}"));
        }

        violations.Should().BeEmpty();
    }

    private static string FindSourceDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var sourceDirectory = Path.Combine(directory.FullName, "src");
            if (File.Exists(Path.Combine(sourceDirectory, "MyAIAgent.slnx")))
                return sourceDirectory;
        }

        throw new DirectoryNotFoundException("Could not locate the solution source directory.");
    }
}
