using System;
using System.Collections.Generic;

namespace CSimple.Models
{
    /// <summary>
    /// Result of memory compression operation
    /// </summary>
    public class CompressionResult
    {
        public int TokensReduced { get; set; }
        public float EfficiencyGain { get; set; }
        public bool CompressionSuccessful { get; set; }
        public List<string> RulesApplied { get; set; } = new List<string>();
    }

    /// <summary>
    /// Configuration profile for memory compression
    /// </summary>
    public class MemoryPersonalityProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public List<CompressionRule> CompressionRules { get; set; } = new List<CompressionRule>();
        public PreservationSettings PreservationSettings { get; set; } = new PreservationSettings();
    }

    /// <summary>
    /// Rule for memory compression
    /// </summary>
    public class CompressionRule
    {
        public string Type { get; set; } = string.Empty;
        public int Priority { get; set; }
        public float Threshold { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Settings for preserving important data during compression
    /// </summary>
    public class PreservationSettings
    {
        public bool PreserveCriticalNodes { get; set; } = true;
        public bool PreserveUserData { get; set; } = true;
        public bool PreserveClassifications { get; set; } = true;
        public float MinimumEfficiencyThreshold { get; set; } = 0.05f;
    }

    /// <summary>
    /// Analysis of pipeline memory usage
    /// </summary>
    public class PipelineMemoryAnalysis
    {
        public int TotalNodes { get; set; }
        public int TotalConnections { get; set; }
        public int TotalTokens { get; set; }
        public int RedundantConnections { get; set; }
        public float MemoryEfficiency { get; set; }
    }
}
