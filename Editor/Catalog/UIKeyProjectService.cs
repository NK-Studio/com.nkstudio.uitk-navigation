using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NKStudio.UITKNavigation.Editor.Navigation;
using NKStudio.UITKNavigation.Identity;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    /// <summary>
    /// Entry point for project-wide UIKey scanning and renaming.
    /// </summary>
    /// <remarks>
    /// The per-format work lives next to it: <see cref="UIKeyUxmlUsages"/> for UXML
    /// attributes and <see cref="UIKeyGraphUsages"/> for authoring graphs.
    /// </remarks>
    internal static class UIKeyProjectService
    {
        private static bool _catalogMigrationInProgress;

        internal static IReadOnlyList<UIKeyUsage> ScanProject(bool logFailures = true)
        {
            var usages = new List<UIKeyUsage>();
            UIKeyUxmlUsages.ScanUxml(usages, logFailures);
            UIKeyGraphUsages.ScanGraphs(usages, logFailures);
            return usages;
        }

        internal static void EnsureCatalogIsSeparated()
        {
            if (_catalogMigrationInProgress ||
                !UIKeyCatalog.instance.NeedsSeparatedCatalogMigration)
            {
                return;
            }

            _catalogMigrationInProgress = true;
            try
            {
                UIKeyCatalog.instance.MigrateToSeparatedCatalog(ScanProject(false));
            }
            finally
            {
                _catalogMigrationInProgress = false;
            }
        }

        internal static int CountUsages(
            UIKey value,
            IReadOnlyList<UIKeyUsage> usages,
            UIKeyCatalogKind? kind = null)
        {
            return usages?.AsValueEnumerable().Count(usage =>
                usage.Value == value &&
                (!kind.HasValue || usage.CatalogKind == kind.Value)) ?? 0;
        }

        internal static int CountCategoryUsages(
            string category,
            IReadOnlyList<UIKeyUsage> usages,
            UIKeyCatalogKind? kind = null)
        {
            return usages?.AsValueEnumerable().Count(usage =>
                string.Equals(usage.Value.Category, category, StringComparison.Ordinal) &&
                (!kind.HasValue || usage.CatalogKind == kind.Value)) ?? 0;
        }

        internal static bool RenameCategory(
            string oldCategory,
            string newCategory,
            UIKeyCatalogKind kind,
            out string error)
        {
            return Rename(
                key => string.Equals(key.Category, oldCategory, StringComparison.Ordinal)
                    ? new UIKey(newCategory, key.Key)
                    : key,
                key => string.Equals(key.Category, oldCategory, StringComparison.Ordinal),
                kind,
                out error);
        }

        internal static bool RenameKey(
            UIKey oldValue,
            string newKey,
            UIKeyCatalogKind kind,
            out string error)
        {
            return Rename(
                key => key == oldValue
                    ? new UIKey(oldValue.Category, newKey)
                    : key,
                key => key == oldValue,
                kind,
                out error);
        }

        private static bool Rename(
            Func<UIKey, UIKey> replace,
            Func<UIKey, bool> matches,
            UIKeyCatalogKind kind,
            out string error)
        {
            error = null;
            IReadOnlyList<UIKeyUsage> usages = ScanProject();
            string[] paths = usages
                .AsValueEnumerable()
                .Where(usage => usage.CatalogKind == kind && matches(usage.Value))
                .Select(usage => usage.AssetPath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0)
                return true;

            var backups = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                foreach (string path in paths)
                {
                    if (!File.Exists(path))
                        throw new FileNotFoundException("사용 중인 자산을 찾지 못했습니다.", path);
                    backups[path] = File.ReadAllText(path);
                }

                var uxmlDocuments = new Dictionary<string, XmlDocument>(StringComparer.Ordinal);
                foreach (string path in paths.AsValueEnumerable().Where(UIKeyUxmlUsages.IsUxmlPath))
                    uxmlDocuments[path] = UIKeyUxmlUsages.LoadUxml(path);

                foreach ((string path, XmlDocument document) in uxmlDocuments)
                {
                    if (UIKeyUxmlUsages.ReplaceUxml(document, replace, matches, kind))
                        document.Save(path);
                }

                foreach (string path in paths.AsValueEnumerable().Where(UIKeyGraphUsages.IsGraphPath))
                {
                    UINavigationAuthoringGraph graph =
                        GraphDatabase.LoadGraph<UINavigationAuthoringGraph>(path);
                    if (graph == null)
                        throw new InvalidOperationException($"Graph를 불러오지 못했습니다: {path}");

                    if (UIKeyGraphUsages.ReplaceGraph(graph, replace, matches, kind))
                        GraphDatabase.SaveGraph(graph);
                }

                foreach (string path in paths)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                foreach ((string path, string contents) in backups)
                {
                    try
                    {
                        File.WriteAllText(path, contents);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                    catch (Exception restoreException)
                    {
                        Debug.LogException(restoreException);
                    }
                }

                AssetDatabase.Refresh();
                error = exception.Message;
                Debug.LogException(exception);
                return false;
            }
        }
    }
}
