# Datra - 게임 데이터 관리 시스템

한국어 | [English](README.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET%20Standard-2.1-blue.svg)](https://dotnet.microsoft.com/)
[![Unity](https://img.shields.io/badge/Unity-2020.3+-black.svg)](https://unity.com/)

Datra는 다양한 데이터 형식(CSV, JSON, YAML)을 지원하고 C# Source Generator를 통해 자동 코드 생성을 제공하는 게임 개발용 종합 데이터 관리 시스템입니다. Unity와 표준 .NET 환경에서 모두 원활하게 작동하도록 설계되었습니다.

## 🚀 주요 기능

- **다양한 데이터 형식 지원**: CSV, JSON, YAML 파일 형식
- **자동 코드 생성**: C# Source Generator를 사용하여 보일러플레이트 코드 제거
- **타입 안정성**: 컴파일 타임 검증을 통한 강력한 타이핑
- **플랫폼 독립적**: Unity 및 표준 .NET 애플리케이션에서 작동
- **비동기 지원**: 모든 I/O 작업은 비동기로 처리
- **리포지토리 패턴**: 깔끔한 아키텍처를 위한 리포지토리 패턴 구현
- **Unity 패키지 지원**: Unity 패키지로 가져올 수 있음

## 🎬 Unity 에디터 데모

Datra는 Unity 내에서 게임 데이터를 직접 관리하고 시각화할 수 있는 강력한 Unity 에디터 창을 제공합니다:

<p align="center">
  <img src="docs/images/unity-editor-demo.gif" alt="Unity Editor Demo" width="100%">
</p>

에디터 창의 주요 기능:
- 실시간 데이터 시각화 및 편집
- 다양한 데이터 형식 지원 (CSV, JSON, YAML)
- 자동 코드 생성 통합
- 타입 안전한 데이터 관리
- 게임 디자이너와 개발자를 위한 직관적인 UI

## 🔥 핵심 기능 및 예제

### 📋 기본 데이터 모델

간단한 속성으로 게임 데이터를 정의합니다:

```csharp
using Datra.Attributes;
using Datra.Interfaces;

// 여러 항목을 위한 테이블 데이터 (예: 캐릭터 데이터베이스)
[TableData("Characters.csv", Format = DataFormat.Csv)]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
    public int Health { get; set; }
    public int Mana { get; set; }
}

// 설정을 위한 단일 데이터 (예: 게임 설정)
[SingleData("GameConfig.json", Format = DataFormat.Json)]
public partial class GameConfigData
{
    public string GameName { get; set; }
    public int MaxLevel { get; set; }
    public float ExpMultiplier { get; set; }
}
```

### 🔗 DataRef<>를 사용한 데이터 참조

타입 안전한 DataRef<> 속성으로 다른 데이터 테이블을 참조합니다. DataRef는 참조된 데이터의 ID를 저장하고 컨텍스트를 사용하여 해결합니다:

```csharp
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;

[TableData("RefTestDataList.csv", Format = DataFormat.Csv)]
public partial class RefTestData : ITableData<string>
{
    public string Id { get; set; }
    
    // 문자열 ID로 캐릭터 참조
    public StringDataRef<CharacterData> CharacterRef { get; set; }
    
    // 정수 ID로 아이템 참조
    public IntDataRef<ItemData> ItemRef { get; set; }
    
    // 아이템 참조 배열
    public IntDataRef<ItemData>[] ItemRefs { get; set; }
}

// 사용 예제
var refData = context.RefTestData.GetById("test_001");
var character = refData.CharacterRef.Evaluate(context); // 컨텍스트로 참조 해결
var item = refData.ItemRef.Evaluate(context);           // 타입 안전한 해결
```

CSV 파일에서 참조는 ID로 저장되고 배열은 파이프(|) 구분자를 사용합니다:
```csv
Id,CharacterRef,ItemRef,ItemRefs
test_01,hero_011,1001,1001|1002|1003
test_02,hero_002,1002,2001|2002
```

참고: DataRef<>는 ID 값만 저장합니다. 실제 참조된 데이터를 가져오려면 `Evaluate(context)` 메서드를 사용하세요.

### 🎯 Enum 지원

더 나은 타입 안정성과 가독성을 위해 enum을 사용하세요:

```csharp
public enum CharacterGrade
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum ItemType
{
    Weapon,
    Armor,
    Consumable,
    Material
}

[TableData("Characters.csv", Format = DataFormat.Csv)]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public CharacterGrade Grade { get; set; }  // Enum 속성
    public StatType[] Stats { get; set; }      // Enum 배열
}
```

### 📚 배열 지원

배열 속성으로 여러 값을 저장합니다:

```csharp
[SingleData("GameConfig.json", Format = DataFormat.Json)]
public partial class GameConfigData
{
    public GameMode[] AvailableModes { get; set; }
    public RewardType[] EnabledRewards { get; set; }
    
    // 데이터 참조 배열
    public StringDataRef<CharacterData>[] UnlockableCharacters { get; set; }
    public IntDataRef<ItemData>[] StartingItems { get; set; }
}

[TableData("Characters.csv", Format = DataFormat.Csv)]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public int[] UpgradeCosts { get; set; }  // 정수 배열
    public StatType[] Stats { get; set; }    // Enum 배열
}
```

### 🏠 중첩 타입 지원

데이터 모델 내에 struct나 class 타입을 포함할 수 있습니다. 중첩 타입은 CSV에서 점 표기법으로 직렬화됩니다:

```csharp
// 중첩 타입 정의
public struct PooledPrefab
{
    public string Path { get; set; }
    public int InitialCount { get; set; }
    public int MaxCount { get; set; }
}

[TableData("Characters.csv", Format = DataFormat.Csv)]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public PooledPrefab ModelPrefab { get; set; }  // 중첩 struct
}
```

CSV 파일에서 중첩 속성은 열 헤더에 점 표기법을 사용합니다:
```csv
Id,Name,ModelPrefab.Path,ModelPrefab.InitialCount,ModelPrefab.MaxCount
hero_001,Knight,Assets/Prefabs/Knight.prefab,5,20
hero_002,Mage,Assets/Prefabs/Mage.prefab,3,15
```

참고: 중첩 타입은 한 단계의 중첩만 지원합니다. 깊게 중첩된 타입(중첩 내부의 중첩)은 지원되지 않습니다.

### 🎨 복합 데이터 모델

풍부한 데이터 구조를 위해 모든 기능을 결합합니다:

```csharp
[TableData("Items.json")]  // 확장자에서 형식이 자동 감지됨
public partial class ItemData : ITableData<int>
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int Price { get; set; }
    public ItemType Type { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
}

[SingleData("GameConfig.json", Format = DataFormat.Json)]
public partial class GameConfigData
{
    public string GameName { get; set; }
    public int MaxLevel { get; set; }
    public float ExpMultiplier { get; set; }
    
    // Enum 속성
    public GameMode DefaultMode { get; set; }
    public GameMode[] AvailableModes { get; set; }
    
    // 데이터 참조
    public StringDataRef<CharacterData> DefaultCharacter { get; set; }
    public IntDataRef<ItemData> StartingItem { get; set; }
    
    // 참조 배열
    public StringDataRef<CharacterData>[] UnlockableCharacters { get; set; }
    public IntDataRef<ItemData>[] StartingItems { get; set; }
}

// 사용법: 참조 해결
var config = context.GameConfig.Get();
var defaultCharacter = config.DefaultCharacter.Evaluate(context);
var startingItem = config.StartingItem.Evaluate(context);

// 배열 참조 해결
foreach (var charRef in config.UnlockableCharacters)
{
    var character = charRef.Evaluate(context);
    Console.WriteLine($"잠금 해제 가능: {character?.Name}");
}
```

## 📦 프로젝트 구조

```
Datra/
├── Datra.sln                # 메인 솔루션 파일
├── Datra/                   # 데이터 로딩 및 리포지토리 시스템
├── Datra.Generators/        # 자동 코드 생성을 위한 소스 생성기
├── Datra.Analyzers/         # 사용자 코드를 위한 소스 분석기
├── Datra.Tests/             # 유닛 테스트 프로젝트
└── Datra.Client/            # Unity 클라이언트 프로젝트 예제
```

## 🛠️ 설치

### .NET 프로젝트용

1. 저장소를 클론합니다:
```bash
git clone https://github.com/penspanic/Datra.git
```

2. 솔루션에 프로젝트 참조를 추가합니다:
```xml
<ProjectReference Include="path/to/Datra.Core/Datra.Core.csproj" />
<ProjectReference Include="path/to/Datra.Generators/Datra.Generators.csproj" 
                  OutputItemType="Analyzer" 
                  ReferenceOutputAssembly="false" />
```

### Unity 프로젝트용

1. Unity Package Manager에서 "+" 버튼을 클릭하고 "Add package from git URL..."을 선택합니다.

2. 다음 URL을 입력합니다:
```
https://github.com/penspanic/Datra.git?path=Datra.Data
```

3. Unity가 자동으로 종속성과 함께 패키지를 가져옵니다.

또는 Unity 프로젝트의 `Packages/manifest.json`에 직접 추가할 수 있습니다:
```json
{
  "dependencies": {
    "com.datra.data": "https://github.com/penspanic/Datra.git?path=Datra.Data"
  }
}
```

## 📝 사용법

### 1. 데이터 모델 정의

```csharp
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;

[TableData("Characters.csv", Format = DataFormat.Csv)]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
    public float Health { get; set; }
    public float Mana { get; set; }
    public CharacterGrade Grade { get; set; }  // Enum 지원
}

[SingleData("GameConfig.yaml", Format = DataFormat.Yaml)]
public partial class GameConfig
{
    public int MaxLevel { get; set; }
    public float ExpMultiplier { get; set; }
    public int StartingGold { get; set; }
    public StringDataRef<CharacterData> DefaultCharacter { get; set; }  // 캐릭터 참조
}
```

### 2. 데이터 컨텍스트 설정

`DatraConfiguration` 속성을 어셈블리에 추가하여 생성된 컨텍스트를 구성합니다:

```csharp
using Datra.Attributes;

// AssemblyInfo.cs 또는 프로젝트의 아무 .cs 파일에
[assembly: DatraConfiguration("GameData",
    Namespace = "MyGame.Generated",           // 필수: 생성된 코드의 네임스페이스
    EnableLocalization = true,                // 선택: 지역화 지원 활성화
    LocalizationKeyDataPath = "Localizations/LocalizationKeys.csv",
    EmitPhysicalFiles = false                 // 선택: 생성된 코드 디버깅용
)]
```

**참고**: `Namespace` 속성은 **필수**입니다. 이는 Unity와 .NET 환경 간에 일관된 네임스페이스 동작을 보장합니다. 설정하지 않으면 컴파일 에러 `DATRA003`이 발생합니다.

### 3. 데이터 컨텍스트 생성

Source Generator가 모델을 기반으로 DataContext 클래스를 자동으로 생성합니다. 생성된 `GameDataContext` 클래스는 설정된 네임스페이스에 배치됩니다:

```csharp
// 이 클래스는 Datra.Generated 네임스페이스에 자동 생성됩니다
namespace Datra.Generated
{
    public partial class GameDataContext : IDataContext
    {
        public IDataRepository<string, CharacterData> Character { get; }
        public ISingleDataRepository<GameConfig> GameConfig { get; }
        
        public async Task LoadAllAsync() { /* ... */ }
    }
}
```

### 4. 데이터 로드 및 사용

```csharp
using Datra.Generated;

// 데이터 제공자와 로더 팩토리 생성
var rawDataProvider = new FileRawDataProvider("path/to/data");
var loaderFactory = new DataLoaderFactory();

// 컨텍스트 생성 (GameDataContext는 Datra.Generated 네임스페이스에 있음)
var context = new GameDataContext(rawDataProvider, loaderFactory);

// 모든 데이터 로드
await context.LoadAllAsync();

// 데이터 사용
var character = context.Character.GetById("hero_001");
var allCharacters = context.Character.GetAll();
var config = context.GameConfig.Get();

// 데이터 참조 사용
var refData = context.RefTestData.GetById("test_001");
var referencedCharacter = refData.CharacterRef.Evaluate(context);  // 컨텍스트로 해결
var referencedItem = refData.ItemRef.Evaluate(context);

// 배열 작업
foreach (var itemRef in refData.ItemRefs)
{
    var item = itemRef.Evaluate(context);  // 각 참조가 해결됨
    Console.WriteLine($"아이템: {item.Name}, 가격: {item.Price}");
}
```

## 🎮 Unity 통합

Datra는 Unity와 원활하게 작동하도록 설계되었습니다. Unity 프로젝트 예제(`Datra.Client`)는 다음을 보여줍니다:

- Unity 패키지 시스템과의 통합
- Unity 환경에서의 리소스 로딩
- MonoBehaviour 및 ScriptableObject에서의 사용
- Unity의 비동기 패턴과의 호환성
- **시각적 데이터 관리를 위한 커스텀 Unity 에디터 창** (위 데모 참조)

### Unity 전용 기능

- Unity 전용 코드 경로를 위한 조건부 컴파일
- Unity 패키지 매니페스트 지원
- Unity 2020.3 이상과 호환
- 직관적인 데이터 관리를 위한 커스텀 에디터 UI
- 실시간 데이터 미리보기 및 편집 기능

## 📊 지원되는 데이터 형식

### CSV 파일
캐릭터 스탯, 아이템 속성 등의 표 형식 데이터에 최적입니다.

```csv
Id,Name,Level,Health,Mana
hero_001,Knight,10,150,50
hero_002,Mage,8,80,200
```

### JSON 파일
복잡한 중첩 구조와 배열에 이상적입니다.

```json
{
  "items": [
    {
      "id": 1001,
      "name": "Iron Sword",
      "damage": 10,
      "price": 100
    }
  ]
}
```

### YAML 파일
가독성이 좋은 설정 파일에 완벽합니다.

```yaml
MaxLevel: 100
ExpMultiplier: 1.5
StartingGold: 1000
```

## 🏗️ 아키텍처

Datra는 깔끔한 아키텍처 접근 방식을 사용합니다:

1. **데이터 레이어** (`Datra.Data`): 데이터 로딩 및 리포지토리 패턴 처리
2. **코드 생성** (`Datra.Generators`): Roslyn을 사용한 자동 코드 생성
3. **모델** (여러분의 프로젝트): 속성으로 데이터 구조 정의
4. **컨텍스트** (생성됨): 데이터 접근을 위한 자동 생성 컨텍스트

## 🤝 기여하기

기여를 환영합니다! Pull Request를 자유롭게 제출해주세요. 주요 변경사항의 경우 먼저 이슈를 열어 변경하고자 하는 내용을 논의해주세요.

1. 저장소를 포크합니다
2. 기능 브랜치를 생성합니다 (`git checkout -b feature/AmazingFeature`)
3. 변경사항을 커밋합니다 (`git commit -m 'Add some AmazingFeature'`)
4. 브랜치에 푸시합니다 (`git push origin feature/AmazingFeature`)
5. Pull Request를 엽니다

## 📄 라이선스

이 프로젝트는 MIT 라이선스에 따라 라이선스가 부여됩니다 - 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.

## 🙏 감사의 말

- Entity Framework의 DbContext 패턴에서 영감을 받음
- 코드 생성을 위해 Roslyn Source Generator 사용
- 게임 개발자를 위한 애정으로 제작됨

## 📞 연락처

- 프로젝트 링크: [https://github.com/penspanic/Datra](https://github.com/penspanic/Datra)
- 이슈: [https://github.com/penspanic/Datra/issues](https://github.com/penspanic/Datra/issues)

---

타입 안전하고 효율적인 데이터 관리를 원하는 게임 개발자를 위해 ❤️로 만들었습니다.
