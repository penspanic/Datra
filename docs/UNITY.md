# Unity Integration Guide

This guide covers Datra integration with Unity projects.

## Table of Contents

- [Installation](#installation)
- [Package Structure](#package-structure)
- [Data Editor Window](#data-editor-window)
- [Runtime Data Loading](#runtime-data-loading)
- [Addressables Support](#addressables-support)
- [Editor Customization](#editor-customization)

---

## Installation

### Via Package Manager

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

### Optional: Addressables

For Addressables support:

```json
{
  "dependencies": {
    "com.penspanic.datra.addressables": "https://github.com/penspanic/Datra.git?path=Datra.Unity/Addressables"
  }
}
```

---

## Package Structure

```
Datra.Unity/
├── Runtime/                    # Runtime package
│   ├── ResourcesRawDataProvider.cs    # Load from Resources/
│   ├── UnityDataRefResolver.cs        # DataRef resolution
│   └── UnitySerializationLogger.cs    # Console logging
│
├── Editor/                     # Editor package
│   ├── DatraEditorWindow.cs           # Main editor window
│   ├── DatraDataManager.cs            # Data management
│   ├── Panels/                        # UI panels
│   ├── Views/                         # Table/Form views
│   └── Services/                      # Editor services
│
└── Addressables/               # Addressables package
    └── AddressableRawDataProvider.cs  # Load from Addressables
```

---

## Data Editor Window

Open via: **Window > Datra > Data Editor**

### Features

- **Navigation Panel**: Browse all data types
- **Table View**: Grid display for quick overview
- **Form View**: Detailed property editing
- **Change Tracking**: Visual indicators for modified data
- **Save/Reload**: Per-type or all data

### Table View

![Table View](images/unity-editor-demo.gif)

- Click row to select item
- Double-click to switch to Form View
- Column headers show property names
- Nested types expand to multiple columns

### Form View

- Full property editing
- Collection editors (List, Dictionary)
- DataRef dropdowns
- Asset pickers with folder constraints

### Localization Panel

Switch to localization mode to edit translations:

- Language selector
- Key-value editor
- Multi-language preview

---

## Runtime Data Loading

### From Resources Folder

Place data files in `Assets/Resources/Data/`:

```csharp
using Datra.Unity;

public class GameManager : MonoBehaviour
{
    private GameDataContext _context;

    async void Start()
    {
        var provider = new ResourcesRawDataProvider("Data");
        _context = new GameDataContext(provider, new DataLoaderFactory());

        await _context.LoadAllAsync();

        var hero = _context.Character.GetById("hero_001");
        Debug.Log($"Loaded: {hero.Name}");
    }
}
```

### From StreamingAssets

```csharp
var path = Path.Combine(Application.streamingAssetsPath, "Data");
var provider = new FileRawDataProvider(path);
```

### Selective Loading

Load specific data types:

```csharp
await _context.Character.LoadAsync();  // Load only characters
await _context.ReloadAsync("Character");  // Reload specific type
```

---

## Addressables Support

### Setup

1. Install Addressables package
2. Add `com.penspanic.datra.addressables` package
3. Mark data files as Addressables with labels

### Configuration

```csharp
[TableData("Characters.json", Label = "gamedata")]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
}
```

### Loading

```csharp
var provider = new AddressableRawDataProvider();
var context = new GameDataContext(provider, new DataLoaderFactory());

await context.LoadAllAsync();
```

### Multi-File with Addressables

```csharp
[TableData("Characters/", MultiFile = true, Pattern = "*.json", Label = "characters")]
public partial class CharacterData : ITableData<string>
{
    // Each file becomes one entry
}
```

---

## Editor Customization

### Asset Type Attributes

Specify Unity asset types for property fields:

```csharp
[AssetType(typeof(GameObject))]
public string PrefabPath { get; set; }

[AssetType(typeof(Sprite))]
public string IconPath { get; set; }

[AssetType(typeof(AudioClip))]
public string SoundPath { get; set; }
```

### Folder Path Constraints

Restrict asset selection to specific folders:

```csharp
[FolderPath("Assets/Sprites/Characters")]
public string SpritePath { get; set; }

[FolderPath("Assets/Prefabs", "*.prefab")]
public string PrefabPath { get; set; }
```

### Hide Properties

```csharp
[DatraIgnore]
public string InternalId { get; set; }  // Hidden in editor

[ReadOnlyInInspector]
public int ComputedValue { get; set; }  // Read-only in editor
```

---

## Architecture

### MVVM Pattern

The editor uses MVVM for testability:

```
DatraEditorWindow (View)
    │
    └── DatraEditorViewModel (ViewModel)
        ├── IDataService
        ├── IChangeTrackingService
        └── ILocalizationEditorService
```

### Services

| Service | Purpose |
|---------|---------|
| `IDataService` | Data load/save operations |
| `IChangeTrackingService` | File modification tracking |
| `ILocalizationEditorService` | Language editing |
| `IEditableDataSource<K,V>` | Transactional editing |

### ViewModel Commands

```csharp
// Access from window
var window = EditorWindow.GetWindow<DatraEditorWindow>();

// Select data type
window.ViewModel.SelectDataTypeCommand(typeof(CharacterData));

// Save operations
await window.ViewModel.SaveCommand();
await window.ViewModel.SaveAllCommand();

// Check state
bool hasChanges = window.ViewModel.HasAnyUnsavedChanges;
```

---

## Troubleshooting

### Generator Not Working

1. Ensure `DatraConfiguration` attribute is set
2. Check `Namespace` property is provided (required)
3. Rebuild: `./Scripts/build-all.sh`

### Unity Compilation Errors

Common issues:

| Error | Cause | Solution |
|-------|-------|----------|
| CS0116 | Reserved keyword as property name | Generator adds `@` prefix automatically |
| CS0101 | Duplicate definition | Delete `*.g.cs` files, set `EmitPhysicalFiles = false` |
| DATRA003 | Missing namespace | Add `Namespace = "..."` to configuration |

### Editor Window Issues

- **Window empty**: Check data files exist at configured paths
- **Changes not saving**: Check file permissions
- **Type not appearing**: Ensure `[TableData]` or `[SingleData]` attribute is applied

---

## Best Practices

### Data Organization

```
Assets/
├── Resources/
│   └── Data/
│       ├── Characters.csv
│       ├── Items.json
│       └── Config.yaml
│
└── Scripts/
    └── Data/
        ├── Models/
        │   ├── CharacterData.cs
        │   └── ItemData.cs
        └── DatraConfiguration.cs
```

### Configuration File

Create a dedicated configuration file:

```csharp
// Assets/Scripts/Data/DatraConfiguration.cs
using Datra.Attributes;

[assembly: DatraConfiguration("GameData",
    Namespace = "MyGame.Data.Generated",
    EnableLocalization = true,
    LocalizationKeyDataPath = "Data/Localizations/Keys.csv",
    DefaultLanguage = "en"
)]
```

### Version Control

Add to `.gitignore`:
```
# Generated files (if EmitPhysicalFiles = true)
*.g.cs

# Meta files for generated content
*.datrameta
```

---

## Requirements

- Unity 2020.3 or later
- .NET Standard 2.1
- UI Toolkit (included in Unity 2020.3+)
