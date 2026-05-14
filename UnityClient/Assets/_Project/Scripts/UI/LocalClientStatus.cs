using System;
using System.Collections.Generic;

namespace TokenForge.Client.UI
{
    public sealed class LocalClientStatus
    {
        private readonly List<BootstrapSectionStatus> sections = new List<BootstrapSectionStatus>();
        private readonly List<string> debugLines = new List<string>();

        private LocalClientStatus()
        {
        }

        public string ClientStatus { get; private set; } = string.Empty;
        public DateTimeOffset InitializedAt { get; private set; }
        public IReadOnlyList<BootstrapSectionStatus> Sections => sections;
        public IReadOnlyList<string> DebugLines => debugLines;

        public static LocalClientStatus CreateInitialized()
        {
            var status = new LocalClientStatus
            {
                ClientStatus = "Local client initialized",
                InitializedAt = DateTimeOffset.Now
            };

            status.sections.Add(new BootstrapSectionStatus(
                "Connected Project",
                "Local placeholder",
                "No project connection selected yet."));
            status.sections.Add(new BootstrapSectionStatus(
                "Git Activity",
                "Waiting for local analysis",
                "Repository scan will run from local state in a later bootstrap step."));
            status.sections.Add(new BootstrapSectionStatus(
                "AI Agent Logs",
                "No agent sessions loaded",
                "Agent log providers are not connected during bootstrap."));
            status.sections.Add(new BootstrapSectionStatus(
                "Daily Progress",
                "Ready",
                "Daily growth summary will use local gameplay and analysis state."));

            status.debugLines.Add("Bootstrap source: local runtime");
            status.debugLines.Add("Backend sync: disabled");
            status.debugLines.Add("API contracts: untouched");

            return status;
        }
    }
}
