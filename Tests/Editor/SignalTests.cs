using System.Reflection;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    internal sealed class SignalTests
    {
        private TestNavigationGraphBuilder _builder;
        private GameObject _navigatorObject;
        private UINavigatorBehaviour _previousNavigator;

        private static readonly FieldInfo InstanceField =
            typeof(UINavigatorBehaviour).GetField(
                "_instance",
                BindingFlags.Static | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            _builder = new TestNavigationGraphBuilder();
            _previousNavigator = InstanceField?.GetValue(null) as UINavigatorBehaviour;
            InstanceField?.SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_navigatorObject != null)
                Object.DestroyImmediate(_navigatorObject);

            InstanceField?.SetValue(null, _previousNavigator);
            _builder.Dispose();
        }

        [Test]
        public void Send_WithoutActiveNavigatorOrValidKey_ReturnsFalse()
        {
            Assert.IsFalse(Signal.Send(default(UIKey)));
            Assert.IsFalse(Signal.Send(" ", "Open"));
            Assert.IsFalse(Signal.Send("Test", " "));
            Assert.IsFalse(Signal.Send("Test", "Open"));
            Assert.IsFalse(Signal.Send(string.Empty));
            Assert.IsFalse(Signal.Send("Home"));
        }

        [Test]
        public void Send_UsesStringAndUIKeyOverloads_AndReturnsTransitionResult()
        {
            _builder
                .AddNode("A")
                .AddNode("B")
                .AddNode("C")
                .AddTransition("A", "Open", "B", UINavigationTransitionKind.Push)
                .AddTransition("B", "Continue", "C", UINavigationTransitionKind.Push);

            UINavigatorBehaviour navigator = CreateNavigator(_builder.Build());
            navigator.Service.Initialize();

            Assert.IsTrue(Signal.Send("Test", "Open"));
            Assert.AreEqual("B", navigator.Service.ActiveNode.Id);
            Assert.IsTrue(Signal.Send(new UIKey("Test", "Continue")));
            Assert.AreEqual("C", navigator.Service.ActiveNode.Id);
            Assert.IsFalse(Signal.Send("Test", "Missing"));
        }

        [Test]
        public void Send_BeforeInitialization_IsQueued()
        {
            _builder
                .AddNode("A")
                .AddNode("B")
                .AddTransition("A", "Open", "B", UINavigationTransitionKind.Push);

            UINavigatorBehaviour navigator = CreateNavigator(_builder.Build());

            Assert.IsTrue(Signal.Send("Test", "Open"));
            Assert.IsFalse(navigator.Service.IsInitialized);

            navigator.Service.Initialize();
            Assert.AreEqual("B", navigator.Service.ActiveNode.Id);
        }

        [Test]
        public void Send_CustomDestination_UsesSingleStringOverload()
        {
            _builder
                .AddNode("A")
                .AddNode("Home")
                .AddCustomTransition("Home", "Home", "A");

            UINavigatorBehaviour navigator = CreateNavigator(_builder.Build());
            navigator.Service.Initialize();

            Assert.IsTrue(Signal.Send("Home"));
            Assert.AreEqual("Home", navigator.Service.ActiveNode.Id);
        }

        private UINavigatorBehaviour CreateNavigator(UINavigationAsset asset)
        {
            _navigatorObject = new GameObject("Signal Test Navigator");
            _navigatorObject.SetActive(false);
            UINavigatorBehaviour navigator = _navigatorObject.AddComponent<UINavigatorBehaviour>();
            typeof(UINavigatorBehaviour)
                .GetField(
                    "navigationAsset",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(navigator, asset);
            _navigatorObject.SetActive(true);
            return navigator;
        }
    }
}
