# Oratia Runtime + Datra 통합 분석

## 전제 조건

- **Oratia는 파격적 개편 가능** - 기존 데이터 완전 삭제 OK
- **마이그레이션 불필요** - 처음부터 새로 설계
- **최소 노력으로 직렬화 구현**이 목표
- CSV 포맷 강제 아님 - JSON 사용 가능

---

## 1. 통합 목표

### 1.1 시나리오

```
Oratia Editor (Web)          Unity Game (PetroHunter2)
     │                              │
     │  Graph 파일 생성/편집          │  Oratia Runtime 실행
     │  CharacterInfo 편집           │  Graph 데이터 로드 (읽기 전용)
     │                              │
     ▼                              ▼
  개별 JSON 파일들  ───export───>  Datra로 로드
  (graph_001.json)               (OratiaDataContext)
  (graph_002.json)
```

### 1.2 핵심 요구사항

| 요구사항 | 설명 |
|---------|------|
| **별도 DataContext** | `OratiaDataContext` - Oratia.Runtime 어셈블리에 정의 |
| **Multi-file TableData** | Graph는 파일당 1개 → 폴더의 여러 JSON 파일 로드 |
| **Addressables 호환** | 런타임에서 Addressables로 로드 가능해야 함 |

---

## 2. 현재 Datra 지원 현황

### 2.1 이미 존재하는 기능

| 기능 | 상태 |
|------|------|
| JSON 직렬화 | ✅ `JsonSerializerBuilder.cs` |
| SingleData | ✅ `SingleDataAttribute.cs` |
| LocaleRef / FixedLocale | ✅ 있음 |
| DataRef | ✅ `StringDataRef`, `IntDataRef` |
| Addressables Provider | ✅ `AddressableRawDataProvider` |

### 2.2 없는 기능 (추가 필요)

| 기능 | 설명 |
|------|------|
| **Multi-file TableData** | 폴더 내 여러 파일을 하나의 테이블로 로드 |
| **IRawDataProvider 파일 리스트** | `ListFilesAsync()` 메서드 없음 |

---

## 3. Multi-file TableData 구현 방안

### 3.1 문제점: Addressables 제약

현재 `IRawDataProvider` 인터페이스:
```csharp
public interface IRawDataProvider
{
    Task<string> LoadTextAsync(string path);  // 단일 파일만
    bool Exists(string path);
    string ResolveFilePath(string path);
}
```

**Addressables 환경 제약:**
- 폴더 검색 불가 (키 기반 로드만)
- `Directory.GetFiles()` 런타임에 사용 불가

| Provider | 폴더 스캔 | 런타임 |
|----------|---------|--------|
| AssetDatabaseRawDataProvider | ✅ 가능 | ❌ Editor Only |
| AddressableRawDataProvider | ❌ 불가 | ✅ Runtime |

### 3.2 해결 방안

#### 옵션 A: Addressables Label 기반 (권장)

```csharp
// Attribute 확장
[TableData(Label = "OratiaGraph", Format = DataFormat.Json, MultiFile = true)]
public partial class Graph : ITableData<string> { }
```

**Addressables 사용:**
```csharp
// Label로 다중 에셋 로드
var handle = Addressables.LoadAssetsAsync<TextAsset>("OratiaGraph", null);
var textAssets = await handle.Task;

foreach (var textAsset in textAssets)
{
    var graph = JsonConvert.DeserializeObject<Graph>(textAsset.text);
    result[graph.Id] = graph;
}
```

**장점:**
- Addressables의 native 기능 활용
- 파일 추가/삭제 시 Label만 지정하면 됨
- 런타임 성능 최적화 (Addressables가 관리)

**단점:**
- Unity Editor에서 Label 설정 필요
- 빌드 시 Addressables Group 관리 필요

#### 옵션 B: 매니페스트 파일

```csharp
[TableData(Manifest = "Graphs/_manifest.json", Format = DataFormat.Json)]
public partial class Graph : ITableData<string> { }
```

