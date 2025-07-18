using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<
    Datra.Data.Analyzers.DataClassSetterAnalyzer>;

namespace Datra.Data.Analyzers.Tests
{
    public class DataClassSetterAnalyzerTests
    {
        [Fact]
        public async Task NoDiagnostic_WhenPropertyHasNoSetter()
        {
            var test = @"
namespace Datra.Data.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TableDataAttribute : System.Attribute
    {
        public TableDataAttribute(string path) { }
    }
}

namespace Datra.Data.Interfaces
{
    public interface ITableData<T>
    {
        T Id { get; }
    }
}

namespace TestNamespace
{
    using Datra.Data.Attributes;
    using Datra.Data.Interfaces;

    [TableData(""Items.json"")]
    public partial class ItemData : ITableData<int>
    {
        public int Id { get; }
        public string Name { get; }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task Diagnostic_WhenTableDataClassHasSetter()
        {
            var test = @"
namespace Datra.Data.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class TableDataAttribute : System.Attribute
    {
        public TableDataAttribute(string path) { }
    }
}

namespace Datra.Data.Interfaces
{
    public interface ITableData<T>
    {
        T Id { get; set; }
    }
}

namespace TestNamespace
{
    using Datra.Data.Attributes;
    using Datra.Data.Interfaces;

    [TableData(""Items.json"")]
    public partial class ItemData : ITableData<int>
    {
        public int Id { get; {|#0:set;|} }
        public string Name { get; {|#1:set;|} }
    }
}";

            var expected1 = VerifyCS.Diagnostic(DataClassSetterAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("Id", "ItemData");
            
            var expected2 = VerifyCS.Diagnostic(DataClassSetterAnalyzer.DiagnosticId)
                .WithLocation(1)
                .WithArguments("Name", "ItemData");

            await VerifyCS.VerifyAnalyzerAsync(test, expected1, expected2);
        }

        [Fact]
        public async Task Diagnostic_WhenSingleDataClassHasSetter()
        {
            var test = @"
namespace Datra.Data.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class SingleDataAttribute : System.Attribute
    {
        public SingleDataAttribute(string path) { }
    }
}

namespace TestNamespace
{
    using Datra.Data.Attributes;

    [SingleData(""GameConfig.yaml"")]
    public partial class GameConfig
    {
        public int MaxLevel { get; {|#0:set;|} }
        public float ExpMultiplier { get; {|#1:set;|} }
    }
}";

            var expected1 = VerifyCS.Diagnostic(DataClassSetterAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("MaxLevel", "GameConfig");
            
            var expected2 = VerifyCS.Diagnostic(DataClassSetterAnalyzer.DiagnosticId)
                .WithLocation(1)
                .WithArguments("ExpMultiplier", "GameConfig");

            await VerifyCS.VerifyAnalyzerAsync(test, expected1, expected2);
        }

        [Fact]
        public async Task NoDiagnostic_WhenClassIsNotDataClass()
        {
            var test = @"
namespace TestNamespace
{
    public class RegularClass
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task Diagnostic_WhenImplementsITableDataWithoutAttribute()
        {
            var test = @"
namespace Datra.Data.Interfaces
{
    public interface ITableData<T>
    {
        T Id { get; set; }
    }
}

namespace TestNamespace
{
    using Datra.Data.Interfaces;

    public partial class ItemData : ITableData<string>
    {
        public string Id { get; {|#0:set;|} }
        public string Description { get; {|#1:set;|} }
    }
}";

            var expected1 = VerifyCS.Diagnostic(DataClassSetterAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("Id", "ItemData");
            
            var expected2 = VerifyCS.Diagnostic(DataClassSetterAnalyzer.DiagnosticId)
                .WithLocation(1)
                .WithArguments("Description", "ItemData");

            await VerifyCS.VerifyAnalyzerAsync(test, expected1, expected2);
        }
    }
}