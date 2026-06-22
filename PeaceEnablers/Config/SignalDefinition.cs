namespace PeaceEnablers.Config
{
    public enum SignalMode
    {
        StressTest,
        EarlyWarning,
        Resilience
    }

    public sealed class SignalDefinition
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public SignalMode Mode { get; init; }
        public bool HigherIsRisk { get; init; } = true;

        public static readonly IReadOnlyList<SignalDefinition> All = new List<SignalDefinition>
        {
            new() { Code = "PEM", Name = "Peace Equilibrium Metric", Mode = SignalMode.StressTest, HigherIsRisk = false },
            new() { Code = "SFS", Name = "Systemic Fragility Stress", Mode = SignalMode.StressTest },
            new() { Code = "GAS", Name = "Governance Asymmetry Stress", Mode = SignalMode.StressTest },
            new() { Code = "SCS", Name = "Social Cohesion Strength", Mode = SignalMode.Resilience, HigherIsRisk = false },
            new() { Code = "NCS", Name = "Narrative Contention Stress", Mode = SignalMode.StressTest },
            new() { Code = "IIS", Name = "Institutional Integrity Stress", Mode = SignalMode.StressTest },
            new() { Code = "SCSS", Name = "Security-Civic Strain Signal", Mode = SignalMode.StressTest },
            new() { Code = "TIS", Name = "Trust Instability Signal", Mode = SignalMode.StressTest },
            new() { Code = "MAS", Name = "Market Access Stress", Mode = SignalMode.StressTest },
            new() { Code = "IAS", Name = "Inclusion Asymmetry Stress", Mode = SignalMode.StressTest },
            new() { Code = "PEM-DM", Name = "PEM Directional Movement", Mode = SignalMode.StressTest },
            new() { Code = "NSS", Name = "Neighborhood Spillover Stress", Mode = SignalMode.StressTest },
            new() { Code = "EWES", Name = "Early Warning Escalation Score", Mode = SignalMode.EarlyWarning },
            new() { Code = "VCS", Name = "Volatility Change Signal", Mode = SignalMode.EarlyWarning },
            new() { Code = "CRS", Name = "Conflict Risk Shift", Mode = SignalMode.EarlyWarning },
            new() { Code = "MPS", Name = "Macro Pressure Signal", Mode = SignalMode.EarlyWarning },
            new() { Code = "LRS", Name = "Legitimacy Risk Signal", Mode = SignalMode.EarlyWarning },
            new() { Code = "YFS", Name = "Youth Friction Signal", Mode = SignalMode.EarlyWarning },
            new() { Code = "HVS", Name = "Humanitarian Vulnerability Signal", Mode = SignalMode.EarlyWarning },
            new() { Code = "USS", Name = "Urban Security Signal", Mode = SignalMode.EarlyWarning },
            new() { Code = "ESS", Name = "Essential Services Signal", Mode = SignalMode.EarlyWarning },
            new() { Code = "SAS", Name = "State Adaptability Score", Mode = SignalMode.Resilience, HigherIsRisk = false },
            new() { Code = "MCS", Name = "Mediation Capacity Score", Mode = SignalMode.Resilience, HigherIsRisk = false },
            new() { Code = "EDS", Name = "Economic Diversification Score", Mode = SignalMode.Resilience, HigherIsRisk = false },
            new() { Code = "RSS", Name = "Recovery Speed Score", Mode = SignalMode.Resilience, HigherIsRisk = false },
            new() { Code = "ICS", Name = "Institutional Continuity Score", Mode = SignalMode.Resilience, HigherIsRisk = false },
            new() { Code = "FSS", Name = "Fiscal Stability Score", Mode = SignalMode.Resilience, HigherIsRisk = false },
            new() { Code = "SJS", Name = "Social Justice Score", Mode = SignalMode.Resilience, HigherIsRisk = false }
        };

        public static readonly IReadOnlyDictionary<string, SignalDefinition> ByCode = All
            .ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);
    }
}
