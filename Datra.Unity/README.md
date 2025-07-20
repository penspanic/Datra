# Datra Unity Integration

Unity-specific integration package for the Datra data management system.

## Features

- **Unity Editor Integration**: Visual editor for managing Datra data using UI Toolkit
- **Runtime Support**: Unity-specific extensions and utilities for working with Datra
- **DataRef Resolution**: Specialized resolver for handling data references in Unity
- **Type-Safe Data Editing**: Dynamic UI generation based on data types

## Installation

1. Ensure Datra core package is installed
2. Add this package to your Unity project via Package Manager

## Usage

### Initialization

```csharp
// Initialize Datra with your DataContext
var dataContext = new MyDataContext(rawDataProvider, loaderFactory);
DatraUnityInitializer.Initialize(dataContext);

// Load data
await dataContext.LoadAllAsyncUnity();
```

### Editor Window

Open the Datra Editor from the Unity menu: `Window > Datra > Data Editor`

### DataRef Resolution

```csharp
// Resolve data references
var resolver = DatraUnityInitializer.Resolver;
var actualData = resolver.Resolve(myDataRef);
```

## Package Structure

- `Runtime/`: Runtime components and extensions
  - `Extensions/`: Unity-specific extension methods
  - `Serialization/`: DataRef resolver for Unity
- `Editor/`: Editor-only components
  - `UI/`: UI Toolkit components for data editing
  - Main editor window and utilities

## Requirements

- Unity 2021.3 or later
- Datra core package
- UI Toolkit package