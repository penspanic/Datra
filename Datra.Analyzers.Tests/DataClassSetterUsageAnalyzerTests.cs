using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    Datra.Analyzers.DataClassSetterUsageAnalyzer>;

namespace Datra.Analyzers.Tests
{
    public class DataClassSetterUsageAnalyzerTests
    {
        [Fact]
        public async Task NoDiagnostic_WhenNoSetterUsage()
        {
            var test = @"
namespace Datra.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TableDataAttribute : System.Attribute
    {
        public TableDataAttribute(string path) { }
    }
}

namespace Datra.Interfaces
{
    public interface ITableData<T>
    {
        T Id { get; }
    }
}

namespace TestNamespace
{
    using Datra.Attributes;
    using Datra.Interfaces;

    [TableData(""Items.json"")]
    public partial class ItemData : ITableData<int>
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class TestClass
    {
        public void TestMethod()
        {
            var item = new ItemData();
            var id = item.Id; // Reading is fine
            var name = item.Name; // Reading is fine
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task Diagnostic_WhenSettingTableDataProperty()
        {
            var test = @"
namespace Datra.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TableDataAttribute : System.Attribute
    {
        public TableDataAttribute(string path) { }
    }
}

namespace Datra.Interfaces
{
    public interface ITableData<T>
    {
        T Id { get; set; }
    }
}

namespace TestNamespace
{
    using Datra.Attributes;
    using Datra.Interfaces;

    [TableData(""Items.json"")]
    public partial class ItemData : ITableData<int>
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class TestClass
    {
        public void TestMethod()
        {
            var item = new ItemData();
            {|#0:item.Id|} = 123; // Error: setting property
            {|#1:item.Name|} = ""Test""; // Error: setting property
        }
    }
}";

            var expected1 = VerifyCS.Diagnostic(DataClassSetterUsageAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("Id", "ItemData");
            
            var expected2 = VerifyCS.Diagnostic(DataClassSetterUsageAnalyzer.DiagnosticId)
                .WithLocation(1)
                .WithArguments("Name", "ItemData");

            await VerifyCS.VerifyAnalyzerAsync(test, expected1, expected2);
        }

        [Fact]
        public async Task Diagnostic_WhenSettingSingleDataProperty()
        {
            var test = @"
namespace Datra.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class SingleDataAttribute : System.Attribute
    {
        public SingleDataAttribute(string path) { }
    }
}

namespace TestNamespace
{
    using Datra.Attributes;

    [SingleData(""GameConfig.yaml"")]
    public partial class GameConfig
    {
        public int MaxLevel { get; set; }
        public float ExpMultiplier { get; set; }
    }

    public class TestClass
    {
        public void TestMethod()
        {
            var config = new GameConfig();
            {|#0:config.MaxLevel|} = 100; // Error: setting property
            {|#1:config.ExpMultiplier|} = 1.5f; // Error: setting property
        }
    }
}";

            var expected1 = VerifyCS.Diagnostic(DataClassSetterUsageAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("MaxLevel", "GameConfig");
            
            var expected2 = VerifyCS.Diagnostic(DataClassSetterUsageAnalyzer.DiagnosticId)
                .WithLocation(1)
                .WithArguments("ExpMultiplier", "GameConfig");

            await VerifyCS.VerifyAnalyzerAsync(test, expected1, expected2);
        }

        [Fact]
        public async Task NoDiagnostic_WhenSettingInConstructor()
        {
            var test = @"
namespace Datra.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TableDataAttribute : System.Attribute
    {
        public TableDataAttribute(string path) { }
    }
}

namespace Datra.Interfaces
{
    public interface ITableData<T>
    {
        T Id { get; set; }
    }
}

namespace TestNamespace
{
    using Datra.Attributes;
    using Datra.Interfaces;

    [TableData(""Items.json"")]
    public partial class ItemData : ITableData<int>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ItemData()
        {
            Id = 1; // OK in constructor
            Name = ""Default""; // OK in constructor
        }

        public ItemData(int id, string name)
        {
            Id = id; // OK in constructor
            Name = name; // OK in constructor
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task NoDiagnostic_WhenSettingNonDataClassProperty()
        {
            var test = @"
namespace TestNamespace
{
    public class RegularClass
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class TestClass
    {
        public void TestMethod()
        {
            var obj = new RegularClass();
            obj.Id = 123; // OK: not a data class
            obj.Name = ""Test""; // OK: not a data class
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task Diagnostic_WhenUsingCompoundAssignment()
        {
            var test = @"
namespace Datra.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TableDataAttribute : System.Attribute
    {
        public TableDataAttribute(string path) { }
    }
}

namespace TestNamespace
{
    using Datra.Attributes;

    [TableData(""Items.json"")]
    public partial class ItemData
    {
        public int Count { get; set; }
        public int Value { get; set; }
    }

    public class TestClass
    {
        public void TestMethod()
        {
            var item = new ItemData();
            {|#0:item.Count|} += 5; // Error: compound assignment
            {|#1:item.Value|} *= 2; // Error: compound assignment
        }
    }
}";

            var expected1 = VerifyCS.Diagnostic(DataClassSetterUsageAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("Count", "ItemData");
            
            var expected2 = VerifyCS.Diagnostic(DataClassSetterUsageAnalyzer.DiagnosticId)
                .WithLocation(1)
                .WithArguments("Value", "ItemData");

            await VerifyCS.VerifyAnalyzerAsync(test, expected1, expected2);
        }

        [Fact]
        public async Task Diagnostic_WhenImplementsITableDataWithoutAttribute()
        {
            var test = @"
namespace Datra.Interfaces
{
    public interface ITableData<T>
    {
        T Id { get; set; }
    }
}

namespace TestNamespace
{
    using Datra.Interfaces;

    public partial class ItemData : ITableData<string>
    {
        public string Id { get; set; }
        public string Description { get; set; }
    }

    public class TestClass
    {
        public void TestMethod()
        {
            var item = new ItemData();
            {|#0:item.Id|} = ""123""; // Error: implements ITableData
            {|#1:item.Description|} = ""Test""; // Error: implements ITableData
        }
    }
}";

            var expected1 = VerifyCS.Diagnostic(DataClassSetterUsageAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("Id", "ItemData");
            
            var expected2 = VerifyCS.Diagnostic(DataClassSetterUsageAnalyzer.DiagnosticId)
                .WithLocation(1)
                .WithArguments("Description", "ItemData");

            await VerifyCS.VerifyAnalyzerAsync(test, expected1, expected2);
        }
    }
}