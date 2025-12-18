#nullable enable
using System;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;

namespace Datra.Editor.Services
{
    /// <summary>
    /// No-operation file lock service for single-user scenarios.
    /// All lock operations succeed without actually locking.
    /// </summary>
    public class NoOpFileLockService : IFileLockService
    {
        public event Action<string, LockInfo?>? OnLockChanged;

        public Task<LockResult> AcquireLockAsync(string path, string userId, TimeSpan? duration = null)
        {
            var lockInfo = new LockInfo(
                path: path,
                userId: userId,
                userName: userId,
                acquiredAt: DateTime.UtcNow,
                expiresAt: DateTime.UtcNow.Add(duration ?? TimeSpan.FromHours(1))
            );

            return Task.FromResult(LockResult.Succeeded(lockInfo));
        }

        public Task<bool> ReleaseLockAsync(string path, string userId)
        {
            OnLockChanged?.Invoke(path, null);
            return Task.FromResult(true);
        }

        public Task<LockInfo?> GetLockInfoAsync(string path)
        {
            return Task.FromResult<LockInfo?>(null);
        }

        public Task<bool> IsLockedAsync(string path)
        {
            return Task.FromResult(false);
        }

        public Task<bool> IsLockedByOtherAsync(string path, string currentUserId)
        {
            return Task.FromResult(false);
        }

        public Task<LockResult> RefreshLockAsync(string path, string userId, TimeSpan? duration = null)
        {
            return AcquireLockAsync(path, userId, duration);
        }
    }
}
