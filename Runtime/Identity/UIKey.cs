using System;
using UnityEngine;

namespace NKStudio.UITKNavigation.Identity
{
    /// <summary>
    /// Represents UI Key data.
    /// </summary>
    [Serializable]
    public struct UIKey : IEquatable<UIKey>
    {
        [SerializeField] private string category;
        [SerializeField] private string key;

        /// <summary>
        /// Initializes a normalized UI key from a category and a key value.
        /// </summary>
        /// <param name="category">The catalog category that scopes the key.</param>
        /// <param name="key">The key value within the category.</param>
        public UIKey(string category, string key)
        {
            this.category = Normalize(category);
            this.key = Normalize(key);
        }

        /// <summary>
        /// Gets the normalized category, or an empty string when unset.
        /// </summary>
        public string Category => category ?? string.Empty;
        /// <summary>
        /// Gets the normalized key value, or an empty string when unset.
        /// </summary>
        public string Key => key ?? string.Empty;
        /// <summary>
        /// Gets whether both the category and key contain non-whitespace text.
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(key);

        /// <summary>
        /// Determines whether this key and another key have identical ordinal category and key values.
        /// </summary>
        /// <param name="other">The key to compare.</param>
        /// <returns><see langword="true"/> when both components match; otherwise, <see langword="false"/>.</returns>
        public bool Equals(UIKey other)
        {
            return string.Equals(Category, other.Category, StringComparison.Ordinal)
                   && string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether the specified object is an equal UI key.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns><see langword="true"/> when the object is an equal <see cref="UIKey"/>.</returns>
        public override bool Equals(object obj) => obj is UIKey other && Equals(other);

        /// <summary>
        /// Returns an ordinal hash code for the category and key pair.
        /// </summary>
        /// <returns>The hash code for this key.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Category) * 397)
                       ^ StringComparer.Ordinal.GetHashCode(Key);
            }
        }

        /// <summary>
        /// Returns the key as <c>Category/Key</c>, or <c>(None)</c> when invalid.
        /// </summary>
        /// <returns>A display string for this key.</returns>
        public override string ToString() => IsValid ? $"{Category}/{Key}" : "(None)";

        /// <summary>
        /// Determines whether two UI keys are equal.
        /// </summary>
        /// <param name="left">The left key.</param>
        /// <param name="right">The right key.</param>
        /// <returns><see langword="true"/> when the keys are equal.</returns>
        public static bool operator ==(UIKey left, UIKey right) => left.Equals(right);
        /// <summary>
        /// Determines whether two UI keys are different.
        /// </summary>
        /// <param name="left">The left key.</param>
        /// <param name="right">The right key.</param>
        /// <returns><see langword="true"/> when the keys are different.</returns>
        public static bool operator !=(UIKey left, UIKey right) => !left.Equals(right);

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
