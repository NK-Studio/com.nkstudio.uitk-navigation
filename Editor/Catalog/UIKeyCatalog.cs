using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using UnityEditor;
using UnityEngine;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    [FilePath(
        "ProjectSettings/UITKNavigationKeyCatalog.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class UIKeyCatalog : ScriptableSingleton<UIKeyCatalog>
    {
        [Serializable]
        internal sealed class CategoryEntry
        {
            [SerializeField] private string name;
            [SerializeField] private List<string> keys = new();

            internal CategoryEntry(string name)
            {
                this.name = Normalize(name);
            }

            internal string Name
            {
                get => name ?? string.Empty;
                set => name = Normalize(value);
            }

            internal IReadOnlyList<string> Keys => keys;

            internal bool Contains(string key)
            {
                string normalized = Normalize(key);
                return keys.AsValueEnumerable().Any(item => string.Equals(item, normalized, StringComparison.Ordinal));
            }

            internal bool Add(string key)
            {
                string normalized = Normalize(key);
                if (string.IsNullOrEmpty(normalized) || Contains(normalized))
                    return false;

                keys.Add(normalized);
                SortKeys();
                return true;
            }

            internal bool Remove(string key)
            {
                int index = keys.FindIndex(item =>
                    string.Equals(item, Normalize(key), StringComparison.Ordinal));
                if (index < 0)
                    return false;

                keys.RemoveAt(index);
                return true;
            }

            internal bool Rename(string oldKey, string newKey)
            {
                string oldValue = Normalize(oldKey);
                string newValue = Normalize(newKey);
                if (string.IsNullOrEmpty(newValue) ||
                    Contains(newValue) ||
                    string.Equals(oldValue, newValue, StringComparison.Ordinal))
                {
                    return false;
                }

                int index = keys.FindIndex(item =>
                    string.Equals(item, oldValue, StringComparison.Ordinal));
                if (index < 0)
                    return false;

                keys[index] = newValue;
                SortKeys();
                return true;
            }

            internal void SortKeys()
            {
                keys = keys
                    .AsValueEnumerable()
                    .Select(Normalize)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(value => value, StringComparer.Ordinal)
                    .ToList();
            }

            internal CategoryEntry Clone()
            {
                var clone = new CategoryEntry(Name);
                foreach (string key in Keys)
                    clone.Add(key);
                return clone;
            }
        }

        private const int CurrentSchemaVersion = 2;

        [SerializeField] private List<CategoryEntry> categories = new();
        [SerializeField] private List<CategoryEntry> viewCategories = new();
        [SerializeField] private List<CategoryEntry> buttonCategories = new();
        [SerializeField] private List<CategoryEntry> signalCategories = new();
#pragma warning disable CS0414
        [SerializeField] private bool splitCatalogMigrated;
#pragma warning restore CS0414
        [SerializeField] private int catalogSchemaVersion;

        internal static event Action Changed;

        internal bool NeedsSeparatedCatalogMigration
        {
            get
            {
                EnsureValid();
                return catalogSchemaVersion < CurrentSchemaVersion;
            }
        }

        internal void MigrateToSeparatedCatalog(IReadOnlyList<UIKeyUsage> usages)
        {
            EnsureValid();
            if (catalogSchemaVersion >= CurrentSchemaVersion)
                return;

            var existingKinds = new Dictionary<UIKey, HashSet<UIKeyCatalogKind>>();
            CollectExisting(viewCategories, UIKeyCatalogKind.View);
            CollectExisting(buttonCategories, UIKeyCatalogKind.Button);
            CollectExisting(signalCategories, UIKeyCatalogKind.Signal);

            var legacyKeys = new HashSet<UIKey>(
                categories.AsValueEnumerable().Where(category => category != null).SelectMany(category =>
                    category.Keys.AsValueEnumerable().Select(key => new UIKey(category.Name, key))).ToArray());
            var usedKinds = (usages ?? Array.Empty<UIKeyUsage>())
                .AsValueEnumerable()
                .Where(usage => usage.Value.IsValid)
                .GroupBy(usage => usage.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.AsValueEnumerable().Select(usage => usage.CatalogKind).ToHashSet());

            var separated = new Dictionary<UIKeyCatalogKind, HashSet<UIKey>>
            {
                [UIKeyCatalogKind.View] = new HashSet<UIKey>(),
                [UIKeyCatalogKind.Button] = new HashSet<UIKey>(),
                [UIKeyCatalogKind.Signal] = new HashSet<UIKey>()
            };

            foreach ((UIKey value, HashSet<UIKeyCatalogKind> kinds) in usedKinds)
            {
                foreach (UIKeyCatalogKind kind in kinds)
                    separated[kind].Add(value);
            }

            foreach ((UIKey value, HashSet<UIKeyCatalogKind> kinds) in existingKinds)
            {
                if (usedKinds.ContainsKey(value))
                    continue;

                UIKeyCatalogKind target = kinds.Count == 1
                    ? kinds.AsValueEnumerable().First()
                    : UIKeyCatalogKind.Signal;
                separated[target].Add(value);
            }

            foreach (UIKey value in legacyKeys)
            {
                if (!usedKinds.ContainsKey(value) && !existingKinds.ContainsKey(value))
                    separated[UIKeyCatalogKind.Signal].Add(value);
            }

            Undo.RecordObject(this, "Separate UI Navigation Key Catalog");
            viewCategories = BuildCategories(separated[UIKeyCatalogKind.View]);
            buttonCategories = BuildCategories(separated[UIKeyCatalogKind.Button]);
            signalCategories = BuildCategories(separated[UIKeyCatalogKind.Signal]);
            splitCatalogMigrated = true;
            catalogSchemaVersion = CurrentSchemaVersion;
            SaveAndNotify();
            return;

            void CollectExisting(
                IEnumerable<CategoryEntry> source,
                UIKeyCatalogKind kind)
            {
                foreach (CategoryEntry category in source ?? Array.Empty<CategoryEntry>())
                {
                    foreach (string key in category.Keys)
                    {
                        var value = new UIKey(category.Name, key);
                        if (!existingKinds.TryGetValue(value, out HashSet<UIKeyCatalogKind> kinds))
                        {
                            kinds = new HashSet<UIKeyCatalogKind>();
                            existingKinds.Add(value, kinds);
                        }

                        kinds.Add(kind);
                    }
                }
            }
        }

        internal IReadOnlyList<CategoryEntry> Categories
            => GetCategories(UIKeyCatalogKind.Signal);

        internal IReadOnlyList<CategoryEntry> GetCategories(UIKeyCatalogKind kind)
        {
            EnsureValid();
            return GetMutableCategories(kind);
        }

        internal IEnumerable<UIKey> Keys
            => GetKeys(UIKeyCatalogKind.Signal);

        internal IEnumerable<UIKey> GetKeys(UIKeyCatalogKind kind)
        {
            EnsureValid();
            return GetMutableCategories(kind).AsValueEnumerable().SelectMany(category =>
                category.Keys.AsValueEnumerable().Select(key => new UIKey(category.Name, key))).ToArray();
        }

        internal bool Contains(
            UIKey value,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            if (!value.IsValid)
                return false;

            CategoryEntry category = FindCategory(value.Category, kind);
            return category != null && category.Contains(value.Key);
        }

        internal bool AddCategory(
            string category,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            EnsureValid();
            string normalized = Normalize(category);
            if (string.IsNullOrEmpty(normalized) || FindCategory(normalized, kind) != null)
                return false;

            Undo.RecordObject(this, "Add UI Navigation Category");
            GetMutableCategories(kind).Add(new CategoryEntry(normalized));
            SaveAndNotify();
            return true;
        }

        internal bool Add(
            UIKey value,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            EnsureValid();
            if (!value.IsValid)
                return false;

            CategoryEntry category = FindCategory(value.Category, kind);
            List<CategoryEntry> target = GetMutableCategories(kind);
            bool changed = false;
            Undo.RecordObject(this, "Add UI Navigation Key");
            if (category == null)
            {
                category = new CategoryEntry(value.Category);
                target.Add(category);
                changed = true;
            }

            changed |= category.Add(value.Key);
            if (changed)
                SaveAndNotify();

            return changed;
        }

        internal int AddRange(
            IEnumerable<UIKey> values,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            EnsureValid();
            if (values == null)
                return 0;

            int count = 0;
            Undo.RecordObject(this, "Import UI Navigation Keys");
            foreach (UIKey value in values.AsValueEnumerable().Where(item => item.IsValid).Distinct())
            {
                CategoryEntry category = FindCategory(value.Category, kind);
                List<CategoryEntry> target = GetMutableCategories(kind);
                if (category == null)
                {
                    category = new CategoryEntry(value.Category);
                    target.Add(category);
                }

                if (category.Add(value.Key))
                    count++;
            }

            if (count > 0)
                SaveAndNotify();

            return count;
        }

        internal bool RemoveCategory(
            string category,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            EnsureValid();
            List<CategoryEntry> target = GetMutableCategories(kind);
            int index = target.FindIndex(item =>
                string.Equals(item.Name, Normalize(category), StringComparison.Ordinal));
            if (index < 0)
                return false;

            Undo.RecordObject(this, "Remove UI Navigation Category");
            target.RemoveAt(index);
            SaveAndNotify();
            return true;
        }

        internal bool Remove(
            UIKey value,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            EnsureValid();
            CategoryEntry category = FindCategory(value.Category, kind);
            if (category == null)
                return false;

            Undo.RecordObject(this, "Remove UI Navigation Key");
            if (!category.Remove(value.Key))
                return false;

            SaveAndNotify();
            return true;
        }

        internal bool RenameCategoryLocal(
            string oldCategory,
            string newCategory,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            EnsureValid();
            string oldValue = Normalize(oldCategory);
            string newValue = Normalize(newCategory);
            CategoryEntry category = FindCategory(oldValue, kind);
            if (category == null ||
                string.IsNullOrEmpty(newValue) ||
                FindCategory(newValue, kind) != null ||
                string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                return false;
            }

            Undo.RecordObject(this, "Rename UI Navigation Category");
            category.Name = newValue;
            SaveAndNotify();
            return true;
        }

        internal bool RenameKeyLocal(
            UIKey oldValue,
            string newKey,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            EnsureValid();
            CategoryEntry category = FindCategory(oldValue.Category, kind);
            if (category == null)
                return false;

            Undo.RecordObject(this, "Rename UI Navigation Key");
            if (!category.Rename(oldValue.Key, newKey))
                return false;

            SaveAndNotify();
            return true;
        }

        internal CategoryEntry FindCategory(
            string category,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            EnsureValid();
            string normalized = Normalize(category);
            return GetMutableCategories(kind).AsValueEnumerable().FirstOrDefault(item =>
                string.Equals(item.Name, normalized, StringComparison.Ordinal));
        }

        internal void SaveNow()
        {
            EnsureValid();
            Save(true);
        }

        private void SaveAndNotify()
        {
            EnsureValid();
            Save(true);
            Changed?.Invoke();
        }

        private void EnsureValid()
        {
            categories ??= new List<CategoryEntry>();
            viewCategories ??= new List<CategoryEntry>();
            buttonCategories ??= new List<CategoryEntry>();
            signalCategories ??= new List<CategoryEntry>();
            viewCategories = NormalizeCategories(viewCategories);
            buttonCategories = NormalizeCategories(buttonCategories);
            signalCategories = NormalizeCategories(signalCategories);
        }

        private static List<CategoryEntry> BuildCategories(IEnumerable<UIKey> values)
        {
            return values
                .AsValueEnumerable()
                .Where(value => value.IsValid)
                .GroupBy(value => value.Category, StringComparer.Ordinal)
                .Select(group =>
                {
                    var category = new CategoryEntry(group.Key);
                    foreach (UIKey value in group)
                        category.Add(value.Key);
                    return category;
                })
                .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(category => category.Name, StringComparer.Ordinal)
                .ToList();
        }

        private List<CategoryEntry> GetMutableCategories(UIKeyCatalogKind kind)
        {
            return kind switch
            {
                UIKeyCatalogKind.View => viewCategories,
                UIKeyCatalogKind.Button => buttonCategories,
                _ => signalCategories
            };
        }

        private static List<CategoryEntry> NormalizeCategories(List<CategoryEntry> source)
        {
            List<CategoryEntry> result = (source ?? new List<CategoryEntry>())
                .AsValueEnumerable()
                .Where(category => category != null && !string.IsNullOrEmpty(category.Name))
                .GroupBy(category => category.Name, StringComparer.Ordinal)
                .Select(group => group.AsValueEnumerable().First())
                .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(category => category.Name, StringComparer.Ordinal)
                .ToList();
            foreach (CategoryEntry category in result)
                category.SortKeys();
            return result;
        }

        private static string Normalize(string value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
