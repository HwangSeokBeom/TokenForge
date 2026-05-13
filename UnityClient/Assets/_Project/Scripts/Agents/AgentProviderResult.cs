using System;
using System.Collections.Generic;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Agents
{
    [Serializable]
    public sealed class AgentProviderWarning
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class AgentProviderError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRecoverable { get; set; } = true;
    }

    [Serializable]
    public sealed class AgentProviderResult
    {
        public bool IsAvailable { get; set; }
        public bool IsSuccess { get; set; }
        public bool IsPartialSuccess { get; set; }
        public string ProviderId { get; set; } = string.Empty;
        public AgentWorkSession Session { get; set; }
        public ProviderConfidence Confidence { get; set; } = ProviderConfidence.Unknown;
        public List<AgentProviderWarning> Warnings { get; set; } = new List<AgentProviderWarning>();
        public AgentProviderError Error { get; set; }

        public static AgentProviderResult Success(string providerId, AgentWorkSession session, ProviderConfidence confidence)
        {
            return new AgentProviderResult
            {
                IsAvailable = true,
                IsSuccess = true,
                ProviderId = providerId,
                Session = session,
                Confidence = confidence
            };
        }

        public static AgentProviderResult Partial(string providerId, AgentWorkSession session, ProviderConfidence confidence, string warningCode, string warningMessage)
        {
            var result = Success(providerId, session, confidence);
            result.IsSuccess = false;
            result.IsPartialSuccess = true;
            result.Warnings.Add(new AgentProviderWarning { Code = warningCode, Message = warningMessage });
            return result;
        }

        public static AgentProviderResult Failure(string providerId, string errorCode, string errorMessage, bool isRecoverable = true)
        {
            return new AgentProviderResult
            {
                IsAvailable = false,
                IsSuccess = false,
                ProviderId = providerId,
                Error = new AgentProviderError
                {
                    Code = errorCode,
                    Message = errorMessage,
                    IsRecoverable = isRecoverable
                }
            };
        }
    }
}
