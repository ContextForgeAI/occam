using OccamMcp.Core.Exam;
using OccamMcp.Core.Transport;
using Xunit;

namespace OccamMcp.Core.Tests.Exam;

public sealed class SessionToolSurfaceTests
{
    [Fact]
    public void StartsAtInitialProfile_AndAlwaysExposesExamSubmit()
    {
        var surface = new SessionToolSurface(OccamToolProfile.Reader, isPinned: false);

        Assert.Equal(OccamToolProfile.Reader, surface.ProfileId);
        Assert.True(surface.IsExposed(SessionToolSurface.ExamSubmitToolName));
        Assert.True(surface.IsExposed("occam_transcode"));
        Assert.False(surface.IsExposed("occam_playbook_heal"));
    }

    [Fact]
    public void TryApplyExamTier_ChangesSurface_WhenNotPinned()
    {
        var surface = new SessionToolSurface(OccamToolProfile.Reader, isPinned: false);

        Assert.True(surface.TryApplyExamTier(AgentTier.Weak));
        Assert.Equal(OccamToolProfile.Minimal, surface.ProfileId);
        Assert.Equal(1, surface.Generation);
        Assert.True(surface.IsExposed("occam"));
        Assert.False(surface.IsExposed("occam_transcode"));
        Assert.True(surface.IsExposed(SessionToolSurface.ExamSubmitToolName));
    }

    [Fact]
    public void TryApplyExamTier_NoOp_WhenPinned()
    {
        var surface = new SessionToolSurface(OccamToolProfile.Reader, isPinned: true);

        Assert.False(surface.TryApplyExamTier(AgentTier.Strong));
        Assert.Equal(OccamToolProfile.Reader, surface.ProfileId);
        Assert.Equal(0, surface.Generation);
    }

    [Fact]
    public void TryApplyExamTier_NoOp_WhenUnchanged()
    {
        var surface = new SessionToolSurface(OccamToolProfile.Basic, isPinned: false);

        Assert.False(surface.TryApplyExamTier(AgentTier.Medium));
        Assert.Equal(0, surface.Generation);
    }

    [Fact]
    public void StrongTier_WidensToFull()
    {
        var surface = new SessionToolSurface(OccamToolProfile.Reader, isPinned: false);

        Assert.True(surface.TryApplyExamTier(AgentTier.Strong));
        Assert.Equal(OccamToolProfile.Full, surface.ProfileId);
        Assert.Equal(
            OccamMcpServerRegistration.OccamToolNames.Length,
            surface.GetExposedCoreToolNames().Length);
    }
}
