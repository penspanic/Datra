# Datra - Game Data Management System

[한국어](README.ko.md) | English

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET%20Standard-2.1-blue.svg)](https://dotnet.microsoft.com/)
[![Unity](https://img.shields.io/badge/Unity-2020.3+-black.svg)](https://unity.com/)

Datra is a comprehensive data management system for game development that supports multiple data formats (CSV, JSON, YAML) and provides automatic code generation through C# Source Generators. It's designed to work seamlessly in both Unity and standard .NET environments.

## 🚀 Features

- **Multiple Data Format Support**: CSV, JSON, and YAML file formats
- **Automatic Code Generation**: Uses C# Source Generators to eliminate boilerplate code
- **Type Safety**: Strong typing with compile-time validation
- **Polymorphic JSON Support**: Abstract classes and inheritance with automatic type handling
- **Collection Support**: List<T>, Dictionary<K,V>, and arrays with full editor support
- **Platform Independent**: Works in Unity and standard .NET applications
- **Async/Await Support**: All I/O operations are asynchronous
- **Repository Pattern**: Clean architecture with repository pattern implementation
- **Unity Editor Integration**: Visual data editing with Table/Form views and popup editors
- **Unity Package Support**: Can be imported as Unity packages

## 🎬 Unity Editor Demo

Datra provides a powerful Unity Editor window for managing and visualizing your game data directly within Unity:

<p align="center">
  <img src="docs/images/unity-editor-demo.gif" alt="Unity Editor Demo" width="100%">
</p>

The editor window features:
- **Real-time data visualization and editing** with Table View and Form View
- **Support for multiple data formats** (CSV, JSON, YAML)
- **Collection editing** with popup editors for List<T> and Dictionary<K,V>
- **Polymorphic type support** with automatic derived type discovery
- **Collapsible elements** in collection editors for better organization
- **Reorder support** with up/down buttons for list elements
- **Type-safe data management** with compile-time validation
- **Change tracking** with modified indicators and revert functionality

## 🔥 Key Features & Examples

### 📋 Basic Data Models

Define your game data with simple attributes:

```csharp
using Datra.Attributes;
using Datra.Interfaces;

// Table data for multiple entries (e.g., character database)
[TableData("Characters.csv", Format = DataFormat.Csv)]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
    public int Health { get; set; }
    public int Mana { get; set; }
}

// Single data for configuration (e.g., game settings)
[SingleData("GameConfig.json", Format = DataFormat.Json)]
public partial class GameConfigData
{
    public string GameName { get; set; }
    public int MaxLevel { get; set; }
    public float ExpMultiplier { get; set; }
}
```

### 🔗 Data References with DataRef<>

Reference other data tables with type-safe DataRef<> properties. DataRef stores the ID of the referenced data and resolves it using the context:

```csharp
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;

[TableData("RefTestDataList.csv", Format = DataFormat.Csv)]
public partial class RefTestData : ITableData<string>
{
    public string Id { get; set; }
    
    // Reference to character by string ID
    public StringDataRef<CharacterData> CharacterRef { get; set; }
    
    // Reference to item by integer ID
    public IntDataRef<ItemData> ItemRef { get; set; }
    
    // Array of item references
    public IntDataRef<ItemData>[] ItemRefs { get; set; }
}

// Usage example
var refData = context.RefTestData.GetById("test_001");
var character = refData.CharacterRef.Evaluate(context); // Resolve reference with context
var item = refData.ItemRef.Evaluate(context);           // Type-safe resolution
```

In CSV files, references are stored as IDs and arrays use pipe (|) separators:
```csv
Id,CharacterRef,ItemRef,ItemRefs
test_01,hero_011,1001,1001|1002|1003
test_02,hero_002,1002,2001|2002
```

Note: DataRef<> stores only the ID value. Use the `Evaluate(context)` method to retrieve the actual referenced data.

### 🎯 Enum Support

Use enums for better type safety and readability:

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
    public CharacterGrade Grade { get; set; }  // Enum property
    public StatType[] Stats { get; set; }      // Array of enums
}
```

### 📚 Array Support

Store multiple values with array properties:

```csharp
[SingleData("GameConfig.json", Format = DataFormat.Json)]
public partial class GameConfigData
{
    public GameMode[] AvailableModes { get; set; }
    public RewardType[] EnabledRewards { get; set; }
    
    // Array of data references
    public StringDataRef<CharacterData>[] UnlockableCharacters { get; set; }
    public IntDataRef<ItemData>[] StartingItems { get; set; }
}

[TableData("Characters.csv", Format = DataFormat.Csv)]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public int[] UpgradeCosts { get; set; }  // Array of integers
    public StatType[] Stats { get; set; }    // Array of enums
}
```

### 🧬 Polymorphic JSON Support

Use abstract classes or interfaces with multiple implementations. Datra automatically handles type discrimination in JSON:

```csharp
// Base abstract class for quest objectives
public abstract class QuestObjective
{
    public string Id { get; set; }
    public string Description { get; set; }
    public bool IsCompleted { get; set; }
}

// Concrete implementations
public class KillObjective : QuestObjective
{
    public string TargetEnemyId { get; set; }
    public int RequiredCount { get; set; }
    public int CurrentCount { get; set; }
}

public class TalkObjective : QuestObjective
{
    public string TargetNpcId { get; set; }
    public List<string> DialogueKeys { get; set; }
}

public class CollectObjective : QuestObjective
{
    public int TargetItemId { get; set; }
    public int RequiredAmount { get; set; }
}

// Use polymorphic list in your data model
[TableData("Quests.json", Format = DataFormat.Json)]
public partial class QuestData : ITableData<string>
{
    public string Id { get; set; }
    public List<QuestObjective> Objectives { get; set; }  // Polymorphic list
    public Dictionary<string, int> RewardItems { get; set; }
}
```

In JSON files, the `$type` field stores the concrete type:
```json
{
  "Id": "quest_main_001",
  "Objectives": [
    {
      "$type": "MyGame.Models.TalkObjective",
      "Id": "obj_001",
      "Description": "Talk to the village elder",
      "TargetNpcId": "npc_elder_001",
      "DialogueKeys": ["dialogue_intro_001", "dialogue_intro_002"]
    },
    {
      "$type": "MyGame.Models.KillObjective",
      "Id": "obj_002",
      "Description": "Defeat the slimes",
      "TargetEnemyId": "enemy_slime",
      "RequiredCount": 5
    }
  ]
}
```

The Unity Editor automatically discovers derived types using `TypeCache` and provides a dropdown for adding new elements:

<p align="center">
  <img src="docs/images/polymorphic-editor.png" alt="Polymorphic Type Editor" width="400">
</p>

### 🏠 Nested Type Support

Embed struct or class types within your data models. Nested types are serialized using dot notation in CSV:

```csharp
// Define a nested type
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
    public PooledPrefab ModelPrefab { get; set; }  // Nested struct
}
```

In CSV files, nested properties use dot notation for column headers:
```csv
Id,Name,ModelPrefab.Path,ModelPrefab.InitialCount,ModelPrefab.MaxCount
hero_001,Knight,Assets/Prefabs/Knight.prefab,5,20
hero_002,Mage,Assets/Prefabs/Mage.prefab,3,15
```

Note: Nested types support one level of nesting. Deeply nested types (nested within nested) are not supported.

### 🎨 Complex Data Models

Combine all features for rich data structures:

```csharp
[TableData("Items.json")]  // Format auto-detected from extension
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
    
    // Enum properties
    public GameMode DefaultMode { get; set; }
    public GameMode[] AvailableModes { get; set; }
    
    // Data references
    public StringDataRef<CharacterData> DefaultCharacter { get; set; }
    public IntDataRef<ItemData> StartingItem { get; set; }
    
    // Arrays of references
    public StringDataRef<CharacterData>[] UnlockableCharacters { get; set; }
    public IntDataRef<ItemData>[] StartingItems { get; set; }
}

