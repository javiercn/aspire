// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Dashboard.Components.CustomIcons;
using Aspire.Dashboard.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Aspire.Dashboard.Components.Layout;

public partial class DesktopNavMenu : ComponentBase, IDisposable
{
    internal static Icon ResourcesIcon(bool active = false) =>
        active ? new Octicons.Size24.AppFolder()
                  : new Octicons.Size24.AppFolder();

    internal static Icon ConsoleLogsIcon(bool active = false) =>
        active ? new Octicons.Size24.SlideText()
                  : new Octicons.Size24.SlideText();

    internal static Icon StructuredLogsIcon(bool active = false) =>
        active ? new Octicons.Size24.SlideTextSparkle()
                  : new Octicons.Size24.SlideTextSparkle();

    internal static Icon TracesIcon(bool active = false) =>
        active ? new Octicons.Size24.GanttChart()
                  : new Octicons.Size24.GanttChart();

    internal static Icon MetricsIcon(bool active = false) =>
        active ? new Octicons.Size24.ChartMultiple()
                  : new Octicons.Size24.ChartMultiple();

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    // NavLink has limited options for matching the current address when highlighting itself as active.
    // Can't use Match.All because of the query string. Can't use Match.Prefix always because it matches every page.
    // Track whether we are on the resource page manually. If we are then change match to prefix to allow the query string.
    private bool _isResources;

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
        ProcessNavigationUri(NavigationManager.Uri);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        ProcessNavigationUri(e.Location);
    }

    private void ProcessNavigationUri(string location)
    {
        if (Uri.TryCreate(location, UriKind.Absolute, out var result))
        {
            var trimmedPath = result.AbsolutePath.TrimStart('/');
            var isResources = trimmedPath == DashboardUrls.ResourcesBasePath || trimmedPath[0] == '?';
            if (isResources != _isResources)
            {
                _isResources = isResources;
                StateHasChanged();
            }
        }
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}
