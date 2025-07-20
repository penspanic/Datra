using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Datra.Generators
{
    internal static class GeneratorLogger
    {
        private static readonly List<string> _logs = new List<string>();
        private static readonly Stopwatch _stopwatch = new Stopwatch();

        public static void StartLogging()
        {
            _logs.Clear();
            _stopwatch.Restart();
        }

        public static void Log(string message)
        {
            var logEntry = $"[{_stopwatch.ElapsedMilliseconds}ms] {message}";
            _logs.Add(logEntry);
            Debug.WriteLine($"[SourceGenerator] {logEntry}");
        }

        public static void LogWarning(string message)
        {
            Log($"WARNING: {message}");
        }

        public static void LogError(string message, Exception ex = null)
        {
            var errorMessage = ex != null ? $"ERROR: {message} - {ex.Message}" : $"ERROR: {message}";
            Log(errorMessage);
        }

        public static void AddDebugOutput(GeneratorExecutionContext context)
        {
            if (_logs.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("// Source Generator Debug Log");
                sb.AppendLine($"// Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"// Total execution time: {_stopwatch.ElapsedMilliseconds}ms");
                sb.AppendLine("//");
                
                foreach (var log in _logs)
                {
                    sb.AppendLine($"// {log}");
                }

                context.AddSource("GeneratorLog.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        }
    }
}