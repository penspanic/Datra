#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for List&lt;T&gt; types with polymorphism support
    /// </summary>
    public class ListFieldHandler : BaseCollectionFieldHandler
    {
        public override int Priority => 22;  // Higher than ArrayFieldHandler (20)

        public override bool CanHandle(Type type, MemberInfo member = null)
        {
            if (!type.IsGenericType)
                return false;

            return type.GetGenericTypeDefinition() == typeof(List<>);
        }

        protected override Type GetElementType(Type collectionType)
        {
            return collectionType.GetGenericArguments()[0];
        }

        protected override IList GetElementsAsList(object collection)
        {
            if (collection == null) return new List<object>();

            var list = collection as IList;
            if (list == null) return new List<object>();

            var result = new List<object>();
            foreach (var item in list)
            {
                result.Add(item);
            }
            return result;
        }

        protected override object CreateCollectionFromList(IList elements, Type elementType)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType);

            foreach (var element in elements)
            {
                list.Add(element);
            }

            return list;
        }

        protected override string GetCollectionDisplayText(object collection)
        {
            if (collection == null) return "[0 items]";

            var list = collection as IList;
            var count = list?.Count ?? 0;
            return $"[{count} items]";
        }
    }
}