// Usage: Resolving references
var config = context.GameConfig.Get();
var defaultCharacter = config.DefaultCharacter.Evaluate(context);
var startingItem = config.StartingItem.Evaluate(context);

// Resolving array references
foreach (var charRef in config.UnlockableCharacters)
{
    var character = charRef.Evaluate(context);
    Console.WriteLine($"Unlockable: {character?.Name}");
}
```

## 📦 Project Structure

```
Datra/
├── Datra.sln                     # Main solution file
├── Datra/                        # Data loading and repository system
├── Datra.Generators/        # Source generators for automatic code 
├── Datra.Analyzers/        # Source analyzers for user code
generation
├── Datra.Tests/                   # Unit test project
└── Datra.Client/                 # Unity client project example
```

## 🛠️ Installation

### For .NET Projects

1. Clone the repository:
```bash
git clone https://github.com/penspanic/Datra.git
```

2. Add project references to your solution:
```xml
<ProjectReference Include="path/to/Datra.Core/Datra.Core.csproj" />
<ProjectReference Include="path/to/Datra.Generators/Datra.Generators.csproj" 
                  OutputItemType="Analyzer" 
                  ReferenceOutputAssembly="false" />
```

### For Unity Projects

1. In Unity Package Manager, click the "+" button and select "Add package from git URL..."

2. Enter the following URL:
```
https://github.com/penspanic/Datra.git?path=Datra.Data
```

3. Unity will automatically import the package along with its dependencies.

Alternatively, you can add it directly to your Unity project's `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.datra.data": "https://github.com/penspanic/Datra.git?path=Datra.Data"
  }
}
```

## 📝 Usage

### 1. Define Your Data Models

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
    public CharacterGrade Grade { get; set; }  // Enum support
}

[SingleData("GameConfig.yaml", Format = DataFormat.Yaml)]
public partial class GameConfig
{
    public int MaxLevel { get; set; }
    public float ExpMultiplier { get; set; }
    public int StartingGold { get; set; }
    public StringDataRef<CharacterData> DefaultCharacter { get; set; }  // Reference to character
}
```

