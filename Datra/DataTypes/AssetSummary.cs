#nullable enable
using System;
using System.Collections.Generic;

namespace Datra.DataTypes
{
    /// <summary>
    /// Asset의 요약 정보 (실제 데이터 없이 메타데이터만 포함)
    /// InitializeAsync에서 전체 목록을 가볍게 로드할 때 사용
    /// </summary>
    public class AssetSummary
    {
        /// <summary>
        /// 기본 생성자
        /// </summary>
        public AssetSummary()
        {
        }

        /// <summary>
        /// ID, Metadata, FilePath로 생성 (Oratia 호환)
        /// </summary>
        public AssetSummary(AssetId id, AssetMetadata metadata, string filePath)
        {
            Id = id;
            FilePath = filePath;
            Metadata = metadata;
            Category = metadata.Category;
            Tags = metadata.Tags ?? (IReadOnlyList<string>)new List<string>();
            LastModified = metadata.ModifiedAt;
            FileSize = metadata.Size;
        }

        /// <summary>
        /// Asset의 고유 ID (GUID)
        /// </summary>
        public AssetId Id { get; set; }

        /// <summary>
        /// 파일 경로 (Asset 폴더 기준 상대 경로)
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 전체 메타데이터 (선택적, Oratia 호환용)
        /// </summary>
        public AssetMetadata? Metadata { get; set; }

        /// <summary>
        /// 파일 이름 (확장자 제외)
        /// </summary>
        public string Name => System.IO.Path.GetFileNameWithoutExtension(FilePath);

        /// <summary>
        /// 표시용 이름 (DisplayName 또는 파일명)
        /// </summary>
        public string DisplayName => Metadata?.DisplayName ?? Name;

        /// <summary>
        /// 카테고리 (메타데이터에서)
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// 태그 목록 (메타데이터에서)
        /// </summary>
        public IReadOnlyList<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// 마지막 수정 시간
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// 파일 크기 (바이트, 선택적)
        /// </summary>
        public long? FileSize { get; set; }

        /// <summary>
        /// 체크섬 (캐싱용, 선택적)
        /// </summary>
        public string? Checksum { get; set; }

        /// <summary>
        /// AssetMetadata에서 Summary 생성
        /// </summary>
        public static AssetSummary FromMetadata(AssetMetadata metadata, string filePath)
        {
            return new AssetSummary
            {
                Id = metadata.Guid,
                FilePath = filePath,
                Metadata = metadata,
                Category = metadata.Category,
                Tags = metadata.Tags ?? (IReadOnlyList<string>)new List<string>(),
                LastModified = metadata.ModifiedAt,
                FileSize = metadata.Size
            };
        }

        /// <summary>
        /// Asset에서 Summary 생성
        /// </summary>
        public static AssetSummary FromAsset<T>(Asset<T> asset) where T : class
        {
            return new AssetSummary
            {
                Id = asset.Id,
                FilePath = asset.FilePath,
                Metadata = asset.Metadata,
                Category = asset.Metadata.Category,
                Tags = asset.Metadata.Tags ?? (IReadOnlyList<string>)new List<string>(),
                LastModified = asset.Metadata.ModifiedAt,
                FileSize = asset.Metadata.Size
            };
        }
    }
}