**매니페스트 파일:**
```json
// Graphs/_manifest.json
{
  "files": [
    "Graphs/graph_001.json",
    "Graphs/graph_002.json",
    "Graphs/graph_003.json"
  ]
}
```

**장점:**
- Addressables Label 설정 불필요
- 파일 목록을 코드/빌드 시스템에서 자동 생성 가능

**단점:**
- 매니페스트 파일 관리 필요
- 파일 추가 시 매니페스트 업데이트 필요

#### 옵션 C: IRawDataProvider 확장

```csharp
public interface IRawDataProvider
{
    // 기존 메서드...

    // 새 메서드 (선택적 구현)
    Task<IEnumerable<string>> ListFilesAsync(string folderPath, string pattern = "*");
}
```

**AddressableRawDataProvider 구현:**
```csharp
public async Task<IEnumerable<string>> ListFilesAsync(string folderPath, string pattern)
{
    // Label = folderPath 로 사용
    var handle = Addressables.LoadResourceLocationsAsync(folderPath, typeof(TextAsset));
    var locations = await handle.Task;
    return locations.Select(loc => loc.PrimaryKey);
}
```

### 3.3 권장 접근법

**옵션 A (Label 기반)** 권장:
1. Addressables의 검증된 기능 활용
2. 런타임 성능 최적화
3. 빌드 파이프라인과 자연스러운 통합

---

## 4. 구현 계획

### 4.1 Datra 확장

| 파일 | 변경 내용 |
|------|----------|
| `TableDataAttribute.cs` | `Label`, `MultiFile` 프로퍼티 추가 |
| `DataModelInfo.cs` | `IsMultiFile`, `Label` 메타데이터 |
| `DataModelAnalyzer.cs` | Multi-file 감지 |
| `JsonSerializerBuilder.cs` | Multi-file 역직렬화 코드 생성 |
| `AddressableRawDataProvider.cs` | Label 기반 다중 로드 지원 |

### 4.2 TableDataAttribute 확장 예시

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class TableDataAttribute : Attribute
{
    public string Path { get; }
    public DataFormat Format { get; set; } = DataFormat.Csv;

    // 새 프로퍼티
    public bool MultiFile { get; set; } = false;
    public string Label { get; set; }  // Addressables Label
}
```

### 4.3 생성 코드 예시

```csharp
// 기존 (단일 파일)
public static async Task<Dictionary<string, Graph>> LoadAsync(IRawDataProvider provider)
{
    var json = await provider.LoadTextAsync("Graphs/graphs.json");
    var list = JsonConvert.DeserializeObject<List<Graph>>(json);
    return list.ToDictionary(x => x.Id);
}

// Multi-file (Label 기반)
public static async Task<Dictionary<string, Graph>> LoadAsync(IRawDataProvider provider)
{
    var assets = await provider.LoadAssetsByLabelAsync<TextAsset>("OratiaGraph");
    var result = new Dictionary<string, Graph>();
    foreach (var asset in assets)
    {
        var graph = JsonConvert.DeserializeObject<Graph>(asset.text);
        result[graph.Id] = graph;
    }
    return result;
}
```

---

## 5. Oratia 데이터 모델 변환

### 5.1 CharacterInfo

```csharp
// Before (Oratia)
public class CharacterInfo : IKeyValueInfo
{
    public string Id { get; set; }
    [JsonIgnore][FixedLocale]
    public Locale Name => Locale.Fixed(nameof(CharacterInfo), Id, nameof(Name));
}

