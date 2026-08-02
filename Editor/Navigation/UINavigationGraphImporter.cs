using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    [ScriptedImporter(5, UINavigationAuthoringGraph.Extension)]
    internal sealed class UINavigationGraphImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            try
            {
                UINavigationAuthoringGraph graph =
                    GraphDatabase.LoadGraphForImporter<UINavigationAuthoringGraph>(context.assetPath);
                var errors = new List<string>();
                var runtimeAsset = UINavigationGraphCompiler.Compile(graph, errors);
                runtimeAsset.name = System.IO.Path.GetFileNameWithoutExtension(context.assetPath);

                context.AddObjectToAsset("NavigationGraph", runtimeAsset);
                context.SetMainObject(runtimeAsset);

                foreach (string validationError in UINavigationGraphValidator.Validate(runtimeAsset))
                {
                    if (!errors.Contains(validationError))
                        errors.Add(validationError);
                }

                foreach (string error in errors)
                    context.LogImportError(error);
            }
            catch (Exception exception)
            {
                context.LogImportError(
                    $"UI Navigation Graph을 임포트하지 못했습니다: {exception.Message}");
            }
        }
    }
}
