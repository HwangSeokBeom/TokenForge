using System;
using System.Collections.Generic;

namespace TokenForge.Client.Common
{
    [Serializable]
    public class Result
    {
        public bool IsSuccess { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();

        public static Result Success()
        {
            return new Result { IsSuccess = true };
        }

        public static Result Failure(string errorCode, string errorMessage)
        {
            return new Result
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }

    [Serializable]
    public sealed class Result<T> : Result
    {
        public T Value { get; set; }

        public static Result<T> Success(T value)
        {
            return new Result<T> { IsSuccess = true, Value = value };
        }

        public static new Result<T> Failure(string errorCode, string errorMessage)
        {
            return new Result<T>
            {
                IsSuccess = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }
}
