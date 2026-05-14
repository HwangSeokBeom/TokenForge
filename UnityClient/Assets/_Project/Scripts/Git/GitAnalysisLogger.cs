using UnityEngine;

namespace TokenForge.Client.Git
{
    public interface IGitAnalysisLogger
    {
        void Info(string message);
        void Warning(string message);
    }

    public sealed class UnityGitAnalysisLogger : IGitAnalysisLogger
    {
        public void Info(string message)
        {
            Debug.Log(message);
        }

        public void Warning(string message)
        {
            Debug.LogWarning(message);
        }
    }
}
