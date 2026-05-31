using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TokenForge.Client.Tests
{
    public sealed class NativeDashboardBridgeDiscoverySmokeTests
    {
        [Test]
        public void NUnitSmoke()
        {
            Debug.Log("PHASE separate native dashboard NUnit smoke");
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator UnityTestSmoke()
        {
            Debug.Log("PHASE separate native dashboard UnityTest smoke");
            yield return null;
            Assert.Pass();
        }
    }
}
