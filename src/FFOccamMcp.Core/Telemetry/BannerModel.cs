using OccamMcp.Core.Transport;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Telemetry;

public sealed record BannerStatusRow(bool Active, string Label, string Value);

public sealed record BannerModel(
    string Architecture,
    string Mode,
    string Workers,
    IReadOnlyList<BannerStatusRow> StatusRows,
    string ListeningHint);

public interface IBannerContentProvider
{
    BannerModel Build(WorkerPaths paths);
}

public sealed class DefaultBannerContentProvider : IBannerContentProvider
{
    public BannerModel Build(WorkerPaths paths)
    {
        var headlessActive = !paths.HasDistinctBrowserWorker;
        var headlessValue = paths.HasDistinctBrowserWorker
            ? "Playwright worker ready"
            : "Bmax fallback mode active";

        return new BannerModel(
            Architecture: ".NET 10 Core (Native AOT)",
            Mode: "L0 extract-only",
            Workers: "Node http + browser",
            StatusRows:
            [
                new BannerStatusRow(true, "Extract", "Live only"),
                new BannerStatusRow(
                    true,
                    "Tools",
                    $"{OccamToolProfile.GetExposedToolNames().Length} occam_* ({OccamToolProfile.Resolve()})"),
                new BannerStatusRow(true, "Playbooks", "seeds + heal/save"),
                new BannerStatusRow(headlessActive, "Headless", headlessValue),
            ],
            ListeningHint: "Listening via stdio...");
    }
}
