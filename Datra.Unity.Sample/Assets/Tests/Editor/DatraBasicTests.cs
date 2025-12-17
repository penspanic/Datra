using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Datra.SampleData.Generated;
using Datra.SampleData.Models;
using Datra.Serializers;
using Datra.Unity.Runtime.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Basic tests for Datra integration in Unity.
    /// These tests verify that the generated code compiles and works correctly.
    /// </summary>
    public class DatraBasicTests
    {
        private string _testDataPath;

        [SetUp]
        public void SetUp()
        {
            // Find the Data folder in Assets
            _testDataPath = Path.Combine(Application.dataPath, "Data");
        }

        [Test]
        public void DataContext_CanBeCreated()
        {
            // Arrange & Act
            var provider = new ResourcesRawDataProvider("Data");
            var context = new GameDataContext(provider);

            // Assert
            Assert.IsNotNull(context);
            Assert.IsNotNull(context.Character);
            Assert.IsNotNull(context.Item);
        }

        [Test]
        public void CharacterData_TypeIsGenerated()
        {
            // Verify that the CharacterData type exists and has expected properties
            var type = typeof(CharacterData);

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetProperty("Id"));
            Assert.IsNotNull(type.GetProperty("Level"));
            Assert.IsNotNull(type.GetProperty("Health"));
        }

        [Test]
        public void CharacterData_HasNestedTypeProperty()
        {
            // Verify nested type support (PooledPrefab)
            var type = typeof(CharacterData);
            var testPooledPrefabProp = type.GetProperty("TestPooledPrefab");

            Assert.IsNotNull(testPooledPrefabProp, "TestPooledPrefab property should exist");
            Assert.AreEqual(typeof(PooledPrefab), testPooledPrefabProp.PropertyType);
        }

        [Test]
        public void PooledPrefab_HasExpectedProperties()
        {
            // Verify PooledPrefab struct has expected properties
            var type = typeof(PooledPrefab);

            Assert.IsNotNull(type.GetProperty("Path"), "Path property should exist");
            Assert.IsNotNull(type.GetProperty("InitialCount"), "InitialCount property should exist");
            Assert.IsNotNull(type.GetProperty("MaxCount"), "MaxCount property should exist");
        }

        [UnityTest]
        public IEnumerator DataContext_CanLoadData()
        {
            // Arrange
            var provider = new ResourcesRawDataProvider("Data");
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            // Assert - check if data was loaded
            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.Message}");
            }

            var characters = context.Character.GetAll();
            Assert.IsNotNull(characters);
            Assert.Greater(characters.Count, 0, "Should have loaded at least one character");
        }

        [UnityTest]
        public IEnumerator CharacterData_NestedType_IsDeserialized()
        {
            // Arrange
            var provider = new ResourcesRawDataProvider("Data");
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.Message}");
            }

            // Assert - verify nested type is properly deserialized
            var characters = context.Character.GetAll();
            Assert.Greater(characters.Count, 0);

            var firstChar = characters.Values.GetEnumerator();
            firstChar.MoveNext();
            var character = firstChar.Current;

            // TestPooledPrefab should be initialized (not null/default for struct)
            Assert.IsNotNull(character.TestPooledPrefab.Path,
                "Nested type Path should be deserialized");
        }

        [Test]
        public void CsvSerializer_IsGenerated()
        {
            // Verify that the serializer class was generated
            var serializerType = typeof(CharacterDataSerializer);

            Assert.IsNotNull(serializerType);

            // Check for expected methods
            var deserializeMethod = serializerType.GetMethod("DeserializeCsv");
            var serializeMethod = serializerType.GetMethod("SerializeCsv");

            Assert.IsNotNull(deserializeMethod, "DeserializeCsv method should exist");
            Assert.IsNotNull(serializeMethod, "SerializeCsv method should exist");
        }
    }
}
