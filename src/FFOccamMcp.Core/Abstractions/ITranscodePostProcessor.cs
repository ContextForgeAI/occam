using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Abstractions;

public interface ITranscodePostProcessor
{
    int Order { get; }
    TranscodeOutcome Process(TranscodeOutcome input, TranscodeContext ctx);
}
