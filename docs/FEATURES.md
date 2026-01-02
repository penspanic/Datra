# Datra Features Guide

This document covers all features available in Datra.

## Table of Contents

- [Data Attributes](#data-attributes)
- [Data Formats](#data-formats)
- [Data References](#data-references)
- [Nested Types](#nested-types)
- [Polymorphic JSON](#polymorphic-json)
- [Localization](#localization)
- [Asset Data](#asset-data)
- [Configuration](#configuration)

---

## Data Attributes

### TableData

For key-value table data with multiple entries (e.g., character database, item list).

```csharp
[TableData("Characters.csv", Format = DataFormat.Csv)]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }  // Key property
    public string Name { get; set; }
    public int Level { get; set; }
}
```

**Options:**
- `FilePath` - Path to data file (required)
- `Format` - `DataFormat.Auto`, `Csv`, `Json`, `Yaml` (default: Auto)
- `MultiFile` - Load multiple files as one table
- `Pattern` - File pattern for multi-file mode (e.g., `"*.json"`)
- `Label` - Addressables label for Unity Addressables

**Key Types:**
- `ITableData<string>` - String key
- `ITableData<int>` - Integer key

### SingleData

For single configuration objects (e.g., game settings).

```csharp
[SingleData("GameConfig.json")]
public partial class GameConfigData
{
    public string GameName { get; set; }
    public int MaxLevel { get; set; }
    public float ExpMultiplier { get; set; }
}
```

### AssetData

For file-based assets with stable GUIDs. Each file becomes one data entry.

```csharp
[AssetData("Scripts/", Pattern = "*.json")]
public partial class ScriptData : ITableData<string>
{
    public string Id { get; set; }
    public string Content { get; set; }
}
```

Asset data uses `.datrameta` companion files to maintain stable GUIDs even when files are renamed or moved.

---

## Data Formats

### CSV

Best for tabular data. Supports:
- Header row with property names
- Arrays with delimiter (default: `|`)
- Nested types with dot notation

```csv
Id,Name,Stats,ModelPrefab.Path
hero_001,Knight,Strength|Agility,Assets/Prefabs/Knight.prefab
```

### JSON

Best for complex nested structures.

```json
{
  "Id": "quest_001",
  "Title": "Dragon Slayer",
  "Rewards": [
    { "ItemId": 1001, "Count": 5 }
  ]
}
```

### YAML

Best for human-readable configuration.

```yaml
MaxLevel: 100
ExpMultiplier: 1.5
StartingGold: 1000
```

### Auto-Detection

Format is auto-detected from file extension:
- `.csv` → CSV
- `.json` → JSON
- `.yaml`, `.yml` → YAML

---

## Data References

Type-safe references between data tables.

### StringDataRef

Reference by string ID:

```csharp
[TableData("Quests.csv")]
public partial class QuestData : ITableData<string>
{
    public string Id { get; set; }
    public StringDataRef<CharacterData> QuestGiver { get; set; }
}

// Usage
var quest = context.Quest.GetById("quest_001");
var giver = quest.QuestGiver.Evaluate(context);  // Returns CharacterData
```

### IntDataRef

Reference by integer ID:

```csharp
public IntDataRef<ItemData> RewardItem { get; set; }
public IntDataRef<ItemData>[] BonusItems { get; set; }  // Array of references
```

### CSV Format

References are stored as IDs, arrays use `|` delimiter:

```csv
Id,QuestGiver,RewardItem,BonusItems
quest_001,npc_elder,1001,2001|2002|2003
```

---

## Nested Types

Embed struct or class types within data models.

### Definition

```csharp
public struct PooledPrefab
{
    public string Path { get; set; }
    public int InitialCount { get; set; }
    public int MaxCount { get; set; }
}

[TableData("Characters.csv")]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public PooledPrefab ModelPrefab { get; set; }  // Nested struct
}
```

### CSV Format

Nested properties use dot notation:

```csv
Id,Name,ModelPrefab.Path,ModelPrefab.InitialCount,ModelPrefab.MaxCount
hero_001,Knight,Assets/Prefabs/Knight.prefab,5,20
```

**Note:** Only one level of nesting is supported.

---

## Polymorphic JSON

Use abstract classes with multiple implementations. Datra automatically handles type discrimination.

### Definition

```csharp
public abstract class QuestObjective
{
    public string Id { get; set; }
    public string Description { get; set; }
}

public class KillObjective : QuestObjective
{
    public string TargetEnemyId { get; set; }
    public int RequiredCount { get; set; }
}

public class TalkObjective : QuestObjective
{
    public string TargetNpcId { get; set; }
}

[TableData("Quests.json")]
public partial class QuestData : ITableData<string>
{
    public string Id { get; set; }
    public List<QuestObjective> Objectives { get; set; }  // Polymorphic list
}
```

### JSON Format

The `$type` field stores the concrete type:

```json
{
  "Id": "quest_001",
  "Objectives": [
    {
      "$type": "MyGame.KillObjective",
      "Id": "obj_001",
      "TargetEnemyId": "enemy_slime",
      "RequiredCount": 5
    },
    {
      "$type": "MyGame.TalkObjective",
      "Id": "obj_002",
      "TargetNpcId": "npc_elder"
    }
  ]
}
```

---

## Localization

Built-in multi-language support.

### LocaleRef

Wrapper for localization keys:

```csharp
[TableData("Characters.csv")]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public LocaleRef Name { get; set; }        // Localized name
    public LocaleRef Description { get; set; } // Localized description
}

// Usage
var character = context.Character.GetById("hero_001");
string localizedName = character.Name.Evaluate(localizationContext);
```

### Configuration

Enable in `DatraConfiguration`:

```csharp
[assembly: DatraConfiguration("GameData",
    Namespace = "MyGame.Generated",
    EnableLocalization = true,
    LocalizationKeyDataPath = "Localizations/Keys.csv",
    LocalizationDataPath = "Localizations/",
    DefaultLanguage = "en"
)]
```

### Locale Attributes

#### FixedLocale

Auto-computed locale key (key is read-only, values are editable):

```csharp
[FixedLocale]
public LocaleRef Name { get; set; }
// Key pattern: {TypeName}.{Id}.Name
```

#### NestedLocale

For nested collections with hierarchical keys:

```csharp
[NestedLocale]
public List<DialogLine> Lines { get; set; }
// Key pattern: {TypeName}.{Id}.Lines.{Index}
```

---

## Asset Data

File-based assets with GUID stability.

### Definition

```csharp
[AssetData("Characters/", Pattern = "*.json")]
public partial class CharacterAsset : ITableData<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public CharacterStats Stats { get; set; }
}
```

### GUID Stability

Each asset file has a companion `.datrameta` file:

```
Characters/
├── hero_001.json
├── hero_001.json.datrameta    # Contains stable GUID
├── hero_002.json
└── hero_002.json.datrameta
```

GUIDs persist when files are renamed or moved.

### Repository API

```csharp
var asset = context.CharacterAsset.GetById("guid-here");
var asset = context.CharacterAsset.GetByPath("Characters/hero_001.json");
var assets = context.CharacterAsset.FindByTag("playable");
```

---

## Configuration

### DatraConfiguration Attribute

Required assembly-level configuration:

```csharp
[assembly: DatraConfiguration("GameData",
    // Required
    Namespace = "MyGame.Generated",

    // Optional
    EnableLocalization = true,
    LocalizationKeyDataPath = "Localizations/Keys.csv",
    LocalizationDataPath = "Localizations/",
    UseSingleFileLocalization = false,
    LocalizationKeyColumn = "Key",
    DefaultLanguage = "en",
    EnableDebugLogging = false,
    EmitPhysicalFiles = false  // Set true for debugging generated code
)]
```

### Multi-Context Support

Different assemblies can have independent contexts:

```csharp
// Assembly A
[assembly: DatraConfiguration("GameData", Namespace = "Game.Core")]

// Assembly B
[assembly: DatraConfiguration("ClientData", Namespace = "Game.Client")]
```

### Editor Attributes

#### DatraIgnore

Hide property from editor:

```csharp
[DatraIgnore]
public string InternalField { get; set; }
```

#### ReadOnlyInInspector

Show as read-only in Unity Inspector:

```csharp
[ReadOnlyInInspector]
public string ComputedValue { get; set; }
```

#### AssetType

Specify Unity asset type for asset picker:

```csharp
[AssetType(typeof(GameObject))]
public string PrefabPath { get; set; }
```

#### FolderPath

Restrict asset selection to folder:

```csharp
[FolderPath("Assets/Sprites/Characters", "*.png")]
public string SpritePath { get; set; }
```

---

## Supported Types

### Primitives
`string`, `int`, `float`, `double`, `bool`, `decimal`, `long`, `short`, `byte`, `char`

### Enums
Full support including arrays: `MyEnum[]`

### Collections
- `List<T>`
- `Dictionary<TKey, TValue>`
- Arrays: `T[]`

### Special Types
- `StringDataRef<T>`, `IntDataRef<T>`
- `LocaleRef`
- `Asset<T>`
- Nested structs/classes (one level)

### Not Supported
- Deeply nested types (nested within nested)
- Interfaces as data models
- Generic data models
- Cyclic references
