using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Platform
{
    public sealed class ProjectConnectionService
    {
        public ConnectedProject CreateConnectedProject(string projectPath, string localAlias, bool isGitRepository)
        {
            return new ConnectedProject
            {
                ProjectAlias = localAlias ?? string.Empty,
                ProjectPathHash = SafeHashUtility.ComputeProjectPathHash(projectPath),
                IsGitRepository = isGitRepository,
                AnalysisEnabled = true
            };
        }
    }
}
