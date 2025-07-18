# Datra - Game Data Management System

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET%20Standard-2.1-blue.svg)](https://dotnet.microsoft.com/)
[![Unity](https://img.shields.io/badge/Unity-2020.3+-black.svg)](https://unity.com/)

Datra is a comprehensive data management system for game development that supports multiple data formats (CSV, JSON, YAML) and provides automatic code generation through C# Source Generators. It's designed to work seamlessly in both Unity and standard .NET environments.

## 🚀 Features

- **Multiple Data Format Support**: CSV, JSON, and YAML file formats
- **Automatic Code Generation**: Uses C# Source Generators to eliminate boilerplate code
- **Type Safety**: Strong typing with compile-time validation
- **Platform Independent**: Works in Unity and standard .NET applications
- **Async/Await Support**: All I/O operations are asynchronous
- **Repository Pattern**: Clean architecture with repository pattern implementation
- **Unity Package Support**: Can be imported as Unity packages

## 📦 Project Structure

```
Datra/
├── Datra.sln                     # Main solution file
├── Datra.Core/                   # Core library with data models
├── Datra.Data/                   # Data loading and repository system
├── Datra.Data.Generators/        # Source generators for automatic code generation
├── Datra.Test/                   # Test console application
└── Datra.Client/                 # Unity client project example
```

## 🛠️ Installation

### For .NET Projects

1. Clone the repository:
```bash
git clone https://github.com/yourusername/Datra.git
```

2. Add project references to your solution:
```xml
<ProjectReference Include="path/to/Datra.Core/Datra.Core.csproj" />
<ProjectReference Include="path/to/Datra.Data.Generators/Datra.Data.Generators.csproj" 
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
using Datra.Data;

[TableData("Characters.csv", Format = DataFormat.Csv)]
public partial class CharacterData : ITableData<string>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Level { get; set; }
    public float Health { get; set; }
    public float Mana { get; set; }
}

[SingleData("GameConfig.yaml", Format = DataFormat.Yaml)]
public partial class GameConfig
{
    public int MaxLevel { get; set; }
    public float ExpMultiplier { get; set; }
    public int StartingGold { get; set; }
}
```

### 2. Create Your Data Context

The Source Generator will automatically create a DataContext class based on your models:

```csharp
// This class is auto-generated
public partial class GameDataContext : IDataContext
{
    public IDataRepository<string, CharacterData> Character { get; }
    public ISingleDataRepository<GameConfig> GameConfig { get; }
    
    public async Task LoadAllAsync() { /* ... */ }
}
```

### 3. Load and Use Your Data

```csharp
// Create data provider and loader factory
var rawDataProvider = new FileRawDataProvider("path/to/data");
var loaderFactory = new DataLoaderFactory();

// Create context
var context = new GameDataContext(rawDataProvider, loaderFactory);

// Load all data
await context.LoadAllAsync();

// Use your data
var character = context.Character.GetById("hero_001");
var allCharacters = context.Character.GetAll();
var config = context.GameConfig.Get();
```

## 🎮 Unity Integration

Datra is designed to work seamlessly with Unity. The example Unity project (`Datra.Client`) demonstrates:

- Integration with Unity's package system
- Resource loading in Unity environment
- Usage in MonoBehaviours and ScriptableObjects
- Compatibility with Unity's async patterns

### Unity-Specific Features

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
2. **Code Generation** (`Datra.Data.Generators`): Automatic code generation using Roslyn
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

- Project Link: [https://github.com/yourusername/Datra](https://github.com/yourusername/Datra)
- Issues: [https://github.com/yourusername/Datra/issues](https://github.com/yourusername/Datra/issues)

---

Made with ❤️ for game developers who want type-safe, efficient data management.