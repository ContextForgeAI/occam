using OccamMcp.Core.Routing;
using OccamMcp.Core.Session;

namespace OccamMcp.L0Gate;

internal static class L2SessionUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        RunSessionIdSanitize(assert);
        RunSessionProfileParse(assert);
        RunHeaderMerge(assert);
        RunFetchHeadersScopeCleanup(assert);
        RunPrivacyClassifier(assert);
    }

    private static void RunSessionIdSanitize(Action<string, bool> assert)
    {
        assert("session invalid traversal", SessionProfileHeaders.Resolve("../etc/passwd").Status == SessionProfileStatus.InvalidId);
        assert("session invalid slash", SessionProfileHeaders.Resolve("foo/bar").Status == SessionProfileStatus.InvalidId);
        assert(
            "session missing profile",
            SessionProfileHeaders.Resolve("__gate_missing_profile__").Status == SessionProfileStatus.NotFound);
    }

    private static void RunSessionProfileParse(Action<string, bool> assert)
    {
        var root = Path.Combine(Path.GetTempPath(), $"occam-session-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var previousRoot = SessionProfileHeaders.SessionsRootOverrideForTests;
        SessionProfileHeaders.SessionsRootOverrideForTests = root;
        try
        {
            File.WriteAllText(
                Path.Combine(root, "work.json"),
                """
                {
                  "Cookie": "session=abc",
                  "Authorization": "Bearer token",
                  "Host": "evil.example",
                  "X-Custom": "1"
                }
                """);

            var resolved = SessionProfileHeaders.Resolve("work");
            assert("session profile parse ok", resolved.Status == SessionProfileStatus.Ok);
            assert("session profile cookie", resolved.Headers["Cookie"] == "session=abc");
            assert("session profile auth", resolved.Headers["Authorization"] == "Bearer token");
            assert("session profile custom", resolved.Headers["X-Custom"] == "1");
            assert("session profile blocks host", !resolved.Headers.ContainsKey("Host"));

            File.WriteAllText(Path.Combine(root, "bad.json"), "[1,2,3]");
            assert(
                "session profile non-object empty",
                SessionProfileHeaders.Resolve("bad").Headers.Count == 0);

            File.WriteAllText(Path.Combine(root, "broken.json"), "{not json");
            assert(
                "session profile invalid json not found",
                SessionProfileHeaders.Resolve("broken").Status == SessionProfileStatus.NotFound);

            Directory.CreateDirectory(Path.Combine(root, "states"));
            var statePath = Path.Combine(root, "states", "site.json");
            File.WriteAllText(statePath, """{"cookies":[]}""");
            File.WriteAllText(
                Path.Combine(root, "with-state.json"),
                $$"""
                {
                  "Cookie": "a=b",
                  "storageState": "states/site.json"
                }
                """);
            var withState = SessionProfileHeaders.Resolve("with-state");
            assert("session storage state resolves", withState.Status == SessionProfileStatus.Ok);
            assert("session storage state path", withState.StorageStatePath == Path.GetFullPath(statePath));

            File.WriteAllText(
                Path.Combine(root, "bad-state.json"),
                """
                {
                  "storageState": "states/missing.json"
                }
                """);
            assert(
                "session missing storage state not found",
                SessionProfileHeaders.Resolve("bad-state").Status == SessionProfileStatus.NotFound);
        }
        finally
        {
            SessionProfileHeaders.SessionsRootOverrideForTests = previousRoot;
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    private static void RunHeaderMerge(Action<string, bool> assert)
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Env"] = "1",
            ["Cookie"] = "env=old",
        };
        var session = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cookie"] = "session=new",
            ["Authorization"] = "Bearer x",
        };
        var merged = RequestHeadersMerger.Merge(env, session);
        assert("session merge precedence cookie", merged["Cookie"] == "session=new");
        assert("session merge keeps env", merged["X-Env"] == "1");
        assert("session merge adds auth", merged["Authorization"] == "Bearer x");
    }

    private static void RunFetchHeadersScopeCleanup(Action<string, bool> assert)
    {
        string? warning = null;
        FetchHeadersScope.CleanupFailureSinkForTests = msg => warning = msg;
        var scope = FetchHeadersScope.Create(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = "Bearer hidden",
                ["X-Test"] = "1",
            });
        var tempPath = FetchHeadersScope.ActivePath;
        assert("session headers temp path exists", !string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath));
        if (string.IsNullOrWhiteSpace(tempPath))
        {
            return;
        }

        using (var lockStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            scope.Dispose();
        }

        for (var i = 0; i < 25 && File.Exists(tempPath); i++)
        {
            Thread.Sleep(100);
        }

        // Best-effort cleanup is platform-dependent: Windows cannot delete a file held open
        // (emits the redacted warning), while POSIX unlinks it immediately (no warning). Assert
        // the contract, not one OS's failure mode: either it warned, or the file is gone.
        var warned = warning?.Contains("failed to delete temp headers file", StringComparison.OrdinalIgnoreCase) == true;
        assert(
            "session headers cleanup handled (warned or deleted)",
            warned || !File.Exists(tempPath));
        assert(
            "session headers cleanup warning redacts secret values",
            warning?.Contains("Bearer hidden", StringComparison.Ordinal) != true);
        assert("session headers temp file eventually deleted", !File.Exists(tempPath));
        FetchHeadersScope.CleanupFailureSinkForTests = null;
    }

    private static void RunPrivacyClassifier(Action<string, bool> assert)
    {
        assert("privacy link-local", PrivacyClassifier.Classify("http://169.254.169.254/").IsPrivateHost);
        assert("privacy localhost", PrivacyClassifier.Classify("http://localhost/").IsPrivateHost);
        assert("privacy corp local", PrivacyClassifier.Classify("http://intranet.corp.local/").IsPrivateHost);
        assert("privacy public nginx", !PrivacyClassifier.Classify("https://nginx.org/en/docs/").IsPrivateHost);
    }
}
