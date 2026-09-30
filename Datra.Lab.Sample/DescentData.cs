#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Datra.Lab.Sample.Generated;
using Datra.Lab.Sample.Models;

namespace Datra.Lab.Sample
{
    /// <summary>
    /// The toy game's data files (embedded in this assembly) and the few helpers that read
    /// the loaded tables the way a game's own code would.
    /// </summary>
    public static class DescentData
    {
        private const string Prefix = "Descent/";

        /// <summary>File name → YAML text, as shipped.</summary>
        public static IReadOnlyDictionary<string, string> Files()
        {
            var assembly = typeof(DescentData).GetTypeInfo().Assembly;
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)))
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                files[name.Substring(Prefix.Length)] = reader.ReadToEnd();
            }
            return files;
        }

        /// <summary>Write the data files into <paramref name="directory"/> so a file provider can serve them.</summary>
        public static string WriteTo(string directory)
        {
            Directory.CreateDirectory(directory);
            foreach (var file in Files())
                File.WriteAllText(Path.Combine(directory, file.Key), file.Value);
            return directory;
        }

        /// <summary>Floors top to bottom, in file order.</summary>
        public static IReadOnlyList<FloorData> FloorsInOrder(DescentContext data) =>
            data.Floor.LoadedItems.Values.ToList();

        /// <summary>The factor each floor's value multiplier grows by, on average.</summary>
        public static double ValueGrowth(DescentContext data)
        {
            var floors = FloorsInOrder(data);
            if (floors.Count < 2 || floors[0].ValueMultiplier <= 0) return 1;
            return Math.Pow(floors[floors.Count - 1].ValueMultiplier / floors[0].ValueMultiplier, 1.0 / (floors.Count - 1));
        }

        /// <summary>
        /// Re-slope the value multipliers to grow by <paramref name="growth"/> per floor. Each
        /// floor keeps its hand-tuned distance from the curve: only the slope changes.
        /// </summary>
        public static void SetValueGrowth(DescentContext data, double growth)
        {
            var current = ValueGrowth(data);
            if (current <= 0 || growth <= 0) return;
            var floors = FloorsInOrder(data);
            for (var i = 0; i < floors.Count; i++)
            {
                var value = (float)(floors[i].ValueMultiplier * Math.Pow(growth / current, i));
                ForkedData.Set(floors[i], f => f.ValueMultiplier, value);
            }
        }
    }
}
