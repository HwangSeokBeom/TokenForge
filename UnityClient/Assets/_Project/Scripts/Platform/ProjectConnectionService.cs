using TokenForge.Client.Domain;

namespace TokenForge.Client.Platform
{
    public sealed class ProjectConnectionService
    {
        public ConnectedProject CreateConnectedProject(string projectPath, string localAlias, bool isGitRepository)
        {
            var canonicalPath = RepositoryCompanionProfileService.CanonicalRepositoryPathForIdentity(projectPath);
            var repositoryId = RepositoryCompanionProfileService.HashRepositoryPath(canonicalPath);
            UnityEngine.Debug.Log("INFO [RepositoryIdentity][CANONICALIZE] rawPath=" + (projectPath ?? string.Empty) + " canonicalPath=" + canonicalPath + " repositoryId=" + repositoryId);
            return new ConnectedProject
            {
                Id = repositoryId,
                DisplayName = localAlias ?? string.Empty,
                ApprovedAt = System.DateTimeOffset.UtcNow,
                ConnectionSource = "userSelected",
                ProjectAlias = localAlias ?? string.Empty,
                ProjectPathHash = repositoryId,
                PathHash = repositoryId,
                IsGitRepository = isGitRepository,
                IsActive = true,
                IsArchived = false,
                AnalysisEnabled = true
            };
        }
    }
}
