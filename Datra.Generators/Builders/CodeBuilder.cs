using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Datra.Generators.Builders
{
    internal class CodeBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indentLevel = 0;
        private const string IndentString = "    ";

        public void AddUsing(string namespaceName)
        {
            _sb.AppendLine($"using {namespaceName};");
        }

        public void AddUsings(IEnumerable<string> namespaces)
        {
            foreach (var ns in namespaces.OrderBy(n => n))
            {
                AddUsing(ns);
            }
        }

        public void AddBlankLine()
        {
            _sb.AppendLine();
        }

        public void BeginNamespace(string namespaceName)
        {
            AppendLine($"namespace {namespaceName}");
            BeginBlock();
        }

        public void EndNamespace()
        {
            EndBlock();
        }

        public void BeginClass(string className, string modifiers = "public", string baseClass = null, IEnumerable<string> interfaces = null)
        {
            var declaration = $"{modifiers} class {className}";
            
            var inheritanceList = new List<string>();
            if (!string.IsNullOrEmpty(baseClass))
                inheritanceList.Add(baseClass);
            if (interfaces != null)
                inheritanceList.AddRange(interfaces);
            
            if (inheritanceList.Any())
                declaration += " : " + string.Join(", ", inheritanceList);
            
            AppendLine(declaration);
            BeginBlock();
        }

        public void EndClass()
        {
            EndBlock();
        }

        public void BeginMethod(string methodSignature)
        {
            AppendLine(methodSignature);
            BeginBlock();
        }

        public void EndMethod()
        {
            EndBlock();
        }

        public void BeginBlock()
        {
            AppendLine("{");
            _indentLevel++;
        }

        public void EndBlock()
        {
            _indentLevel--;
            AppendLine("}");
        }
        
        public void EndBlock(string suffix)
        {
            _indentLevel--;
            AppendLine("}" + suffix);
        }

        public void AppendLine(string line = "")
        {
            if (string.IsNullOrEmpty(line))
            {
                _sb.AppendLine();
            }
            else
            {
                _sb.AppendLine(GetIndent() + line);
            }
        }

        public void Append(string text)
        {
            _sb.Append(GetIndent() + text);
        }

        public void AppendMultilineString(string text)
        {
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                AppendLine(line.TrimEnd());
            }
        }

        public override string ToString() => _sb.ToString();

        private string GetIndent() => string.Concat(Enumerable.Repeat(IndentString, _indentLevel));

        public static string GetSimpleTypeName(string fullTypeName)
        {
            // Remove global:: prefix if present
            var typeName = fullTypeName.StartsWith("global::") ? fullTypeName.Substring(8) : fullTypeName;
            var lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 ? typeName.Substring(lastDot + 1) : typeName;
        }

        public static string GetNamespace(string fullTypeName)
        {
            // Remove global:: prefix if present
            var typeName = fullTypeName.StartsWith("global::") ? fullTypeName.Substring(8) : fullTypeName;
            var lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 ? typeName.Substring(0, lastDot) : "Generated";
        }

        public static string ToCamelCase(string pascalCase)
        {
            if (string.IsNullOrEmpty(pascalCase))
                return pascalCase;
            return char.ToLower(pascalCase[0]) + pascalCase.Substring(1);
        }

        public static string GetDataFormat(string format)
        {
            if (format.Contains("."))
            {
                return format.Split('.').Last();
            }
            return format;
        }
    }
}