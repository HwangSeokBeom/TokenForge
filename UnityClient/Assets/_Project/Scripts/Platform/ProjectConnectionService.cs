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
                Id = System.Guid.NewGuid().ToString("N"),
                DisplayName = localAlias ?? string.Empty,
                ApprovedAt = System.DateTimeOffset.UtcNow,
                ConnectionSource = "userSelected",
                ProjectAlias = localAlias ?? string.Empty,
                ProjectPathHash = SafeHashUtility.ComputeProjectPathHash(projectPath),
                PathHash = SafeHashUtility.ComputeProjectPathHash(projectPath),
                IsGitRepository = isGitRepository,
                IsActive = true,
                IsArchived = false,
                AnalysisEnabled = true
            };
        }
    }
}
