using NKStudio.UITKNavigation.Elements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Tests
{
    public sealed class NavElementStartupRuntimeTests
    {
        private NavElement _element;

        [SetUp]
        public void SetUp()
        {
            _element = new NavElement();
        }

        [TearDown]
        public void TearDown()
        {
            if (_element == null)
                return;

            _element.Startup = new UIViewStartupSettings { OnStart = UIViewStartBehaviour.InstantHide };
            _element.PrepareForPlaySession();
            _element.Visibility.Dispose();
        }

        [TestCase((int)UIViewStartBehaviour.InstantHide, false)]
        [TestCase((int)UIViewStartBehaviour.AnimationShow, false)]
        [TestCase((int)UIViewStartBehaviour.InstantShow, true)]
        [TestCase((int)UIViewStartBehaviour.AnimationHide, true)]
        public void PrepareForPlaySessionAppliesStartupVisibility(int behaviour, bool expectedVisible)
        {
            _element.Startup = new UIViewStartupSettings { OnStart = (UIViewStartBehaviour)behaviour };

            _element.PrepareForPlaySession();

            Assert.That(_element.IsVisible, Is.EqualTo(expectedVisible));
            Assert.That(_element.style.display.value, Is.EqualTo(expectedVisible ? DisplayStyle.Flex : DisplayStyle.None));
        }

        [Test]
        public void InstantHideClosesGateBeforeApplyingCustomStartPosition()
        {
            _element.InstantShow();
            _element.Startup = new UIViewStartupSettings
            {
                OnStart = UIViewStartBehaviour.InstantHide,
                UseCustomStartPosition = true,
                CustomStartPosition = Vector3.zero
            };

            _element.PrepareForPlaySession();

            Translate translate = _element.style.translate.value;
            Assert.That(_element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_element.IsVisible, Is.False);
            Assert.That(translate.x.value, Is.Zero);
            Assert.That(translate.y.value, Is.Zero);
            Assert.That(translate.z, Is.Zero);
        }

        [Test]
        public void RepeatedPlaySessionPreparationRestoresConfiguredStartState()
        {
            _element.Startup = new UIViewStartupSettings { OnStart = UIViewStartBehaviour.InstantHide };

            _element.PrepareForPlaySession();
            _element.InstantShow();
            _element.PrepareForPlaySession();

            Assert.That(_element.IsVisible, Is.False);
            Assert.That(_element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ParentIsPreparedBeforeFollowingChild()
        {
            var child = new NavElement
            {
                Startup = new UIViewStartupSettings { OnStart = UIViewStartBehaviour.InstantShow }
            };
            _element.Startup = new UIViewStartupSettings { OnStart = UIViewStartBehaviour.InstantHide };
            _element.Add(child);

            try
            {
                _element.PrepareForPlaySession();
                child.PrepareForPlaySession();

                Assert.That(_element.IsVisible, Is.False);
                Assert.That(child.IsVisible, Is.False);
                Assert.That(child.style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                child.Startup = new UIViewStartupSettings { OnStart = UIViewStartBehaviour.InstantHide };
                child.PrepareForPlaySession();
                child.Visibility.Dispose();
                child.RemoveFromHierarchy();
            }
        }
    }
}