// After (Datra)
[TableData("Oratia/characters.json", Format = DataFormat.Json)]
public partial class CharacterInfo : ITableData<string>
{
    public string Id { get; set; }
    [FixedLocale]
    public LocaleRef Name => LocaleRef.CreateFixed(nameof(CharacterInfo), Id, nameof(Name));
}
```

### 5.2 Graph (Multi-file)

```csharp
// Datra Multi-file TableData
[TableData(Label = "OratiaGraph", Format = DataFormat.Json, MultiFile = true)]
public partial class Graph : ITableData<string>
{
    public string Id { get; set; }  // 파일명 또는 내부 ID
    public List<Node> Nodes { get; set; }
    public List<Choice> Choices { get; set; }
}
```

### 5.3 폴더 구조

```
Assets/StaticData/
├── TableData/              # 기존 PetroHunter 데이터
│   ├── Character.csv
│   └── ...
│
└── Oratia/                 # Oratia 데이터 (별도 폴더)
    ├── characters.json     # 단일 파일 TableData
    ├── items.json
    └── Graphs/             # Multi-file TableData
        ├── scene_001.json
        ├── scene_002.json
        └── scene_003.json
```

---

## 6. OratiaDataContext 구조

### 6.1 DatraConfiguration

```csharp
// Oratia.Runtime/Data/OratiaConfiguration.cs
[assembly: DatraConfiguration(
    DataContextName = "OratiaDataContext",
    GeneratedNamespace = "Oratia.Generated"
)]
```

### 6.2 생성되는 Context

```csharp
// Generated
public class OratiaDataContext : IDataContext
{
    public IReadOnlyDictionary<string, CharacterInfo> Character { get; }
    public IReadOnlyDictionary<string, ItemInfo> Item { get; }
    public IReadOnlyDictionary<string, Graph> Graph { get; }  // Multi-file

    public async Task LoadAllAsync()
    {
        // Label 기반으로 Graph 폴더의 모든 파일 로드
    }
}
```

### 6.3 사용 예시 (PetroHunter2)

```csharp
public class OratiaManager : MonoBehaviour
{
    private OratiaDataContext _oratiaContext;

    public async UniTask InitializeAsync()
    {
        var provider = new AddressableRawDataProvider("Oratia");
        _oratiaContext = new OratiaDataContext(provider, ...);
        await _oratiaContext.LoadAllAsync();
    }

    public Graph GetGraph(string id) => _oratiaContext.Graph[id];
}
```

---

## 7. Oratia.Core에서 Datra 사용

### 7.1 Datra 환경 호환성

**Datra Core (netstandard2.1)**:
- Unity 의존성 없음 (`#if UNITY_*`로 격리됨)
- Blazor/ASP.NET 환경에서 사용 가능

**IRawDataProvider 구현 현황:**

| 환경 | Provider | 상태 |
|-----|----------|------|
| Unity Runtime | AddressableRawDataProvider | ✅ 있음 |
| Unity Editor | AssetDatabaseRawDataProvider | ✅ 있음 |
| .NET (Oratia Editor) | FileSystemRawDataProvider | ❌ 필요 |

### 7.2 FileSystemRawDataProvider 추가

**파일**: `Datra/Providers/FileSystemRawDataProvider.cs`

```csharp
using System.IO;
using System.Threading.Tasks;
using Datra.Interfaces;

namespace Datra.Providers
{
    /// <summary>
    /// File system based data provider for non-Unity environments
    /// </summary>
    public class FileSystemRawDataProvider : IRawDataProvider
    {
        private readonly string _basePath;

        public FileSystemRawDataProvider(string basePath)
        {
            _basePath = basePath;
        }

        public async Task<string> LoadTextAsync(string path)
        {
            var fullPath = Path.Combine(_basePath, path);
            return await File.ReadAllTextAsync(fullPath);
        }

        public async Task SaveTextAsync(string path, string content)
        {
            var fullPath = Path.Combine(_basePath, path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(fullPath, content);
        }

        public bool Exists(string path)
        {
            return File.Exists(Path.Combine(_basePath, path));
        }

        public string ResolveFilePath(string path)
        {
            return Path.GetFullPath(Path.Combine(_basePath, path));
        }

        // Multi-file 지원
        public IEnumerable<string> ListFiles(string folderPath, string pattern = "*.json")
        {
            var fullPath = Path.Combine(_basePath, folderPath);
            if (!Directory.Exists(fullPath))
                return Enumerable.Empty<string>();
            return Directory.GetFiles(fullPath, pattern)
                .Select(f => Path.GetRelativePath(_basePath, f));
        }
    }
}
```

