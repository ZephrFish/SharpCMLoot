using System;
using System.Threading;

namespace SCML.Services
{
    /// <summary>
    /// Service for handling network retry logic with exponential backoff
    /// </summary>
    public static class NetworkRetryService
    {
        // Configuration for retry logic
        private const int MaxRetries = 2;  // Limited to prevent account lockout
        private const int BaseDelayMs = 2000;  // Base delay between retry attempts
        private const int MaxDelayMs = 10000;  // Maximum delay between retries

        public static T ExecuteWithRetry<T>(Func<T> operation, string operationName, bool verbose = false)
        {
            Exception lastException = null;
            
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    if (attempt > 1 && verbose)
                    {
                        Console.WriteLine(string.Format("[*] Retry attempt {0}/{1} for {2}", 
                            attempt, MaxRetries, operationName));
                    }
                    
                    return operation();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt == MaxRetries)
                    {
                        if (verbose)
                        {
                            Console.WriteLine(string.Format("[-] Failed after {0} attempts: {1}", 
                                MaxRetries, operationName));
                        }
                        throw;
                    }
                    
                    // Check if the error is retryable
                    if (!IsRetryableError(ex))
                    {
                        throw;
                    }
                    
                    // Exponential backoff
                    int delayMs = BaseDelayMs * (int)Math.Pow(2, attempt - 1);
                    
                    if (verbose)
                    {
                        Console.WriteLine(string.Format("[!] {0} failed: {1}. Retrying in {2}ms...", 
                            operationName, ex.Message, delayMs));
                    }
                    
                    Thread.Sleep(delayMs);
                }
            }
            
            throw lastException ?? new Exception(string.Format("Failed to execute {0}", operationName));
        }

        public static void ExecuteWithRetry(Action operation, string operationName, bool verbose = false)
        {
            ExecuteWithRetry<object>(() =>
            {
                operation();
                return null;
            }, operationName, verbose);
        }

        private static bool IsRetryableError(Exception ex)
        {
            // IMPORTANT: Do NOT retry authentication failures to prevent account lockout
            var doNotRetryMessages = new[]
            {
                "login failed",
                "authentication",
                "STATUS_LOGON_FAILURE",
                "STATUS_ACCOUNT_LOCKED_OUT",
                "STATUS_ACCOUNT_DISABLED",
                "STATUS_PASSWORD_EXPIRED",
                "STATUS_WRONG_PASSWORD",
                "bad username or password",
                "invalid credentials"
            };
            
            var errorMessage = ex.Message.ToLower();
            
            // Check if this is an authentication error - DO NOT RETRY
            foreach (var doNotRetry in doNotRetryMessages)
            {
                if (errorMessage.Contains(doNotRetry.ToLower()))
                {
                    return false;  // Never retry authentication failures
                }
            }
            
            // Network-related errors that are worth retrying
            var retryableMessages = new[]
            {
                "network",
                "timeout",
                "connection",
                "temporarily",
                "busy",
                "unavailable",
                "cannot connect",
                "remote"
            };
            
            foreach (var retryable in retryableMessages)
            {
                if (errorMessage.Contains(retryable))
                {
                    return true;
                }
            }

            // Check inner exceptions
            if (ex.InnerException != null)
            {
                return IsRetryableError(ex.InnerException);
            }

            return false;
        }
    }
}