using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TokenForge.Client.Tests
{
    public sealed class ProviderNamingGuardrailTests
    {
        [Test]
        public void ProjectUiAndDomainDoNotContainDisallowedProviderName()
        {
            var root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "Assets", "_Project"));
            if (!Directory.Exists(root))
            {
                root = Path.GetFullPath("Assets/_Project");
            }

            var disallowedA = "Chat" + "GPT";
            var disallowedB = "chat" + "Gpt";
            var disallowedC = "Chat" + "Gpt";
            var matches = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs") || path.EndsWith(".prefab") || path.EndsWith(".unity"))
                .SelectMany(path => File.ReadAllLines(path).Select((line, index) => new { path, line, index }))
                .Where(item => item.line.Contains(disallowedA) || item.line.Contains(disallowedB) || item.line.Contains(disallowedC))
                .Select(item => item.path + ":" + (item.index + 1))
                .ToList();

            Assert.IsEmpty(matches);
        }
    }
}
