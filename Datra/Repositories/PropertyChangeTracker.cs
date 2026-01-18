#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Datra.Repositories
{
    /// <summary>
    /// Property-level 변경 추적기
    /// 특정 키의 속성별 변경 사항을 추적
    /// </summary>
    /// <typeparam name="TKey">키 타입</typeparam>
    public class PropertyChangeTracker<TKey> where TKey : notnull
    {
        private readonly Dictionary<(TKey key, string propertyName), PropertyChangeRecord> _changes = new();

        /// <summary>
        /// 속성 변경 기록
        /// </summary>
        private class PropertyChangeRecord
        {
            public object? BaselineValue { get; set; }
            public object? CurrentValue { get; set; }
        }

        /// <summary>
        /// 속성 변경 추적
        /// </summary>
        /// <param name="key">항목 키</param>
        /// <param name="propertyName">속성 이름</param>
        /// <param name="baselineValue">원본 값</param>
        /// <param name="newValue">새 값</param>
        /// <returns>속성이 변경되었는지 여부</returns>
        public bool TrackChange(TKey key, string propertyName, object? baselineValue, object? newValue)
        {
            var changeKey = (key, propertyName);
            bool isModified = !DeepCloner.DeepEquals(baselineValue, newValue);

            if (isModified)
            {
                _changes[changeKey] = new PropertyChangeRecord
                {
                    BaselineValue = baselineValue,
                    CurrentValue = newValue
                };
            }
            else
            {
                // 원본과 동일해지면 변경 기록 제거
                _changes.Remove(changeKey);
            }

            return isModified;
        }

        /// <summary>
        /// 특정 속성이 변경되었는지 확인
        /// </summary>
        public bool IsPropertyModified(TKey key, string propertyName)
        {
            return _changes.ContainsKey((key, propertyName));
        }

        /// <summary>
        /// 특정 키의 변경된 모든 속성 목록
        /// </summary>
        public IEnumerable<string> GetModifiedProperties(TKey key)
        {
            return _changes.Keys
                .Where(k => EqualityComparer<TKey>.Default.Equals(k.key, key))
                .Select(k => k.propertyName);
        }

        /// <summary>
        /// 특정 속성의 원본 값 (Baseline)
        /// </summary>
        public object? GetPropertyBaseline(TKey key, string propertyName)
        {
            if (_changes.TryGetValue((key, propertyName), out var record))
                return record.BaselineValue;
            return null;
        }

        /// <summary>
        /// 특정 키에 변경 사항이 있는지 확인
        /// </summary>
        public bool HasChangesForKey(TKey key)
        {
            return _changes.Keys.Any(k => EqualityComparer<TKey>.Default.Equals(k.key, key));
        }

        /// <summary>
        /// 특정 키의 모든 변경 사항 제거
        /// </summary>
        public void ClearChangesForKey(TKey key)
        {
            var keysToRemove = _changes.Keys
                .Where(k => EqualityComparer<TKey>.Default.Equals(k.key, key))
                .ToList();

            foreach (var k in keysToRemove)
            {
                _changes.Remove(k);
            }
        }

        /// <summary>
        /// 특정 속성의 변경 사항 제거
        /// </summary>
        public void ClearPropertyChange(TKey key, string propertyName)
        {
            _changes.Remove((key, propertyName));
        }

        /// <summary>
        /// 모든 변경 사항 초기화
        /// </summary>
        public void Clear()
        {
            _changes.Clear();
        }

        /// <summary>
        /// 변경된 키 목록
        /// </summary>
        public IEnumerable<TKey> GetChangedKeys()
        {
            return _changes.Keys.Select(k => k.key).Distinct();
        }

        /// <summary>
        /// 객체의 특정 속성 값 가져오기
        /// </summary>
        public static object? GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null) return null;

            var propInfo = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return propInfo?.GetValue(obj);
        }

        /// <summary>
        /// 객체의 특정 속성 값 설정
        /// </summary>
        public static bool SetPropertyValue(object obj, string propertyName, object? value)
        {
            if (obj == null) return false;

            var propInfo = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (propInfo == null || !propInfo.CanWrite)
                return false;

            try
            {
                propInfo.SetValue(obj, value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
