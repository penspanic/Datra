#nullable enable
using System;
using Datra.Serializers;
using Newtonsoft.Json;

namespace Datra.Repositories
{
    /// <summary>
    /// JSON 직렬화/역직렬화를 사용한 깊은 복사 유틸리티.
    /// DatraJsonSettings.CreateForClone()과 동일한 설정을 사용하여
    /// EKDS 등 다른 Datra 컴포넌트와 일관된 직렬화를 보장합니다.
    /// </summary>
    public static class DeepCloner
    {
        private static readonly JsonSerializerSettings _settings = DatraJsonSettings.CreateForClone();

        /// <summary>
        /// 객체를 깊은 복사
        /// </summary>
        /// <typeparam name="T">복사할 타입</typeparam>
        /// <param name="source">원본 객체</param>
        /// <returns>복사된 새 객체</returns>
        public static T Clone<T>(T source) where T : class
        {
            if (source == null)
                return null!;

            try
            {
                var json = JsonConvert.SerializeObject(source, _settings);
                return JsonConvert.DeserializeObject<T>(json, _settings)!;
            }
            catch (Exception)
            {
                // JSON 직렬화 실패 시 원본 반환 (안전하지 않지만 예외 방지)
                return source;
            }
        }

        /// <summary>
        /// 두 값이 깊은 수준에서 동일한지 비교
        /// </summary>
        public static bool DeepEquals(object? a, object? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            // 값 타입이나 문자열은 직접 비교
            if (a.GetType().IsValueType || a is string)
                return a.Equals(b);

            // 복합 타입은 JSON 비교
            try
            {
                var jsonA = JsonConvert.SerializeObject(a, _settings);
                var jsonB = JsonConvert.SerializeObject(b, _settings);
                return jsonA == jsonB;
            }
            catch
            {
                return ReferenceEquals(a, b);
            }
        }
    }
}