### 2. Configure Your Data Context

Add the `DatraConfiguration` attribute to your assembly to configure the generated context:

```csharp
using Datra.Attributes;

// In your AssemblyInfo.cs or any .cs file in your project
[assembly: DatraConfiguration("GameData",
    Namespace = "MyGame.Generated",           // Required: explicit namespace for generated code
    EnableLocalization = true,                // Optional: enable localization support
    LocalizationKeyDataPath = "Localizations/LocalizationKeys.csv",
    EmitPhysicalFiles = false                 // Optional: set to true for debugging generated code
)]
```

**Note**: The `Namespace` property is **required**. This ensures consistent namespace behavior across Unity and .NET environments. Without it, you'll get compile error `DATRA003`.

### 3. Create Your Data Context

The Source Generator will automatically create a DataContext class based on your models. The generated `GameDataContext` class is placed in the configured namespace (or `{AssemblyName}.Generated` by default):

```csharp
// This class is auto-generated in Datra.Generated namespace
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

### 4. Load and Use Your Data

```csharp
using Datra.Generated;

// Create data provider and loader factory
var rawDataProvider = new FileRawDataProvider("path/to/data");
var loaderFactory = new DataLoaderFactory();

// Create context (GameDataContext is in Datra.Generated namespace)
var context = new GameDataContext(rawDataProvider, loaderFactory);

// Load all data
await context.LoadAllAsync();

// Use your data
var character = context.Character.GetById("hero_001");
var allCharacters = context.Character.GetAll();
var config = context.GameConfig.Get();

// Using data references
var refData = context.RefTestData.GetById("test_001");
var referencedCharacter = refData.CharacterRef.Evaluate(context);  // Resolve with context
var referencedItem = refData.ItemRef.Evaluate(context);

// Working with arrays
foreach (var itemRef in refData.ItemRefs)
{
    var item = itemRef.Evaluate(context);  // Each reference is resolved
    Console.WriteLine($"Item: {item.Name}, Price: {item.Price}");
}
```

## 🎮 Unity Integration

Datra is designed to work seamlessly with Unity. The example Unity project (`Datra.Client`) demonstrates:

- Integration with Unity's package system
- Resource loading in Unity environment
- Usage in MonoBehaviours and ScriptableObjects
- Compatibility with Unity's async patterns
- **Custom Unity Editor Window** for visual data management (as shown in the demo above)

### Unity-Specific Features

- **Custom Editor Window** for intuitive data management
- **Table View & Form View** for different editing workflows
- **Collection Editors** with popup windows for complex types:
  - List<T> editing with add/remove/reorder
  - Dictionary<K,V> editing with key-value pairs
  - Polymorphic type selection dropdown
  - Collapsible elements for large collections
- **Asset Reference Fields** with drag-and-drop support
- **Localization Integration** with LocaleRef editing
- Conditional compilation for Unity-specific code paths
- Unity package manifest support
- Compatible with Unity 2020.3 and later

## 📊 Supported Data Formats

### CSV Files
Best for tabular data like character stats, item properties, etc.

```csv
Id,Name,Level,Health,Mana
hero_001,Knight,10,150,50
hero_002,Mage,8,80,200
```

### JSON Files
Ideal for complex nested structures and arrays.

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

### YAML Files
Perfect for configuration files with good readability.

```yaml
MaxLevel: 100
ExpMultiplier: 1.5
StartingGold: 1000
```

## 🏗️ Architecture

Datra uses a clean architecture approach:

1. **Data Layer** (`Datra.Data`): Handles data loading and repository pattern
2. **Code Generation** (`Datra.Generators`): Automatic code generation using Roslyn
3. **Models** (Your project): Define your data structures with attributes
4. **Context** (Generated): Auto-generated context for data access

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Inspired by Entity Framework's DbContext pattern
- Uses Roslyn Source Generators for code generation
- Built with love for game developers

## 📞 Contact

- Project Link: [https://github.com/penspanic/Datra](https://github.com/penspanic/Datra)
- Issues: [https://github.com/penspanic/Datra/issues](https://github.com/penspanic/Datra/issues)

---

Made with ❤️ for game developers who want type-safe, efficient data management.
