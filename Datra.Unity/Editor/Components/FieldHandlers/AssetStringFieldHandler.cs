using System;
using System.Reflection;
using UnityEngine.UIElements;
using Datra.Attributes;
using Datra.Unity.Editor.UI;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for string fields with asset attributes (AssetType, FolderPath)
    /// </summary>
    public class AssetStringFieldHandler : IFieldTypeHandler
    {
        public int Priority => 50; // Higher than basic string handler

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            if (type != typeof(string))
                return false;

            // Check for asset attributes on the member
            if (member is PropertyInfo property)
            {
                return AttributeFieldHandler.HasAssetAttributes(property);
            }
            if (member is FieldInfo field)
            {
                return AttributeFieldHandler.HasAssetAttributes(field);
            }

            return false;
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            AssetTypeAttribute assetType = null;
            FolderPathAttribute folderPath = null;

            if (context.Property != null)
            {
                assetType = AttributeFieldHandler.GetAssetTypeAttribute(context.Property);
                folderPath = AttributeFieldHandler.GetFolderPathAttribute(context.Property);
            }
            else if (context.Member is FieldInfo field)
            {
                assetType = AttributeFieldHandler.GetAssetTypeAttribute(field);
                folderPath = AttributeFieldHandler.GetFolderPathAttribute(field);
            }

            var isTableMode = context.LayoutMode == DatraFieldLayoutMode.Table;

            var assetField = new AssetFieldElement(
                assetType,
                folderPath,
                context.Value as string ?? "",
                newValue => context.OnValueChanged?.Invoke(newValue),
                isTableMode);

            return assetField;
        }
    }
}
