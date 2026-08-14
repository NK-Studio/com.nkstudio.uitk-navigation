using System;
using System.Collections.Generic;
using System.Xml;
using NKStudio.UITKNavigation.Identity;
using UnityEditor;
using UnityEngine;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    /// <summary>
    /// Reads and rewrites UIKey addresses stored as UXML attributes.
    /// </summary>
    internal static class UIKeyUxmlUsages
    {
        internal static void ScanUxml(List<UIKeyUsage> usages, bool logFailures)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:VisualTreeAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsUxmlPath(path))
                    continue;

                try
                {
                    XmlDocument document = LoadUxml(path);
                    foreach (XmlElement element in EnumerateElements(document))
                    {
                        if (element.LocalName ==
                            nameof(NKStudio.UITKNavigation.Elements.NavElement))
                        {
                            AddUxmlUsage(
                                usages,
                                path,
                                element,
                                "view-category",
                                "view-key",
                                UIKeyUsageKind.View);
                        }
                        else if (element.LocalName ==
                                 nameof(NKStudio.UITKNavigation.Elements.NavButton))
                        {
                            AddUxmlUsage(
                                usages,
                                path,
                                element,
                                "signal-category",
                                "signal-key",
                                UIKeyUsageKind.Signal);
                        }
                        else if (element.LocalName == "NavToggle")
                        {
                            AddUxmlUsage(
                                usages,
                                path,
                                element,
                                "toggle-category",
                                "toggle-key",
                                UIKeyUsageKind.Toggle);
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (logFailures)
                        Debug.LogWarning($"UXML 주소를 읽지 못했습니다: {path}\n{exception.Message}");
                }
            }
        }

        private static void AddUxmlUsage(
            ICollection<UIKeyUsage> usages,
            string path,
            XmlElement element,
            string categoryAttribute,
            string keyAttribute,
            UIKeyUsageKind kind)
        {
            var value = new UIKey(
                element.GetAttribute(categoryAttribute),
                element.GetAttribute(keyAttribute));
            if (!value.IsValid)
                return;

            usages.Add(new UIKeyUsage(
                value,
                path,
                kind,
                element.GetAttribute("name")));
        }

        internal static bool ReplaceUxml(
            XmlDocument document,
            Func<UIKey, UIKey> replace,
            Func<UIKey, bool> matches,
            UIKeyCatalogKind targetKind)
        {
            bool changed = false;
            foreach (XmlElement element in EnumerateElements(document))
            {
                string categoryAttribute;
                string keyAttribute;
                UIKeyCatalogKind elementKind;
                if (element.LocalName ==
                    nameof(NKStudio.UITKNavigation.Elements.NavElement))
                {
                    categoryAttribute = "view-category";
                    keyAttribute = "view-key";
                    elementKind = UIKeyCatalogKind.View;
                }
                else if (element.LocalName ==
                         nameof(NKStudio.UITKNavigation.Elements.NavButton))
                {
                    categoryAttribute = "signal-category";
                    keyAttribute = "signal-key";
                    elementKind = UIKeyCatalogKind.Signal;
                }
                else if (element.LocalName == "NavToggle")
                {
                    categoryAttribute = "toggle-category";
                    keyAttribute = "toggle-key";
                    elementKind = UIKeyCatalogKind.Toggle;
                }
                else
                {
                    continue;
                }

                if (elementKind != targetKind)
                    continue;

                var current = new UIKey(
                    element.GetAttribute(categoryAttribute),
                    element.GetAttribute(keyAttribute));
                if (!matches(current))
                    continue;

                UIKey next = replace(current);
                element.SetAttribute(categoryAttribute, next.Category);
                element.SetAttribute(keyAttribute, next.Key);
                changed = true;
            }

            return changed;
        }

        internal static XmlDocument LoadUxml(string path)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            document.Load(path);
            return document;
        }

        private static IEnumerable<XmlElement> EnumerateElements(XmlDocument document)
        {
            if (document?.DocumentElement == null)
                yield break;

            var stack = new Stack<XmlNode>();
            stack.Push(document.DocumentElement);
            while (stack.Count > 0)
            {
                XmlNode node = stack.Pop();
                if (node is XmlElement element)
                    yield return element;

                for (int index = node.ChildNodes.Count - 1; index >= 0; index--)
                    stack.Push(node.ChildNodes[index]);
            }
        }

        internal static bool IsUxmlPath(string path)
        {
            return path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase);
        }
    }
}
