using NKStudio.UITKNavigation.Identity;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    internal enum UIKeyUsageKind
    {
        View,
        Toggle,
        Signal,
        ShowOnEnter,
        HideOnEnter,
        ShowOnExit,
        HideOnExit
    }

    internal readonly struct UIKeyUsage
    {
        internal UIKeyUsage(
            UIKey value,
            string assetPath,
            UIKeyUsageKind kind,
            string context)
        {
            Value = value;
            AssetPath = assetPath;
            Kind = kind;
            Context = context;
        }

        internal UIKey Value { get; }
        internal string AssetPath { get; }
        internal UIKeyUsageKind Kind { get; }
        internal UIKeyCatalogKind CatalogKind => GetCatalogKind(Kind);
        internal string Context { get; }

        internal static UIKeyCatalogKind GetCatalogKind(UIKeyUsageKind kind)
        {
            return kind switch
            {
                UIKeyUsageKind.View or
                    UIKeyUsageKind.ShowOnEnter or
                    UIKeyUsageKind.HideOnEnter or
                    UIKeyUsageKind.ShowOnExit or
                    UIKeyUsageKind.HideOnExit => UIKeyCatalogKind.View,
                UIKeyUsageKind.Toggle => UIKeyCatalogKind.Toggle,
                _ => UIKeyCatalogKind.Signal
            };
        }
    }
}
