# Datra

[한국어](README.ko.md) | English

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET%20Standard-2.1-blue.svg)](https://dotnet.microsoft.com/)
[![Unity](https://img.shields.io/badge/Unity-2020.3+-black.svg)](https://unity.com/)

**Datra** is a game data management system that uses C# Source Generators to automatically generate serialization code for CSV, JSON, and YAML data. Works seamlessly in both Unity and .NET environments.

## Features

- **Multiple Formats**: CSV, JSON, YAML with auto-detection
- **Zero Boilerplate**: Source Generators eliminate manual serialization code
- **Type Safety**: Compile-time validation with strong typing
- **Data References**: Type-safe `DataRef<T>` for cross-table references
- **Unity Integration**: Built-in Editor window with Table/Form views
- **Localization**: Multi-language support with `LocaleRef`
- **Advanced Types**: Nested structs, polymorphic JSON, arrays, enums

## Quick Start

### 1. Define Your Data Model

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

### 2. Configure the Data Context

```csharp
[assembly: DatraConfiguration("GameData",
    Namespace = "MyGame.Generated"  // Required
)]
```

### 3. Load and Use Data

```csharp
var provider = new FileRawDataProvider("path/to/data");
var context = new GameDataContext(provider, new DataLoaderFactory());

await context.LoadAllAsync();

var hero = context.Character.GetById("hero_001");
var config = context.GameConfig.Get();
```

## Installation

### Unity

Add to `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.penspanic.datra": "https://github.com/penspanic/Datra.git?path=Datra",
    "com.penspanic.datra.editor": "https://github.com/penspanic/Datra.git?path=Datra.Editor",
    "com.penspanic.datra.unity": "https://github.com/penspanic/Datra.git?path=Datra.Unity"
  }
}
```

### .NET

Add project references:
```xml
<ProjectReference Include="path/to/Datra/Datra.csproj" />
<ProjectReference Include="path/to/Datra.Generators/Datra.Generators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

### Trimming (Blazor WebAssembly, self-contained .NET)

`Datra` is marked `IsTrimmable`, so an application that publishes trimmed and
never calls Datra has it removed entirely. Nothing to configure.

`YamlDotNet`, which Datra depends on, carries no such marking, so the linker
copies it whole and it stays on the wire even with no references left pointing
at it. An application that reads no YAML can say so:

```xml
<PropertyGroup>
  <DatraTrimYaml>true</DatraTrimYaml>
</PropertyGroup>
```

Off by default, and it has to be: Datra's YAML path is reflection-driven, so a
trimmed build that *does* read YAML fails rather than running slow. JSON and CSV
are unaffected either way.

Measured on a Blazor WebAssembly game that reads only JSON: Datra (347 KB) and
YamlDotNet (293 KB) leave the output, and with AOT the native blob drops a
further 547 KB brotli.

The source generator's `DatraEmitYamlSerializers` property is also reachable
from a consuming project as of 0.3.5 — before that the package shipped no
`build/` props declaring it compiler-visible, so setting it did nothing.

## Unity Editor

<p align="center">
  <img src="docs/images/unity-editor-demo.gif" alt="Unity Editor Demo" width="100%">
</p>

The Unity Editor window provides:
- **Table View / Form View** for data editing
- **Real-time change tracking** with save/revert
- **Collection editors** for List, Dictionary, arrays
- **DataRef selectors** with dropdown pickers
- **Localization panel** for multi-language editing

Open via: `Window > Datra > Data Editor`

## Project Structure

```
Datra/
├── Datra/                  # Core runtime library
├── Datra.Generators/       # Source Generator (compile-time)
├── Datra.Analyzers/        # Roslyn Analyzers
├── Datra.Editor/           # Shared editor utilities
├── Datra.Unity/            # Unity packages (Runtime, Editor, Addressables)
├── Datra.Tests/            # Unit tests
└── Datra.SampleData/       # Sample data models
```

## Documentation

- **[Features Guide](docs/FEATURES.md)** - Detailed feature documentation
- **[Unity Guide](docs/UNITY.md)** - Unity integration guide
- **[Developer Guide](CLAUDE.md)** - Internal development guide

## License

MIT License - see [LICENSE](LICENSE)

## Links

- [GitHub Repository](https://github.com/penspanic/Datra)
- [Issues](https://github.com/penspanic/Datra/issues)
