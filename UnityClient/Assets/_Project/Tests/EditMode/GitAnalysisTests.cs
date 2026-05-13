using System.Collections.Generic;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;

namespace TokenForge.Client.Tests
{
    public sealed class GitAnalysisTests
    {
        [Test]
        public void GitNumstatParser_ParsesBucketsAndCategories()
        {
            var entries = GitNumstatParser.Parse("10\t2\tAssets/_Project/Scripts/UI/FooView.cs\n-\t-\tAssets/_Project/Art/icon.png\n");

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(10, entries[0].AddedLines);
            Assert.AreEqual(FileCategory.UI, entries[0].Category);
            Assert.IsTrue(entries[1].IsBinary);
            Assert.AreEqual(LineChangeBucket.Small, GitNumstatParser.ToBucket(12));
        }

        [Test]
        public void WorkTypeInference_TestHeavyChanges_ReturnsTest()
        {
            var counts = new Dictionary<FileCategory, int>
            {
                [FileCategory.Test] = 4,
                [FileCategory.Domain] = 1
            };

            var workType = WorkTypeInferenceService.Infer(counts, 5);

            Assert.AreEqual(WorkType.Test, workType);
        }
    }
}
