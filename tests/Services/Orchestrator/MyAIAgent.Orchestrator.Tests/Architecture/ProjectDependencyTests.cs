/**
 * MyAIAgent | Orchestrator · Tests
 *
 * Guards service layer dependencies and keeps the public SDK independent of backend projects.
 */

using System.Xml.Linq;
using FluentAssertions;
using MyAIAgent.Common.Application.Models;
using MyAIAgent.Common.Infrastructure.Configuration;

namespace MyAIAgent.Orchestrator.Tests.Architecture;

public sealed class ProjectDependencyTests
{
    [Fact]
    public void ServiceProjects_ShouldOnlyReferenceAllowedLayers()
    {
        // Arrange
        var sourceDirectory = FindSourceDirectory();
        var violations = new List<string>();

        // Act
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
                "Client" => Array.Empty<string>(),
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

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void ConsoleClient_ShouldDependOnlyOnConsumerSdk()
    {
        // Arrange
        var projectFile = Path.Combine(FindSourceDirectory(), "Clients", "MyAIAgent.ConsoleClient", "MyAIAgent.ConsoleClient.csproj");

        // Act
        var references = XDocument.Load(projectFile).Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')));

        // Assert
        references.Should().Equal("MyAIAgent.Gateway.Client");
    }

    [Fact]
    public void SharedProjects_ShouldKeepDependenciesInwardAndDomainFrameworkFree()
    {
        // Arrange
        var commonDirectory = Path.Combine(FindSourceDirectory(), "Common");
        var allowed = new Dictionary<string, string[]>
        {
            ["MyAIAgent.Common.Domain"] = [],
            ["MyAIAgent.Common.Application"] = ["MyAIAgent.Common.Domain"],
            ["MyAIAgent.Common.Infrastructure"] = ["MyAIAgent.Common.Application"],
        };

        // Act / Assert
        foreach (var (name, dependencies) in allowed)
        {
            var project = XDocument.Load(Path.Combine(commonDirectory, name, name + ".csproj"));
            var references = project.Descendants("ProjectReference").Select(reference =>
                Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')));
            references.Should().BeEquivalentTo(dependencies);
            if (name.EndsWith(".Domain", StringComparison.Ordinal))
            {
                project.Descendants("PackageReference").Should().BeEmpty();
                project.Descendants("FrameworkReference").Should().BeEmpty();
            }
        }
    }

    [Fact]
    public void SharedResultsAndAuthorizationSettings_ShouldBelongToTheirOwningLayers()
    {
        // Arrange / Act
        var resultAssembly = typeof(ResponseModel<>).Assembly.GetName().Name;
        var authorizationAssembly = typeof(ServiceAuthorizationModel).Assembly.GetName().Name;

        // Assert
        resultAssembly.Should().Be("MyAIAgent.Common.Application");
        authorizationAssembly.Should().Be("MyAIAgent.Common.Infrastructure");
    }

    [Fact]
    public void PackageReferences_ShouldUseCentrallyPinnedVersions()
    {
        // Arrange
        var sourceDirectory = FindSourceDirectory();
        var configuration = XDocument.Load(Path.Combine(sourceDirectory, "..", "Directory.Packages.props"));
        var versions = configuration.Descendants("PackageVersion")
            .ToDictionary(package => package.Attribute("Include")!.Value, package => package.Attribute("Version")!.Value);

        // Act / Assert
        configuration.Descendants("ManagePackageVersionsCentrally").Single().Value.Should().Be("true");
        var projectFiles = Directory.GetFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(sourceDirectory, "..", "tests"), "*.csproj", SearchOption.AllDirectories));
        foreach (var projectFile in projectFiles)
        {
            foreach (var reference in XDocument.Load(projectFile).Descendants("PackageReference"))
            {
                reference.Attribute("Version").Should().BeNull();
                reference.Attribute("VersionOverride").Should().BeNull();
                versions.Should().ContainKey(reference.Attribute("Include")!.Value);
            }
        }
    }

    [Fact]
    public void ProductionProjects_ShouldNotReferenceTestProjects()
    {
        // Arrange
        var sourceDirectory = FindSourceDirectory();
        var testDirectory = Path.GetFullPath(Path.Combine(sourceDirectory, "..", "tests")) + Path.DirectorySeparatorChar;

        // Act
        var references = Directory.GetFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(project => XDocument.Load(project).Descendants("ProjectReference")
                .Select(reference => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference.Attribute("Include")!.Value))));

        // Assert
        references.Should().NotContain(reference => reference.StartsWith(testDirectory, StringComparison.Ordinal));
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
