# Datra - Claude 작업 가이드

## 프로젝트 개요

Datra는 게임 개발용 데이터 관리 시스템으로, C# Source Generator를 사용해 CSV/JSON/YAML 데이터의 직렬화/역직렬화 코드를 자동 생성합니다.

## 프로젝트 구조

```
Datra/
├── Datra/                      # 핵심 라이브러리 (런타임)
│   ├── Attributes/             # [TableData], [SingleData], [DatraConfiguration] 등
│   ├── DataTypes/              # DataRef<T>, LocaleRef 등
│   ├── Interfaces/             # ITableData, IDataRepository 등
│   └── Plugins/                # Unity용 빌드된 DLL (Generators, Analyzers)
│
├── Datra.Generators/           # Source Generator (핵심!)
│   ├── Analyzers/              # DataModelAnalyzer - 클래스 분석
│   ├── Builders/               # CodeBuilder - 코드 생성 유틸리티
│   ├── Generators/             # 실제 코드 생성기들
│   │   ├── CsvSerializerBuilder.cs    # CSV 직렬화/역직렬화
│   │   ├── DataContextGenerator.cs    # DataContext 클래스 생성
│   │   ├── DataModelGenerator.cs      # 모델 partial 클래스 생성
│   │   └── JsonSerializerBuilder.cs   # JSON 직렬화/역직렬화
│   ├── Models/                 # DataModelInfo, PropertyInfo 등
│   └── DataContextSourceGenerator.cs  # 메인 진입점 (ISourceGenerator)
│
├── Datra.Analyzers/            # Roslyn Analyzer
├── Datra.SampleData/           # 테스트용 샘플 데이터 모델
├── Datra.SampleData2/          # 멀티 컨텍스트 테스트용
├── Datra.Tests/                # 유닛 테스트
├── Datra.Unity/                # Unity 패키지
└── Datra.Unity.Sample/         # Unity 샘플 프로젝트
```

## 빌드 명령어

```bash
# 전체 빌드 (Generator DLL → Unity 폴더로 복사)
./Scripts/build-all.sh

# 개별 빌드
dotnet build Datra.Generators/Datra.Generators.csproj -c Release
dotnet build Datra.SampleData/Datra.SampleData.csproj

# 테스트
dotnet test Datra.Tests/Datra.Tests.csproj

# 생성된 파일 확인 (EmitPhysicalFiles = true 설정 필요)
# DatraConfiguration.cs에서 EmitPhysicalFiles = true로 변경 후 빌드
```

## 핵심 파일 및 역할

### Source Generator 핵심 파일

| 파일 | 역할 |
|------|------|
| `DataContextSourceGenerator.cs` | 메인 진입점. DatraConfiguration 읽고 생성 시작 |
| `DataModelAnalyzer.cs` | 클래스 분석, PropertyInfo 추출, 중첩 타입 감지 |
| `CsvSerializerBuilder.cs` | CSV 직렬화/역직렬화 코드 생성 |
| `DataModelGenerator.cs` | 모델 partial 클래스, 생성자 생성 |
| `CodeBuilder.cs` | 코드 생성 헬퍼 (ToCamelCase, 예약어 처리 등) |

### 모델 정보 (DataModelInfo)

```csharp
// Datra.Generators/Models/DataModelInfo.cs
public class PropertyInfo
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool IsArray { get; set; }
    public bool IsEnum { get; set; }
    public bool IsDataRef { get; set; }
    public bool IsLocaleRef { get; set; }
    public bool IsNestedType { get; set; }      // 중첩 struct/class
    public bool IsNestedStruct { get; set; }    // struct vs class
    public string NestedTypeName { get; set; }
    public List<PropertyInfo> NestedProperties { get; set; }
}
```

## DatraConfiguration 설정

```csharp
[assembly: DatraConfiguration("GameData",
    Namespace = "MyGame.Generated",           // 필수! Unity 호환성
    EnableLocalization = true,
    LocalizationKeyDataPath = "Localizations/LocalizationKeys.csv",
    EmitPhysicalFiles = false                 // 디버깅용
)]
```

**Namespace는 필수** - 미설정시 `DATRA003` 에러 발생

## 자주 발생하는 문제

