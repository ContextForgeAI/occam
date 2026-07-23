namespace OccamMcp.Core.Telemetry;

public interface IOccamLogSink
{
    void Write(OccamLogEvent logEvent);
}
