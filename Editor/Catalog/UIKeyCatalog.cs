using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    [FilePath(
        "ProjectSettings/UITKNavigationKeyCatalog.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class UIKeyCatalog : ScriptableSingleton<UIKeyCatalog>, ISerializationCallbackReceiver
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

        private const int CurrentSchemaVersion = 3;

        [SerializeField] private List<CategoryEntry> categories = new();
        [SerializeField] private List<CategoryEntry> viewCategories = new();
        [FormerlySerializedAs("buttonCategories")]
        [SerializeField] private List<CategoryEntry> toggleCategories = new();
        [SerializeField] private List<CategoryEntry> signalCategories = new();
#pragma warning disable CS0414
        [SerializeField] private bool splitCatalogMigrated;
#pragma warning restore CS0414
        [SerializeField] private int catalogSchemaVersion;

        [NonSerialized] private bool _isValid;
        [NonSerialized] private Dictionary<string, CategoryEntry> _viewCategoryLookup;
        [NonSerialized] private Dictionary<string, CategoryEntry> _toggleCategoryLookup;
        [NonSerialized] private Dictionary<string, CategoryEntry> _signalCategoryLookup;
        [NonSerialized] private HashSet<UIKey> _viewKeyLookup;
        [NonSerialized] private HashSet<UIKey> _toggleKeyLookup;
        [NonSerialized] private HashSet<UIKey> _signalKeyLookup;
        [NonSerialized] private List<UIKey> _viewKeys;
        [NonSerialized] private List<UIKey> _toggleKeys;
        [NonSerialized] private List<UIKey> _signalKeys;

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

            int sourceSchemaVersion = catalogSchemaVersion;

            var existingKinds = new Dictionary<UIKey, HashSet<UIKeyCatalogKind>>();
            CollectExisting(viewCategories, UIKeyCatalogKind.View);
            CollectExisting(toggleCategories, UIKeyCatalogKind.Toggle);
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
                [UIKeyCatalogKind.Toggle] = new HashSet<UIKey>(),
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

                foreach (UIKeyCatalogKind kind in kinds)
                    separated[kind].Add(value);

                // Schema 2 stored NavButton and NavToggle keys together. When no
                // usage can disambiguate a legacy key, keep it in both destinations.
                if (sourceSchemaVersion == 2 && kinds.Contains(UIKeyCatalogKind.Toggle))
                    separated[UIKeyCatalogKind.Signal].Add(value);
            }

            foreach (UIKey value in legacyKeys)
            {
                if (!usedKinds.ContainsKey(value) && !existingKinds.ContainsKey(value))
                    separated[UIKeyCatalogKind.Signal].Add(value);
            }

            Undo.RecordObject(this, "Separate UI Navigation Key Catalog");
            viewCategories = BuildCategories(separated[UIKeyCatalogKind.View]);
            toggleCategories = BuildCategories(separated[UIKeyCatalogKind.Toggle]);
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
            return GetCachedKeys(kind);
        }

        internal bool Contains(
            UIKey value,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            if (!value.IsValid)
                return false;

            EnsureValid();
            return GetKeyLookup(kind).Contains(new UIKey(value.Category, value.Key));
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
            Dictionary<string, CategoryEntry> categoryLookup = GetCategoryLookup(kind);
            Undo.RecordObject(this, "Import UI Navigation Keys");
            foreach (UIKey value in values.AsValueEnumerable().Where(item => item.IsValid).Distinct())
            {
                string categoryName = Normalize(value.Category);
                categoryLookup.TryGetValue(categoryName, out CategoryEntry category);
                List<CategoryEntry> target = GetMutableCategories(kind);
                if (category == null)
                {
                    category = new CategoryEntry(categoryName);
                    target.Add(category);
                    categoryLookup.Add(categoryName, category);
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
            return GetCategoryLookup(kind).TryGetValue(normalized, out CategoryEntry result)
                ? result
                : null;
        }

        internal void SaveNow()
        {
            EnsureValid();
            Save(true);
        }

        private void SaveAndNotify()
        {
            _isValid = false;
            EnsureValid();
            Save(true);
            Changed?.Invoke();
        }

        private void EnsureValid()
        {
            if (_isValid)
                return;

            categories ??= new List<CategoryEntry>();
            viewCategories ??= new List<CategoryEntry>();
            toggleCategories ??= new List<CategoryEntry>();
            signalCategories ??= new List<CategoryEntry>();
            viewCategories = NormalizeCategories(viewCategories);
            toggleCategories = NormalizeCategories(toggleCategories);
            signalCategories = NormalizeCategories(signalCategories);
            BuildLookups(
                viewCategories,
                out _viewCategoryLookup,
                out _viewKeyLookup,
                out _viewKeys);
            BuildLookups(
                toggleCategories,
                out _toggleCategoryLookup,
                out _toggleKeyLookup,
                out _toggleKeys);
            BuildLookups(
                signalCategories,
                out _signalCategoryLookup,
                out _signalKeyLookup,
                out _signalKeys);
            _isValid = true;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _isValid = false;
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
                UIKeyCatalogKind.Toggle => toggleCategories,
                _ => signalCategories
            };
        }

        private Dictionary<string, CategoryEntry> GetCategoryLookup(UIKeyCatalogKind kind)
        {
            return kind switch
            {
                UIKeyCatalogKind.View => _viewCategoryLookup,
                UIKeyCatalogKind.Toggle => _toggleCategoryLookup,
                _ => _signalCategoryLookup
            };
        }

        private HashSet<UIKey> GetKeyLookup(UIKeyCatalogKind kind)
        {
            return kind switch
            {
                UIKeyCatalogKind.View => _viewKeyLookup,
                UIKeyCatalogKind.Toggle => _toggleKeyLookup,
                _ => _signalKeyLookup
            };
        }

        private IReadOnlyList<UIKey> GetCachedKeys(UIKeyCatalogKind kind)
        {
            return kind switch
            {
                UIKeyCatalogKind.View => _viewKeys,
                UIKeyCatalogKind.Toggle => _toggleKeys,
                _ => _signalKeys
            };
        }

        private static void BuildLookups(
            IReadOnlyList<CategoryEntry> source,
            out Dictionary<string, CategoryEntry> categoryLookup,
            out HashSet<UIKey> keyLookup,
            out List<UIKey> keys)
        {
            categoryLookup = new Dictionary<string, CategoryEntry>(source.Count, StringComparer.Ordinal);
            keyLookup = new HashSet<UIKey>();
            keys = new List<UIKey>();

            for (int categoryIndex = 0; categoryIndex < source.Count; categoryIndex++)
            {
                CategoryEntry category = source[categoryIndex];
                categoryLookup.Add(category.Name, category);
                IReadOnlyList<string> categoryKeys = category.Keys;
                for (int keyIndex = 0; keyIndex < categoryKeys.Count; keyIndex++)
                {
                    var value = new UIKey(category.Name, categoryKeys[keyIndex]);
                    keyLookup.Add(value);
                    keys.Add(value);
                }
            }
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
