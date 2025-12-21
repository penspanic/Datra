using System;

namespace Datra.Editor.Utilities
{
    /// <summary>
    /// Utility class for path operations
    /// </summary>
    public static class PathHelper
    {
        /// <summary>
        /// Checks if a path is absolute (starts with / on Unix or drive letter on Windows)
        /// </summary>
        public static bool IsAbsolutePath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Unix absolute path
            if (path.StartsWith("/"))
                return true;

            // Windows absolute path (e.g., C:\, D:\)
            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
                return true;

            return false;
        }

        /// <summary>
        /// Combines a base path with a relative path using forward slashes.
        /// If the path is already absolute, returns it as-is.
        /// </summary>
        public static string CombinePath(string? basePath, string? path)
        {
            if (string.IsNullOrEmpty(basePath))
                return path ?? string.Empty;

            if (string.IsNullOrEmpty(path))
                return basePath;

            // Don't combine if path is already absolute
            if (IsAbsolutePath(path))
                return path;

            // Remove trailing slash from basePath if present
            if (basePath.EndsWith("/") || basePath.EndsWith("\\"))
                basePath = basePath.Substring(0, basePath.Length - 1);

            // Remove leading slash from path if present (for relative paths that accidentally have /)
            while (path.StartsWith("/") || path.StartsWith("\\"))
                path = path.Substring(1);

            // Combine with forward slash
            return basePath + "/" + path;
        }
    }
}
