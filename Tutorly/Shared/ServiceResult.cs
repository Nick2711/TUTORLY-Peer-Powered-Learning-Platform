namespace Tutorly.Shared
{
    /// <summary>
    /// Standard API response wrapper for all service operations
    /// </summary>
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();

        public ServiceResult()
        {
        }

        public ServiceResult(bool success, T? data = default, string? message = null)
        {
            Success = success;
            Data = data;
            Message = message;
        }

        public static ServiceResult<T> SuccessResult(T data, string? message = null)
        {
            return new ServiceResult<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        public static ServiceResult<T> FailureResult(string message, List<string>? errors = null, string? errorCode = null)
        {
            return new ServiceResult<T>
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Errors = errors ?? new List<string>()
            };
        }
    }

    /// <summary>
    /// Non-generic version for operations that don't return data
    /// </summary>
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
        public List<string> Errors { get; set; } = new();

        public ServiceResult()
        {
        }

        public ServiceResult(bool success, string? message = null)
        {
            Success = success;
            Message = message;
        }

        public static ServiceResult SuccessResult(string? message = null)
        {
            return new ServiceResult
            {
                Success = true,
                Message = message
            };
        }

        public static ServiceResult FailureResult(string message, List<string>? errors = null, string? errorCode = null)
        {
            return new ServiceResult
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Errors = errors ?? new List<string>()
            };
        }
    }
}

