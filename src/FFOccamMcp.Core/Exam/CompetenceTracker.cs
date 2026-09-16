namespace OccamMcp.Core.Exam;

/// <summary>
/// One observed tool call, reduced to the signals that say something about the caller.
/// </summary>
/// <remarks>
/// <paramref name="CallerError"/> is the load-bearing field and the easy one to get wrong. A page
/// returning <c>http_404</c> is not the agent's fault and must not count against it; only failures
/// attributable to the call itself do — <c>invalid_arguments</c>, <c>invalid_policy</c>, a malformed
/// argument object. Conflating the two would score the *web* and call it agent competence.
/// </remarks>
/// <param name="ToolName">Tool that was called.</param>
/// <param name="ArgumentsValid">Whether the arguments bound to the tool schema.</param>
/// <param name="CallerError">Whether the call failed for a reason attributable to the caller.</param>
public readonly record struct CallObservation(string ToolName, bool ArgumentsValid, bool CallerError);

/// <summary>
/// A tier derived from behaviour, with the evidence that produced it.
/// </summary>
/// <param name="Tier">Currently applied tier.</param>
/// <param name="Score">Rolling competence in [0,1], or <c>null</c> below the minimum sample.</param>
/// <param name="Observations">Observations in the current window.</param>
/// <param name="SuggestedTier">What the score alone would suggest, before hysteresis.</param>
/// <param name="Oscillations">
/// How many times the applied tier has actually changed. A primary outcome, not a diagnostic: a
/// surface that flips under an agent mid-session is plausibly worse than a stable wrong one
/// (docs/research/hypothesis.md, H3 secondary question).
/// </param>
public readonly record struct CompetenceAssessment(
    AgentTier Tier,
    double? Score,
    int Observations,
    AgentTier SuggestedTier,
    int Oscillations);

/// <summary>
/// Keeps a rolling competence estimate per subject and adjusts the tier as behaviour accumulates.
/// </summary>
/// <remarks>
/// <para>
/// This is the mechanism H3 proposes: that behaviour observed on real calls predicts task success
/// better than a client's self-report or a static model-name lookup. H3 is also the hypothesis most
/// likely to be refuted, and the useful outcome of refuting it is deleting this class rather than
/// tuning it.
/// </para>
/// <para>
/// Two design choices exist to stop the tier thrashing:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>A minimum sample.</b> Below <see cref="MinObservations"/> the tracker reports no score and
/// leaves the tier alone. Judging a client on two calls produces a number that looks like evidence
/// and is not.
/// </description></item>
/// <item><description>
/// <b>Hysteresis.</b> A suggested change must persist for <see cref="StabilityRequirement"/>
/// consecutive observations before it is applied. Whether promotion and demotion should have
/// *different* stability requirements — narrowing a surface is the safe direction, widening it is
/// the risky one — is an open question, deliberately left symmetric until there is data.
/// </description></item>
/// </list>
/// </remarks>
public sealed class CompetenceTracker
{
    /// <summary>Observations retained per subject.</summary>
    public const int DefaultWindow = 20;

    /// <summary>Observations required before a score is reported at all.</summary>
    public const int MinObservations = 5;

    /// <summary>Consecutive agreeing observations required before a tier change is applied.</summary>
    public const int StabilityRequirement = 3;

    /// <summary>Rolling score at or above which <see cref="AgentTier.Strong"/> is suggested.</summary>
    public const double PromoteThreshold = 0.9;

    /// <summary>Rolling score at or below which <see cref="AgentTier.Weak"/> is suggested.</summary>
    public const double DemoteThreshold = 0.5;

    private readonly object _gate = new();
    private readonly Dictionary<ExamSubject, State> _states = [];
    private readonly int _window;
    private readonly int _maxSubjects;

