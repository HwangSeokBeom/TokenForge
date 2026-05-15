using System;
using System.Collections.Generic;

namespace TokenForge.Client.Sync
{
    [Serializable]
    public sealed class SafeSessionDiffSummary
    {
        public bool IsDifferent { get; set; }
        public List<string> ChangedSafeFieldNames { get; set; } = new List<string>();
    }
}
