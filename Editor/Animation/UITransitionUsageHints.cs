using UnityEditor;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Animation
{
    /// <summary>
    /// Keeps the authored usageHints attribute in sync with the enabled transition channels.
    /// </summary>
    internal static class UITransitionUsageHints
    {
        /// <summary>
        /// Defines the managed usage hints value.
        /// </summary>
        private const UsageHints ManagedUsageHints = UsageHints.DynamicTransform | UsageHints.DynamicColor;

        /// <summary>
        /// Performs the sync usage hints operation.
        /// </summary>
        internal static void SyncUsageHints(SerializedProperty transitionsProperty)
        {
            if (transitionsProperty == null)
                return;

            try
            {
                SerializedProperty hints = FindOwnerProperty(transitionsProperty, "usageHints");
                if (hints == null)
                    return;

                UsageHints required =
                    RequiredUsageHints(transitionsProperty.FindPropertyRelative("Show"))
                    | RequiredUsageHints(transitionsProperty.FindPropertyRelative("Hide"));

                var current = (UsageHints)hints.intValue;
                UsageHints next = (current & ~ManagedUsageHints) | required;
                if (next == current)
                    return;

                hints.intValue = (int)next;

                SerializedProperty flags = FindOwnerProperty(transitionsProperty, "usageHints_UxmlAttributeFlags");
                if (flags != null)
                {
                    flags.intValue = (int)(next == UsageHints.None
                        ? UxmlSerializedData.UxmlAttributeFlags.Ignore
                        : UxmlSerializedData.UxmlAttributeFlags.OverriddenInUxml);
                }

                hints.serializedObject.ApplyModifiedProperties();
            }
            catch
            {
                // ignored
            }
        }

        private static UsageHints RequiredUsageHints(SerializedProperty direction)
        {
            if (direction == null)
                return UsageHints.None;

            UsageHints hints = UsageHints.None;

            if (IsChannelEnabled(direction, "Move")
                || IsChannelEnabled(direction, "Rotate")
                || IsChannelEnabled(direction, "Scale"))
            {
                hints |= UsageHints.DynamicTransform;
            }

            if (IsChannelEnabled(direction, "Fade"))
                hints |= UsageHints.DynamicColor;

            return hints;
        }

        private static bool IsChannelEnabled(SerializedProperty direction, string channel)
        {
            SerializedProperty enable = direction.FindPropertyRelative(channel)?.FindPropertyRelative("Enable");
            return enable != null && enable.boolValue;
        }

        /// <summary>
        /// Finds owner property.
        /// </summary>
        private static SerializedProperty FindOwnerProperty(SerializedProperty property, string name)
        {
            return UxmlAuthoringUtility.FindOwnerProperty(property, name);
        }
    }
}
