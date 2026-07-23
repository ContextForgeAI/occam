namespace OccamMcp.Core.Access;

/// <summary>Single pure decision model shared by probe and transcode.</summary>
public static class AccessClassifier
{
    public static AccessAssessment Classify(AccessEvidence evidence)
    {
        var codes = new List<string>(6);

        if (evidence.StatusCode == 401)
        {
            codes.Add("http_401");
        }
        if (evidence.HasAuthenticationChallenge)
        {
            codes.Add("authentication_challenge");
        }
        if (evidence.RedirectedToLogin)
        {
            codes.Add("redirected_to_login");
        }
        if (evidence.HasBlockingIdentityUi)
        {
            codes.Add("blocking_identity_ui");
        }

        if (codes.Count > 0)
        {
            return new AccessAssessment(
                AccessDisposition.Restricted,
                0.95,
                evidence.Stage,
                codes,
                "use_session");
        }

        if (evidence.HasUsableContent)
        {
            codes.Add("usable_public_content");
            if (evidence.AuthenticationTerminology)
            {
                codes.Add("authentication_terminology_non_decisive");
            }
            return new AccessAssessment(
                AccessDisposition.Open,
                0.85,
                evidence.Stage,
                codes,
                "continue");
        }

        if (evidence.AuthenticationTerminology)
        {
            codes.Add("authentication_terminology_only");
        }
        if (evidence.PasswordField)
        {
            codes.Add("password_field_without_blocking_context");
        }
        if (codes.Count == 0)
        {
            codes.Add("insufficient_access_evidence");
        }

        return new AccessAssessment(
            AccessDisposition.Unknown,
            0.25,
            evidence.Stage,
            codes,
            "retry_or_inspect");
    }
}
