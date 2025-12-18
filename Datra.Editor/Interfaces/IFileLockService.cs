#nullable enable
using System;
using System.Threading.Tasks;

namespace Datra.Editor.Interfaces
{
    /// <summary>
    /// Service for managing file locks in multi-user editing scenarios.
    /// Implementations may use database, file system, or server-based locking.
    /// </summary>
    public interface IFileLockService
    {
        /// <summary>
        /// Acquire a lock on a file
        /// </summary>
        /// <param name="path">Path to lock</param>
        /// <param name="userId">User acquiring the lock</param>
        /// <param name="duration">Lock duration (null for default)</param>
        /// <returns>Lock result with success status and lock info</returns>
        Task<LockResult> AcquireLockAsync(string path, string userId, TimeSpan? duration = null);

        /// <summary>
        /// Release a lock on a file
        /// </summary>
        /// <param name="path">Path to unlock</param>
        /// <param name="userId">User releasing the lock</param>
        /// <returns>True if released successfully</returns>
        Task<bool> ReleaseLockAsync(string path, string userId);

        /// <summary>
        /// Get lock information for a file
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>Lock info or null if not locked</returns>
        Task<LockInfo?> GetLockInfoAsync(string path);

        /// <summary>
        /// Check if a file is locked
        /// </summary>
        Task<bool> IsLockedAsync(string path);

        /// <summary>
        /// Check if a file is locked by another user
        /// </summary>
        Task<bool> IsLockedByOtherAsync(string path, string currentUserId);

        /// <summary>
        /// Refresh/extend an existing lock
        /// </summary>
        Task<LockResult> RefreshLockAsync(string path, string userId, TimeSpan? duration = null);

        /// <summary>
        /// Raised when a lock state changes
        /// </summary>
        event Action<string, LockInfo?>? OnLockChanged;
    }

    /// <summary>
    /// Information about a file lock
    /// </summary>
    public class LockInfo
    {
        public string Path { get; }
        public string UserId { get; }
        public string? UserName { get; }
        public DateTime AcquiredAt { get; }
        public DateTime ExpiresAt { get; }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        public LockInfo(string path, string userId, string? userName, DateTime acquiredAt, DateTime expiresAt)
        {
            Path = path;
            UserId = userId;
            UserName = userName;
            AcquiredAt = acquiredAt;
            ExpiresAt = expiresAt;
        }
    }

    /// <summary>
    /// Result of a lock operation
    /// </summary>
    public class LockResult
    {
        public bool Success { get; }
        public LockInfo? Lock { get; }
        public string? ErrorMessage { get; }

        public LockResult(bool success, LockInfo? lockInfo = null, string? errorMessage = null)
        {
            Success = success;
            Lock = lockInfo;
            ErrorMessage = errorMessage;
        }

        public static LockResult Succeeded(LockInfo lockInfo) => new(true, lockInfo);
        public static LockResult Failed(string errorMessage) => new(false, null, errorMessage);
    }
}