    /// <summary>Creates a tracker.</summary>
    /// <param name="window">Observations retained per subject; must be at least <see cref="MinObservations"/>.</param>
    /// <param name="maxSubjects">Cap on tracked subjects, since the key is caller-influenced.</param>
    public CompetenceTracker(int window = DefaultWindow, int maxSubjects = 1_024)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(window, MinObservations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSubjects);
        _window = window;
        _maxSubjects = maxSubjects;
    }

    /// <summary>Number of subjects currently tracked.</summary>
    public int TrackedSubjects
    {
        get
        {
            lock (_gate)
            {
                return _states.Count;
            }
        }
    }

    /// <summary>
    /// Records one call and returns the assessment that now applies.
    /// </summary>
    /// <param name="subject">Who made the call.</param>
    /// <param name="baselineTier">Tier the exam (or the default) established; the starting point.</param>
    /// <param name="observation">What was observed.</param>
    /// <returns>The assessment after folding in this observation.</returns>
    public CompetenceAssessment Record(ExamSubject subject, AgentTier baselineTier, CallObservation observation)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(subject, out var state))
            {
                if (_states.Count >= _maxSubjects)
                {
                    EvictOneLocked();
                }

                state = new State(baselineTier);
                _states[subject] = state;
            }

            state.Add(observation, _window);
            return state.Assess(_window);
        }
    }

    /// <summary>Returns the current assessment without recording anything.</summary>
    /// <param name="subject">Who to assess.</param>
    /// <param name="baselineTier">Tier to report when this subject has no observations yet.</param>
    /// <returns>The current assessment.</returns>
    public CompetenceAssessment Assess(ExamSubject subject, AgentTier baselineTier)
    {
        lock (_gate)
        {
            return _states.TryGetValue(subject, out var state)
                ? state.Assess(_window)
                : new CompetenceAssessment(baselineTier, Score: null, Observations: 0, baselineTier, Oscillations: 0);
        }
    }

    /// <summary>Forgets a subject's history, for example when its exam result is refreshed.</summary>
    /// <param name="subject">Who to forget.</param>
    /// <returns><c>true</c> when there was history to drop.</returns>
    public bool Forget(ExamSubject subject)
    {
        lock (_gate)
        {
            return _states.Remove(subject);
        }
    }

    /// <summary>Maps a rolling score to the tier it suggests, ignoring hysteresis.</summary>
    /// <param name="score">Rolling competence in [0,1].</param>
    /// <returns>The suggested tier.</returns>
    public static AgentTier SuggestTier(double score) =>
        score >= PromoteThreshold ? AgentTier.Strong
        : score <= DemoteThreshold ? AgentTier.Weak
        : AgentTier.Medium;

    private void EvictOneLocked()
    {
        // Arbitrary eviction is acceptable here: losing rolling history degrades the estimate back
        // to the exam baseline, which is a safe state, unlike losing a cached exam result.
        foreach (var key in _states.Keys)
        {
            _states.Remove(key);
            return;
        }
    }

    private sealed class State(AgentTier baselineTier)
    {
        private readonly Queue<CallObservation> _observations = new();
        private AgentTier _appliedTier = baselineTier;
        private AgentTier _pendingTier = baselineTier;
        private int _pendingStreak;

        public int Oscillations { get; private set; }

        public void Add(CallObservation observation, int window)
        {
            _observations.Enqueue(observation);
            while (_observations.Count > window)
            {
                _observations.Dequeue();
            }

            var score = ComputeScore();
            if (score is null)
            {
                return;
            }

            var suggested = SuggestTier(score.Value);
            if (suggested == _pendingTier)
            {
                _pendingStreak++;
            }
            else
            {
                _pendingTier = suggested;
                _pendingStreak = 1;
            }

            if (suggested != _appliedTier && _pendingStreak >= StabilityRequirement)
            {
                _appliedTier = suggested;
                Oscillations++;
                _pendingStreak = 0;
            }
        }

        public CompetenceAssessment Assess(int window)
        {
            var score = ComputeScore();
            var suggested = score is null ? _appliedTier : SuggestTier(score.Value);
            return new CompetenceAssessment(
                _appliedTier,
                score,
                Math.Min(_observations.Count, window),
                suggested,
                Oscillations);
        }

        private double? ComputeScore()
        {
            if (_observations.Count < MinObservations)
            {
                return null;
            }

            var competent = 0;
            foreach (var observation in _observations)
            {
                if (observation.ArgumentsValid && !observation.CallerError)
                {
                    competent++;
                }
            }

            return (double)competent / _observations.Count;
        }
    }
}