### 7.3 OratiaDataContext 위치

**Oratia.Core에 정의** (Unity/Web 공용):

```csharp
// Oratia.Core/Data/OratiaConfiguration.cs
[assembly: DatraConfiguration(
    DataContextName = "OratiaDataContext",
    GeneratedNamespace = "Oratia.Generated"
)]
```

**사용 환경별 Provider:**

```csharp
// Oratia Editor (Blazor)
var provider = new FileSystemRawDataProvider("/path/to/data");
var context = new OratiaDataContext(provider, ...);

// Unity Runtime
var provider = new AddressableRawDataProvider("Oratia");
var context = new OratiaDataContext(provider, ...);
```

---

## 8. Graph 데이터 처리 전략

### 8.1 Oratia Editor에서 Graph 관리

Graph는 동적 생성/수정이 필요하므로 **Oratia DataService 유지**:

```csharp
// Oratia Editor - 동적 파일 관리는 기존 DataService 사용
await _graphStorageService.CreateFileAsync("new_graph", graphContent, userId);
await _graphStorageService.SaveFileAsync(graphFile);
```

### 8.2 Unity Runtime에서 Graph 로드

읽기 전용이므로 **Datra Multi-file TableData 사용**:

```csharp
// Unity Runtime - Datra로 로드
[TableData(Label = "OratiaGraph", Format = DataFormat.Json, MultiFile = true)]
public partial class Graph : ITableData<string> { }

// 사용
var graph = oratiaContext.Graph["scene_001"];
```

### 8.3 하이브리드 전략

| 기능 | Oratia Editor | Unity Runtime |
|-----|--------------|---------------|
| CharacterInfo 로드 | OratiaDataContext | OratiaDataContext |
| ItemInfo 로드 | OratiaDataContext | OratiaDataContext |
| Localization | Datra LocalizationContext | Datra LocalizationContext |
| Graph 로드 | DataService (동적) | OratiaDataContext (Multi-file) |
| Graph 생성/수정 | DataService | ❌ 불가 |

---

## 9. 결론

### 9.1 핵심 통찰

1. **Datra Core는 플랫폼 독립적** - Blazor/Unity 모두 사용 가능
2. **OratiaDataContext는 Oratia.Core에 정의** - 공용 데이터 모델 및 Context
3. **FileSystemRawDataProvider 필요** - Oratia Editor (Blazor)용
4. **Graph는 하이브리드** - Editor는 DataService, Runtime은 Datra

### 9.2 구현 우선순위

| 순서 | 작업 | 위치 |
|-----|------|------|
| 1 | FileSystemRawDataProvider | Datra Core |
| 2 | Multi-file TableData (Label 기반) | Datra Generators |
| 3 | CharacterInfo, ItemInfo Datra 변환 | Oratia.Core |
| 4 | OratiaDataContext 생성 | Oratia.Core |
| 5 | Localization Datra 통합 | Oratia.Core |
| 6 | Graph Multi-file 적용 (런타임용) | Oratia.Core |

### 9.3 Datra 변경 범위

```
Datra/
├── Providers/
│   └── FileSystemRawDataProvider.cs  # 신규 - .NET용
├── Interfaces/
│   └── IRawDataProvider.cs           # ListFiles 메서드 추가 (선택)
├── Attributes/
│   └── TableDataAttribute.cs         # Label, MultiFile 추가
├── Generators/
│   └── JsonSerializerBuilder.cs      # Multi-file 코드 생성
└── Unity/Addressables/
    └── AddressableRawDataProvider.cs # Label 기반 로드
```

### 9.4 Oratia 변경 범위

```
Oratia.Core/
├── Data/
│   └── OratiaConfiguration.cs        # DatraConfiguration
├── Models/Info/
│   ├── CharacterInfo.cs              # ITableData 변환
│   └── ItemInfo.cs                   # ITableData 변환
└── Models/Graph/
    └── Graph.cs                      # Multi-file TableData (런타임용)
```
