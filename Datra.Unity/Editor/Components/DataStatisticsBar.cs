using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Datra.Unity.Editor.Components
{
    /// <summary>
    /// Generic statistics bar that can display multiple key-value statistics.
    /// Can be used in any data view (TableView, FormView, etc.)
    /// </summary>
    public class DataStatisticsBar : VisualElement
    {
        private Dictionary<string, Label> statisticLabels;
        private VisualElement container;

        public DataStatisticsBar()
        {
            AddToClassList("data-statistics-bar");

            statisticLabels = new Dictionary<string, Label>();

            container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            Add(container);
        }

        /// <summary>
        /// Set statistics with optional color formatting.
        /// Clears existing statistics and rebuilds the bar.
        /// </summary>
        /// <param name="statistics">Array of tuples containing (key, value, color)</param>
        public void SetStatistics(params (string key, object value, Color? color)[] statistics)
        {
            container.Clear();
            statisticLabels.Clear();

            for (int i = 0; i < statistics.Length; i++)
            {
                var (key, value, color) = statistics[i];

                var label = new Label($"{key}: {FormatValue(value)}");
                label.AddToClassList("statistic-item");

                if (color.HasValue)
                    label.style.color = color.Value;

                container.Add(label);
                statisticLabels[key] = label;

                // Add separator (except last)
                if (i < statistics.Length - 1)
                {
                    var separator = new Label("|");
                    separator.AddToClassList("statistic-separator");
                    container.Add(separator);
                }
            }
        }

        /// <summary>
        /// Update a specific statistic without rebuilding the entire bar.
        /// More efficient than SetStatistics when only one value changes.
        /// </summary>
        /// <param name="key">The statistic key to update</param>
        /// <param name="value">The new value</param>
        /// <param name="color">Optional color override</param>
        public void UpdateStatistic(string key, object value, Color? color = null)
        {
            if (statisticLabels.TryGetValue(key, out var label))
            {
                label.text = $"{key}: {FormatValue(value)}";

                if (color.HasValue)
                    label.style.color = color.Value;
            }
        }

        /// <summary>
        /// Clear all statistics
        /// </summary>
        public void ClearStatistics()
        {
            container.Clear();
            statisticLabels.Clear();
        }

        /// <summary>
        /// Format value for display with proper number formatting
        /// </summary>
        private string FormatValue(object value)
        {
            if (value == null)
                return "0";

            if (value is int intValue)
                return intValue.ToString("N0");

            if (value is long longValue)
                return longValue.ToString("N0");

            if (value is float floatValue)
                return floatValue.ToString("N2");

            if (value is double doubleValue)
                return doubleValue.ToString("N2");

            if (value is decimal decimalValue)
                return decimalValue.ToString("N2");

            return value.ToString();
        }
    }
}
