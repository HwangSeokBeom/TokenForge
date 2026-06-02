using NUnit.Framework;
using UnityEngine;

namespace TokenForge.Client.Tests
{
    [SetUpFixture]
    public sealed class EditModeLogStackTraceSetup
    {
        private StackTraceLogType originalLogStackTrace;

        [OneTimeSetUp]
        public void DisableInfoLogStackTraces()
        {
            originalLogStackTrace = Application.GetStackTraceLogType(LogType.Log);
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        }

        [OneTimeTearDown]
        public void RestoreInfoLogStackTraces()
        {
            Application.SetStackTraceLogType(LogType.Log, originalLogStackTrace);
        }
    }
}
