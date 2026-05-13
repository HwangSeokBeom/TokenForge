using System;
using System.Collections.Generic;
using System.Linq;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Git
{
    public static class WorkTypeInferenceService
    {
        public static WorkType Infer(IReadOnlyDictionary<FileCategory, int> categoryCounts, int changedFileCount)
        {
            if (changedFileCount <= 0 || categoryCounts.Count == 0)
            {
                return WorkType.Unknown;
            }

            var total = Math.Max(1, changedFileCount);
            var testRatio = Ratio(categoryCounts, FileCategory.Test, total);
            var docsRatio = Ratio(categoryCounts, FileCategory.Docs, total);
            var uiRatio = Ratio(categoryCounts, FileCategory.UI, total);
            var architectureRatio = Ratio(categoryCounts, FileCategory.Architecture, total);
            var configRatio = Ratio(categoryCounts, FileCategory.Config, total);
            var domainRatio = Ratio(categoryCounts, FileCategory.Domain, total);

            if (testRatio >= 0.45f) return WorkType.Test;
            if (docsRatio >= 0.45f) return WorkType.Docs;
            if (uiRatio >= 0.35f) return WorkType.UIUX;
            if (architectureRatio >= 0.35f) return WorkType.Refactor;
            if (configRatio >= 0.5f) return WorkType.Build;
            if (domainRatio >= 0.45f) return WorkType.Feature;

            var nonUnknownCategories = categoryCounts.Count(kvp => kvp.Key != FileCategory.Unknown && kvp.Value > 0);
            return nonUnknownCategories > 1 ? WorkType.Mixed : WorkType.Unknown;
        }

        public static ProviderConfidence ConfidenceFor(WorkType workType, IReadOnlyDictionary<FileCategory, int> categoryCounts, int changedFileCount)
        {
            if (workType == WorkType.Unknown || changedFileCount <= 0)
            {
                return ProviderConfidence.Low;
            }

            var maxCategoryCount = categoryCounts.Count == 0 ? 0 : categoryCounts.Max(kvp => kvp.Value);
            var ratio = (float)maxCategoryCount / Math.Max(1, changedFileCount);
            if (ratio >= 0.6f) return ProviderConfidence.High;
            if (ratio >= 0.35f) return ProviderConfidence.Medium;
            return ProviderConfidence.Low;
        }

        private static float Ratio(IReadOnlyDictionary<FileCategory, int> counts, FileCategory category, int total)
        {
            return counts.TryGetValue(category, out var count) ? (float)count / total : 0f;
        }
    }
}
