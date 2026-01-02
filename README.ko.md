# Datra

한국어 | [English](README.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET%20Standard-2.1-blue.svg)](https://dotnet.microsoft.com/)
[![Unity](https://img.shields.io/badge/Unity-2020.3+-black.svg)](https://unity.com/)

**Datra**는 C# Source Generator를 사용하여 CSV, JSON, YAML 데이터의 직렬화 코드를 자동 생성하는 게임 데이터 관리 시스템입니다. Unity와 .NET 환경 모두에서 원활하게 작동합니다.

## 주요 기능

- **다양한 포맷**: CSV, JSON, YAML 자동 감지 지원
- **보일러플레이트 제거**: Source Generator로 직렬화 코드 자동 생성
- **타입 안전성**: 강력한 타이핑과 컴파일 타임 검증
- **데이터 참조**: 테이블 간 타입 안전한 `DataRef<T>` 참조
- **Unity 통합**: Table/Form 뷰를 갖춘 에디터 윈도우 내장
- **로컬라이제이션**: `LocaleRef`를 통한 다국어 지원
- **고급 타입**: 중첩 struct, 다형성 JSON, 배열, enum

## 빠른 시작

### 1. 데이터 모델 정의

```csharp
using Datra.Attributes;
using Datra.Interfaces;

[TableData("Characters.csv")]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
    public int Health { get; set; }
}

[SingleData("GameConfig.json")]
public partial class GameConfigData
{
    public string GameName { get; set; }
    public int MaxLevel { get; set; }
}
```

### 2. 데이터 컨텍스트 설정

```csharp
[assembly: DatraConfiguration("GameData",
    Namespace = "MyGame.Generated"  // 필수
)]
```

### 3. 데이터 로드 및 사용

```csharp
var provider = new FileRawDataProvider("path/to/data");
var context = new GameDataContext(provider, new DataLoaderFactory());

await context.LoadAllAsync();

var hero = context.Character.GetById("hero_001");
var config = context.GameConfig.Get();
```

## 설치

### Unity

`Packages/manifest.json`에 추가:
```json
{
  "dependencies": {
    "com.penspanic.datra": "https://github.com/penspanic/Datra.git?path=Datra/Plugins",
    "com.penspanic.datra.unity": "https://github.com/penspanic/Datra.git?path=Datra.Unity/Runtime",
    "com.penspanic.datra.editor": "https://github.com/penspanic/Datra.git?path=Datra.Unity/Editor"
  }
}
```

### .NET

프로젝트 참조 추가:
```xml
<ProjectReference Include="path/to/Datra/Datra.csproj" />
<ProjectReference Include="path/to/Datra.Generators/Datra.Generators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Unity 에디터

<p align="center">
  <img src="docs/images/unity-editor-demo.gif" alt="Unity Editor Demo" width="100%">
</p>

Unity 에디터 윈도우 기능:
- **Table View / Form View** 데이터 편집
- **실시간 변경 추적** 및 저장/되돌리기
- **컬렉션 에디터** (List, Dictionary, 배열)
- **DataRef 선택기** (드롭다운 피커)
- **로컬라이제이션 패널** (다국어 편집)

열기: `Window > Datra > Data Editor`

## 프로젝트 구조

```
Datra/
├── Datra/                  # 핵심 런타임 라이브러리
├── Datra.Generators/       # Source Generator (컴파일 타임)
├── Datra.Analyzers/        # Roslyn Analyzer
├── Datra.Editor/           # 공유 에디터 유틸리티
├── Datra.Unity/            # Unity 패키지 (Runtime, Editor, Addressables)
├── Datra.Tests/            # 유닛 테스트
└── Datra.SampleData/       # 샘플 데이터 모델
```

## 문서

- **[기능 가이드](docs/FEATURES.md)** - 상세 기능 문서
- **[Unity 가이드](docs/UNITY.md)** - Unity 통합 가이드
- **[개발자 가이드](CLAUDE.md)** - 내부 개발 가이드

## 라이선스

MIT License - [LICENSE](LICENSE) 참조

## 링크

- [GitHub 저장소](https://github.com/penspanic/Datra)
- [이슈](https://github.com/penspanic/Datra/issues)
