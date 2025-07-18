using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.CodeFixVerifier<
    Datra.Data.Analyzers.DataClassSetterAnalyzer,
    Datra.Data.Analyzers.DataClassSetterCodeFixProvider>;

namespace Datra.Data.Analyzers.Tests
{
    public class DataClassSetterCodeFixTests
    {
        [Fact]
        public async Task RemoveSetter_FromAutoProperty()
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

            var fixedCode = @"
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
        public int Id { get; }
        public string Name { get; }
    }
}";

            var expected1 = VerifyCS.Diagnostic(DataClassSetterAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("Id", "ItemData");
            
            var expected2 = VerifyCS.Diagnostic(DataClassSetterAnalyzer.DiagnosticId)
                .WithLocation(1)
                .WithArguments("Name", "ItemData");

            await VerifyCS.VerifyCodeFixAsync(test, new[] { expected1, expected2 }, fixedCode);
        }

        [Fact]
        public async Task RemoveSetter_FromPropertyWithPrivateSetter()
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
        public int MaxLevel { get; private {|#0:set;|} }
    }
}";

            var fixedCode = @"
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
        public int MaxLevel { get; }
    }
}";

            var expected = VerifyCS.Diagnostic(DataClassSetterAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("MaxLevel", "GameConfig");

            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task RemoveSetter_PreservesGetterBody()
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
        private int _id;
        public int Id 
        { 
            get { return _id; }
            {|#0:set|} { _id = value; }
        }
    }
}";

            var fixedCode = @"
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
        private int _id;
        public int Id 
        { 
            get { return _id; }
        }
    }
}";

            var expected = VerifyCS.Diagnostic(DataClassSetterAnalyzer.DiagnosticId)
                .WithLocation(0)
                .WithArguments("Id", "ItemData");

            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedCode);
        }
    }
}