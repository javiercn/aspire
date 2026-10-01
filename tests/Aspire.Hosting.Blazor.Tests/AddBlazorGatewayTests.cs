// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dcp;
using Aspire.Hosting.Dcp.Model;
using Aspire.Hosting.Tests.Utils;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable ASPIREDOCKERFILEBUILDER001 // DockerfileBuilder is experimental
#pragma warning disable ASPIREPROJECTS001 // ProjectLaunchArgsOverrideAnnotation is experimental

namespace Aspire.Hosting.Blazor.Tests;

public class AddBlazorGatewayTests(ITestOutputHelper testOutputHelper)
{
    private const string GatewayPackageId = "Microsoft.AspNetCore.Components.Gateway.Cli";
    private const string GatewayPackageVersion = "11.0.0-rc.1.26425.128";

    [Fact]
    public void AddBlazorGateway_PreservesProjectResourceApiAndUsesToolForRunMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        IResourceBuilder<ProjectResource> gateway = builder.AddBlazorGateway("gateway");

        Assert.EndsWith(
            Path.Combine("Scripts", "Gateway.cs"),
            gateway.Resource.GetProjectMetadata().ProjectPath);

        var executable = Assert.Single(gateway.Resource.Annotations.OfType<ExecutableAnnotation>());
        Assert.Equal("dotnet", executable.Command);
        Assert.Equal(builder.AppHostDirectory, executable.WorkingDirectory);
        Assert.Single(gateway.Resource.Annotations.OfType<ProjectLaunchArgsOverrideAnnotation>());

        var initialSnapshot = Assert.Single(gateway.Resource.Annotations.OfType<ResourceSnapshotAnnotation>()).InitialSnapshot;
        var source = Assert.Single(
            initialSnapshot.Properties,
            property => property.Name == CustomResourceKnownProperties.Source);
        Assert.Equal(string.Empty, source.Value);

