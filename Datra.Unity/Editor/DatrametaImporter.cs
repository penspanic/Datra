using UnityEditor.AssetImporters;
using UnityEngine;

namespace Datra.Unity.Editor
{
    /// <summary>
    /// Imports .datrameta files as TextAsset for Addressables compatibility.
    /// </summary>
    [ScriptedImporter(1, "datrameta")]
    public class DatrametaImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var text = System.IO.File.ReadAllText(ctx.assetPath);
            var textAsset = new TextAsset(text);
            ctx.AddObjectToAsset("main", textAsset);
            ctx.SetMainObject(textAsset);
        }
    }
}
