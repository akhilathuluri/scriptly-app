namespace Scriptly.Models;

/// <summary>
/// Describes developer-facing metadata shown in the More Info window.
/// Kept as a model to make future updates and localization straightforward.
/// </summary>
public class DeveloperInfo
{
    public string AppName { get; set; } = "Scriptly";
    public string DeveloperName { get; set; } = "Scriptly Team";
    public string DeveloperMessage { get; set; } =
        "Thank you for using Scriptly. Your feedback helps shape upcoming improvements and new features.";
    public string ContactUrl { get; set; } = "https://github.com/scriptly-app/scriptly/discussions";
    public string ReportIssueUrl { get; set; } = "https://github.com/scriptly-app/scriptly/issues/new/choose";

    public static DeveloperInfo Default => new();
}