### 1. Unity에서 컴파일 에러
- **증상**: `CS0116`, `CS1514` 등 구문 에러
- **원인**:
  - C# 예약어가 변수명으로 사용됨 (예: `ref`)
  - 네임스페이스에 공백 포함
- **해결**:
  - `CodeBuilder.ToCamelCase`에서 예약어 처리 (`@ref`)
  - `Namespace` 명시적 설정 (필수화됨)

### 2. 중복 정의 에러
- **증상**: `CS0101` already contains a definition
- **원인**: `EmitPhysicalFiles = true`로 물리 파일 생성 후 끄지 않음
- **해결**: 생성된 `*.g.cs` 파일 삭제, `EmitPhysicalFiles = false`

### 3. Generator가 동작 안 함
- **원인**: DLL이 오래됨
- **해결**: `./Scripts/build-all.sh` 실행

## 코드 생성 흐름

```
1. DataContextSourceGenerator.Execute()
   ↓
2. DataAttributeSyntaxReceiver로 [TableData], [SingleData] 클래스 수집
   ↓
3. DataModelAnalyzer.AnalyzeClasses() - PropertyInfo 추출
   ↓
4. DataContextGenerator.GenerateDataContext() - Context 클래스 생성
   ↓
5. DataModelGenerator.GenerateDataModelFile() - 각 모델별 Serializer 생성
   ↓
6. context.AddSource()로 컴파일에 추가
```

## CSV 직렬화 특이사항

### 중첩 타입 (Nested Type)
```csv
Id,Name,ModelPrefab.Path,ModelPrefab.InitialCount
hero_001,Knight,Assets/Prefabs/Knight.prefab,5
```
- 점 표기법 사용: `PropertyName.FieldName`
- `CsvSerializerBuilder.GenerateNestedTypeDeserialization()` 참조

### 배열
```csv
Stats,UpgradeCosts
Strength|Agility,100|200|300
```
- 기본 구분자: `|` (config.CsvArrayDelimiter)

### DataRef
```csv
CharacterRef,ItemRefs
hero_001,1001|1002|1003
```
- ID만 저장, `Evaluate(context)`로 해결

## 테스트 방법

```bash
# 전체 테스트
dotnet test Datra.Tests/Datra.Tests.csproj

# 특정 테스트
dotnet test --filter "FullyQualifiedName~NestedTypeTests"
```

## 디버깅 팁

### 생성된 코드 확인
1. `DatraConfiguration`에서 `EmitPhysicalFiles = true` 설정
2. 빌드
3. `Datra.SampleData/` 폴더에 `*.g.cs` 파일 확인
4. 확인 후 `EmitPhysicalFiles = false`로 되돌리고 `*.g.cs` 삭제

### Generator 로그 확인
- `GeneratorLogger.Log()` 사용
- 빌드 시 `GeneratorDebugOutput.g.cs`에 로그 출력됨

## C# 예약어 목록

`CodeBuilder.CSharpKeywords`에 정의됨:
```
abstract, as, base, bool, break, byte, case, catch, char, checked,
class, const, continue, decimal, default, delegate, do, double, else,
enum, event, explicit, extern, false, finally, fixed, float, for,
foreach, goto, if, implicit, in, int, interface, internal, is, lock,
long, namespace, new, null, object, operator, out, override, params,
private, protected, public, readonly, ref, return, sbyte, sealed,
short, sizeof, stackalloc, static, string, struct, switch, this, throw,
true, try, typeof, uint, ulong, unchecked, unsafe, ushort, using,
virtual, void, volatile, while
```

프로퍼티 이름이 camelCase로 변환될 때 예약어면 `@` 접두사 추가됨.

## Git 커밋 컨벤션

- 기능 추가: `Add {feature description}`
- 버그 수정: `Fix {bug description}`
- 리팩토링: `Refactor {description}`
- Generator DLL 변경시 항상 함께 커밋

## 관련 이슈 히스토리

- **f608d9d**: multi-context 지원 추가 (ContextName 필수화)
- **c419b4c**: Unity Editor nested type 지원
- **6038cfb**: CSV nested struct/class 지원
- **b976a67**: Namespace 필수화, 예약어 처리
