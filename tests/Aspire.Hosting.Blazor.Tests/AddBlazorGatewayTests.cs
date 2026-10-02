// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dcp;
using Aspire.Hosting.Dcp.Model;
using Aspire.Hosting.Tests.Utils;
using Aspire.Hosting.Utils;
using Aspire.TestUtilities;
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
            "app",
            "11.0.100-rc.1.26425.128",
            "net11.0");

        await Verify(dockerfile, extension: "Dockerfile");
    }

    [Theory]
    [InlineData("8.0.408", "net8.0")]
    [InlineData("9.0.203", "net9.0")]
    [InlineData("10.0.201", "net10.0")]
    [InlineData("11.0.100-rc.1.26425.128", "net11.0")]
    public void BlazorWasmPublishCompanion_InstallsClientSdkAlongsideGatewaySdk(string sdkVersion, string framework)
    {
        var dockerfile = BlazorGatewayExtensions.BuildBlazorWasmPublishDockerfile(
            "Client/Client.csproj", ".aspire/scripts/PrefixEndpoints.cs", "app", sdkVersion, framework);

        Assert.Contains($"--version {sdkVersion} --install-dir /opt/client-dotnet --no-path", dockerfile);
        Assert.Contains($"/opt/client-dotnet/dotnet publish \"Client.csproj\" -f {framework}", dockerfile);
        Assert.Contains("WORKDIR /tmp", dockerfile);
        Assert.Contains("cp \"/src/.aspire/scripts/PrefixEndpoints.cs\" /tmp/PrefixEndpoints.cs", dockerfile);
        Assert.Contains("dotnet run /tmp/PrefixEndpoints.cs", dockerfile);
    }

    [Theory]
    [InlineData("")]
    [InlineData("7.0.410")]
    [InlineData("12.0.100")]
    [InlineData("10.0.201;echo")]
    public void BlazorWasmPublishCompanion_RejectsUnsupportedSdk(string sdkVersion)
    {
        Assert.Throws<NotSupportedException>(() => BlazorGatewayExtensions.BuildBlazorWasmPublishDockerfile(
            "Client/Client.csproj", ".aspire/scripts/PrefixEndpoints.cs", "app", sdkVersion, "net10.0"));
    }

    [Fact]
    [OuterloopTest("Builds the generated publish image and downloads the pinned client SDK")]
    [RequiresFeature(TestFeature.ContainerRuntime | TestFeature.ContainerImageBuild)]
    public async Task BlazorWasmPublishCompanion_BuildsClientPinnedToOlderSdk()
    {
        using var fileSystem = new TestFileSystemService();
        using var directory = fileSystem.TempDirectory.CreateTempSubdirectory();
        var clientDirectory = Path.Combine(directory.Path, "Client");
        Directory.CreateDirectory(Path.Combine(clientDirectory, "wwwroot"));
        await File.WriteAllTextAsync(Path.Combine(clientDirectory, "global.json"),
            """{"sdk":{"version":"10.0.201","rollForward":"latestFeature"}}""");
        File.Copy(Path.Combine(clientDirectory, "global.json"), Path.Combine(directory.Path, "global.json"));
        await File.WriteAllTextAsync(Path.Combine(clientDirectory, "Client.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
              <PropertyGroup>
                <TargetFrameworks>net10.0</TargetFrameworks>
                <RestoreSources>https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json</RestoreSources>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.0" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(clientDirectory, "Program.cs"),
            """
            using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
            await WebAssemblyHostBuilder.CreateDefault(args).Build().RunAsync();
            """);
        await File.WriteAllTextAsync(Path.Combine(clientDirectory, "wwwroot", "index.html"),
            """<!DOCTYPE html><html><head><base href="/" /></head><body><script src="_framework/blazor.webassembly.js"></script></body></html>""");
        var script = await File.ReadAllTextAsync(
            Path.Combine(Path.GetDirectoryName(typeof(BlazorGatewayExtensions).Assembly.Location)!, "Scripts", "PrefixEndpoints.cs"));
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "PrefixEndpoints.cs"),
            "#:property RestoreSources=https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json\n" + script);

        var dockerfile = BlazorGatewayExtensions.BuildBlazorWasmPublishDockerfile(
            "Client/Client.csproj", "PrefixEndpoints.cs", "app", "10.0.201", "net10.0");
        dockerfile += """

            RUN test -f /app/output/wwwroot/app/index.html && \
                test -s /app/output/app.endpoints.json && \
                cd /src/Client && /opt/client-dotnet/dotnet --version | grep -Fx 10.0.201 && \
                cd /tmp && dotnet --version | grep '^11\.'
            CMD ["sleep", "infinity"]
            """;
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "Dockerfile"), dockerfile);

        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        builder.AddDockerfile("client-publish", directory.Path);
        using var app = builder.Build();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceAsync("client-publish", KnownResourceStates.Running, timeout.Token);
        await app.StopAsync(TestContext.Current.CancellationToken);
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
    public async Task GetPublishPropertiesAsync_UsesReleaseConfiguration()
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

            var properties = await BlazorWasmAppBuilder.GetPublishPropertiesAsync(
                projectPath,
                NullLogger.Instance,
                CancellationToken.None);

            Assert.NotNull(properties);
            Assert.Equal("net11.0", properties.TargetFramework);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("net11.0", "net11.0")]
    [InlineData(" ; net11.0 ; ; ", "net11.0")]
    [InlineData("net10.0;net11.0", "net10.0;net11.0")]
    public async Task GetPublishPropertiesAsync_NormalizesSingleEntryTargetFrameworksAndResolvesSdk(string targetFrameworks, string expectedFramework)
    {
        using var fileSystem = new TestFileSystemService();
        using var directory = fileSystem.TempDirectory.CreateTempSubdirectory();
        var projectPath = Path.Combine(directory.Path, "Client.csproj");
        await File.WriteAllTextAsync(projectPath,
            $"""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFrameworks>{targetFrameworks}</TargetFrameworks></PropertyGroup></Project>""");

        var properties = await BlazorWasmAppBuilder.GetPublishPropertiesAsync(
            projectPath, NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.NotNull(properties);
        Assert.Equal(expectedFramework, properties.TargetFramework);
        Assert.NotEmpty(properties.NETCoreSdkVersion);
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
