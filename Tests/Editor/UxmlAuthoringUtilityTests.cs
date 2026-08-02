using NKStudio.UITKNavigation.Editor;
using NUnit.Framework;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    /// <summary>
    /// Provides Uxml Authoring Utility Tests functionality.
    /// </summary>
    public sealed class UxmlAuthoringUtilityTests
    {
        [TestCase("MainLobby", ExpectedResult = "main-lobby")]
        [TestCase("CasualDemo", ExpectedResult = "casual-demo")]
        [TestCase("Shop", ExpectedResult = "shop")]
        [TestCase("Slide1", ExpectedResult = "slide1")]
        [TestCase("character_showing", ExpectedResult = "character-showing")]
        [TestCase("Area Selection", ExpectedResult = "area-selection")]
        [TestCase("already-kebab", ExpectedResult = "already-kebab")]
        [TestCase("", ExpectedResult = "")]
        public string ToKebabCase_ConvertsIdentifiers(string value)
        {
            return UxmlAuthoringUtility.ToKebabCase(value);
        }

        [Test]
        public void ToKebabCase_KeepsAcronymTogether()
        {
            Assert.AreEqual("ui-panel", UxmlAuthoringUtility.ToKebabCase("UIPanel"));
            Assert.AreEqual("http-server", UxmlAuthoringUtility.ToKebabCase("HTTPServer"));
        }

        [Test]
        public void ToKebabCase_DropsLeadingAndDuplicateSeparators()
        {
            Assert.AreEqual("main-lobby", UxmlAuthoringUtility.ToKebabCase("  Main__Lobby "));
            Assert.AreEqual("main-lobby", UxmlAuthoringUtility.ToKebabCase("/Main/Lobby"));
        }
    }
}
