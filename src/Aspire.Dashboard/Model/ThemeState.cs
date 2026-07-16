// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Model;

/// <summary>
/// App-level switch that selects the dashboard's visual variant: the classic Fluent look or the
/// GitHub look. This is the single source of truth consumed by three places:
/// <list type="bullet">
///   <item>C# icon rendering (<c>Octicons</c> returns Octicon or Fluent glyphs based on <see cref="UseGitHubUI"/>).</item>
///   <item>The <c>data-ui-variant</c> attribute rendered on <c>&lt;html&gt;</c> (gates github-theme.css).</item>
///   <item>app-theme.js (branches the Fluent design-token reseed on the attribute).</item>
/// </list>
/// Controlled by the <c>ASPIRE_DASHBOARD_UI_VARIANT</c> environment variable. Set it to
/// <c>fluent</c> to switch back to the classic look; anything else (including unset) uses the
/// GitHub look. It is read once at startup, so changing it takes effect on the next dashboard launch.
/// </summary>
public static class ThemeState
{
    public const string VariantGitHub = "github";
    public const string VariantFluent = "fluent";

    /// <summary>The active variant string, either <see cref="VariantGitHub"/> or <see cref="VariantFluent"/>.</summary>
    public static string Variant { get; } =
        string.Equals(Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_UI_VARIANT"), VariantFluent, StringComparison.OrdinalIgnoreCase)
            ? VariantFluent
            : VariantGitHub;

    /// <summary><see langword="true"/> when the GitHub look is active.</summary>
    public static bool UseGitHubUI { get; } = Variant == VariantGitHub;
}