        Assert.Collection(
            gateway.Resource.Annotations.OfType<EndpointAnnotation>().OrderBy(endpoint => endpoint.Name),
            endpoint => Assert.Equal("http", endpoint.Name),
            endpoint => Assert.Equal("https", endpoint.Name));
    }

    [Fact]
    public async Task AddBlazorGateway_ConfiguresGatewayToolArguments()
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        var gateway = builder.AddBlazorGateway("gateway");
        using var app = builder.Build();

        var args = await ArgumentEvaluator.GetArgumentListAsync(gateway.Resource);

        Assert.Collection(
            args,
            arg => Assert.Equal("tool", arg),
            arg => Assert.Equal("exec", arg),
            arg => Assert.Equal(GatewayPackageId, arg),
            arg => Assert.Equal("--version", arg),
            arg => Assert.Equal(GatewayPackageVersion, arg),
            arg => Assert.Equal("--yes", arg),
            arg => Assert.Equal("--", arg),
            arg => Assert.Equal("--environment", arg),
            arg => Assert.Equal(builder.Environment.EnvironmentName, arg),
            arg => Assert.Equal("--Logging:LogLevel:Microsoft=Warning", arg),
            arg => Assert.Equal("--Logging:LogLevel:Microsoft.Hosting.Lifetime=Information", arg),
            arg => Assert.Equal("--Logging:LogLevel:System.Net.Http.HttpClient.OtlpExporter=Warning", arg));
    }

    [Fact]
    public async Task AddBlazorGateway_RendersCompleteProcessLaunchPlan()
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        var gateway = builder.AddBlazorGateway("gateway");
        using var app = builder.Build();

        await AssertGatewayProcessLaunchPlanAsync(gateway.Resource, builder, app.Services);
    }

    [Fact]
    public async Task AddBlazorGateway_InPublishMode_UsesFileBasedGateway()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        IResourceBuilder<ProjectResource> gateway = builder.AddBlazorGateway("gateway");

        var container = Assert.Single(builder.Resources.OfType<ContainerResource>());
        Assert.Equal("gateway", container.Name);

        var build = Assert.Single(container.Annotations.OfType<DockerfileBuildAnnotation>());
        Assert.NotNull(build.DockerfileFactory);

        var context = new DockerfileFactoryContext
        {
            Services = builder.Services.BuildServiceProvider(),
            Resource = container,
            CancellationToken = CancellationToken.None
        };

        var dockerfile = await build.DockerfileFactory(context);

        await Verify(dockerfile, extension: "Dockerfile");

        Assert.Empty(gateway.Resource.Annotations.OfType<ProjectLaunchArgsOverrideAnnotation>());
        Assert.Empty(gateway.Resource.Annotations.OfType<ExecutableAnnotation>());
    }

    [Fact]
    public async Task BlazorWasmPublishCompanion_UsesNet11Sdk()
    {
        var dockerfile = BlazorGatewayExtensions.BuildBlazorWasmPublishDockerfile(
            "Blazor/Blazor.csproj",
            ".aspire/scripts/PrefixEndpoints.cs",
            "app");

        await Verify(dockerfile, extension: "Dockerfile");
    }

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net10.0")]
    [InlineData("net11.0")]
    public void ValidateBlazorWasmPublishTargetFramework_AcceptsSupportedFrameworks(string targetFramework)
    {
        BlazorGatewayExtensions.ValidateBlazorWasmPublishTargetFramework(targetFramework);
    }

    [Theory]
    [InlineData("net12.0")]
    [InlineData("net10.0;net11.0")]
    [InlineData("netstandard2.1")]
    [InlineData("invalid")]
    public void ValidateBlazorWasmPublishTargetFramework_RejectsUnsupportedFrameworks(string targetFramework)
    {
        Assert.Throws<NotSupportedException>(
            () => BlazorGatewayExtensions.ValidateBlazorWasmPublishTargetFramework(targetFramework));
    }

    [Fact]
    public async Task GetTargetFrameworkAsync_UsesReleaseConfiguration()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var projectPath = Path.Combine(directory.FullName, "Client.csproj");
            await File.WriteAllTextAsync(
                projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework Condition="'$(Configuration)' == 'Debug'">net12.0</TargetFramework>
                    <TargetFramework Condition="'$(Configuration)' == 'Release'">net11.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var targetFramework = await BlazorWasmAppBuilder.GetTargetFrameworkAsync(
                projectPath,
                NullLogger.Instance,
                CancellationToken.None);

            Assert.Equal("net11.0", targetFramework);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetSolutionRoot_UsesNearestSolutionAncestor()
    {
        var solutionRoot = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(solutionRoot.FullName, "Test.slnx"), "<Solution />");
            var appHostDirectory = Directory.CreateDirectory(Path.Combine(solutionRoot.FullName, "src", "AppHost")).FullName;

            var projectDirectory = Directory.CreateDirectory(Path.Combine(solutionRoot.FullName, "src", "Client")).FullName;

            Assert.Equal(solutionRoot.FullName, BlazorGatewayExtensions.GetSolutionRoot(appHostDirectory, projectDirectory));
        }
        finally
        {
            solutionRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetSolutionRoot_WithoutSolution_Throws()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var appHostDirectory = Directory.CreateDirectory(Path.Combine(directory.FullName, "AppHost")).FullName;

            var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.FullName, "Client")).FullName;

            var exception = Assert.Throws<InvalidOperationException>(
                () => BlazorGatewayExtensions.GetSolutionRoot(appHostDirectory, projectDirectory));

            Assert.Contains("requires a .sln or .slnx file", exception.Message);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetSolutionRoot_SkipsSolutionThatDoesNotContainProject()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var appHostDirectory = Directory.CreateDirectory(Path.Combine(directory.FullName, "AppHost")).FullName;
            File.WriteAllText(Path.Combine(appHostDirectory, "AppHost.slnx"), "<Solution />");
            var projectDirectory = Directory.CreateDirectory(Path.Combine(directory.FullName, "Client")).FullName;

            var exception = Assert.Throws<InvalidOperationException>(
                () => BlazorGatewayExtensions.GetSolutionRoot(appHostDirectory, projectDirectory));

            Assert.Contains("that also contains the client project", exception.Message);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void WithBlazorClientApp_RunModeGateway_ForwardsServiceReferences()
    {
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var weatherApi = builder.AddProject<TestProjectMetadata>("weatherapi")
            .WithHttpEndpoint();
        var wasmApp = builder.AddBlazorWasmApp("store", "Store/Store.csproj")
            .WithReference(weatherApi);

        var gateway = builder.AddBlazorGateway("gateway")
            .WithBlazorClientApp(wasmApp);

        var endpointReference = Assert.Single(
            gateway.Resource.Annotations.OfType<EndpointReferenceAnnotation>(),
            annotation => annotation.Resource.Name == "weatherapi");

        Assert.True(endpointReference.UseAllEndpoints);
        Assert.Same(gateway.Resource, wasmApp.Resource.Parent);
    }

    private sealed class TestProjectMetadata : IProjectMetadata
    {
        public string ProjectPath => "TestProject/TestProject.csproj";

        public LaunchSettings LaunchSettings { get; } = new();
    }

    internal static async Task AssertGatewayProcessLaunchPlanAsync(
        IResource resource,
        IDistributedApplicationBuilder builder,
        IServiceProvider services)
    {
        var executionContext = new DistributedApplicationExecutionContext(
            new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)
            {
                Services = services
            });
        var executionConfiguration = await ExecutionConfigurationBuilder.Create(resource)
            .WithArgumentsConfig()
            .BuildAsync(executionContext, NullLogger.Instance, CancellationToken.None);

        Assert.Null(executionConfiguration.Exception);

        var plan = await ExecutableCreator.ResolveLaunchPlanAsync(
            resource,
            executionConfiguration,
            builder.Configuration,
            new DistributedApplicationOptions(),
            new ExecutableLaunchPolicy(builder.Configuration),
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(ExecutableLaunchMechanism.Process, plan.Mechanism);
        Assert.Equal("dotnet", plan.Command);
        Assert.Equal(builder.AppHostDirectory, plan.WorkingDirectory);
        Assert.Equal(
            [
                "tool",
                "exec",
                GatewayPackageId,
                "--version",
                GatewayPackageVersion,
                "--yes",
                "--",
                "--environment",
                builder.Environment.EnvironmentName,
                "--Logging:LogLevel:Microsoft=Warning",
                "--Logging:LogLevel:Microsoft.Hosting.Lifetime=Information",
                "--Logging:LogLevel:System.Net.Http.HttpClient.OtlpExporter=Warning"
            ],
            plan.Arguments);

        var executable = Executable.Create("gateway-12345678", "stale");
        var renderedResource = new RenderedModelResource<Executable>(resource, executable);
        ExecutableCreator.Render(
            renderedResource,
            plan,
            pemCertificates: null,
            NullLogger<ExecutableCreator>.Instance);

        Assert.Equal(ExecutionType.Process, executable.Spec.ExecutionType);
        Assert.Equal("dotnet", executable.Spec.ExecutablePath);
        Assert.Equal(builder.AppHostDirectory, executable.Spec.WorkingDirectory);
        Assert.Equal(plan.Arguments, executable.Spec.Args);
    }
}
