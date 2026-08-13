using System;
using System.Collections.Generic;
using LitMotion;
using NKStudio.UITKNavigation.Animation;
using NKStudio.UITKNavigation.Elements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Animation
{
    /// <summary>
    /// Plays a Show or Hide transition on the selected NavElement from the inspector.
    /// </summary>
    /// <remarks>
    /// The preview state is process-wide: only one preview runs at a time no matter how many
    /// inspectors are open, and a domain reload clears it.
    /// </remarks>
    internal static class UITransitionPreviewController
    {
        private static ManualMotionDispatcher _previewDispatcher;
        private static IVisualElementScheduledItem _previewPump;
        private static NavElement _previewElement;
        private static UIAnimationType _previewType;
        private static Action _previewHideRestore;

        /// <summary>
        /// Gets the preview targets.
        /// </summary>
        private static readonly List<NavElement> PreviewTargets = new List<NavElement>();

        internal static void RunPreview(UIAnimationType type, Label hint)
        {
            if (!UITransitionSelectionResolver.TryGetInspectedNavElement(out NavElement element))
            {
                hint.text = "Select a NavElement on the canvas";
                return;
            }

            CancelPreview();
            hint.text = string.Empty;

            PreviewTargets.Clear();
            PreviewTargets.Add(element);
            NavElement.CollectFollowerElements(element, PreviewTargets);

            var dispatcher = new ManualMotionDispatcher();
            float duration = 0f;
            bool infinite = false;
            bool anyAnimation = false;

            for (int i = 0; i < PreviewTargets.Count; i++)
            {
                NavElement target = PreviewTargets[i];
                target.Visibility.Scheduler = dispatcher.Scheduler;

                UITransitionSet set = target.Transitions;
                UIAnimation animation = set == null
                    ? null
                    : type == UIAnimationType.Show ? set.BuildShow() : set.BuildHide();

                if (type == UIAnimationType.Show)
                    target.Visibility.ShowAnimation = animation;
                else
                    target.Visibility.HideAnimation = animation;

                if (animation == null)
                    continue;

                anyAnimation = true;
                duration = Mathf.Max(duration, animation.TotalDuration);
                infinite |= animation.IsInfinite;
            }

            if (!anyAnimation)
            {
                PreviewTargets.Clear();
                element.InstantShow();
                return;
            }

            _previewDispatcher = dispatcher;
            _previewElement = element;
            _previewType = type;

            if (type == UIAnimationType.Show)
            {
                element.Visibility.InstantHide();
                element.Visibility.Show();
            }
            else
            {
                element.InstantShow();

                UIViewVisibility visibility = element.Visibility;
                _previewHideRestore = () =>
                {
                    visibility.HideFinished -= _previewHideRestore;
                    _previewHideRestore = null;
                    RestoreHidePose();
                };
                visibility.HideFinished += _previewHideRestore;
                visibility.Hide();
            }

            PumpPreview(duration, infinite);
        }

        /// <summary>
        /// Performs the restore hide pose operation.
        /// </summary>
        private static void RestoreHidePose()
        {
            for (int i = 0; i < PreviewTargets.Count; i++)
            {
                NavElement target = PreviewTargets[i];
                if (target?.panel == null)
                    continue;

                UIViewVisibility visibility = target.Visibility;
                UIAnimation animation = visibility.HideAnimation;
                visibility.Gate.style.display = DisplayStyle.Flex;

                if (animation == null)
                    continue;

                animation.Prepare(visibility.Gate);
                animation.ApplyAt(visibility.Gate, animation.TotalDuration);
            }
        }

        /// <summary>
        /// Performs the pump preview operation.
        /// </summary>
        private static void PumpPreview(float duration, bool infinite)
        {
            VisualElement pumpTarget = _previewElement;
            if (pumpTarget?.panel == null)
                return;

            pumpTarget.MarkDirtyRepaint();

            float elapsed = 0f;
            double previousTime = EditorApplication.timeSinceStartup;
            ManualMotionDispatcher dispatcher = _previewDispatcher;
            IVisualElementScheduledItem item = null;
            item = pumpTarget.schedule.Execute(() =>
            {
                if (dispatcher != _previewDispatcher)
                {
                    item.Pause();
                    return;
                }

                double now = EditorApplication.timeSinceStartup;
                float delta = Mathf.Clamp((float)(now - previousTime), 0f, 0.05f);
                previousTime = now;

                elapsed += delta;
                dispatcher.Update(delta);

                if (!infinite && elapsed >= Mathf.Max(0f, duration))
                    FinishPreviewPump(dispatcher);
                
            }).Every(0);

            _previewPump = item;
        }

        private static void FinishPreviewPump(ManualMotionDispatcher dispatcher)
        {
            _previewPump?.Pause();
            _previewPump = null;
            dispatcher?.Reset();
            if (!ReferenceEquals(dispatcher, _previewDispatcher))
                return;

            if (_previewElement != null)
            {
                if (_previewType == UIAnimationType.Show)
                    _previewElement.Visibility.InstantShow();
            }
            _previewDispatcher = null;
        }

        private static void UnsubscribeHideRestore()
        {
            if (_previewHideRestore == null)
                return;

            if (_previewElement != null)
                _previewElement.Visibility.HideFinished -= _previewHideRestore;
            _previewHideRestore = null;
        }

        internal static void CancelPreview()
        {
            _previewPump?.Pause();
            _previewPump = null;
            _previewDispatcher?.Reset();
            _previewDispatcher = null;
            UnsubscribeHideRestore();

            if (_previewElement != null)
            {
                if (_previewElement.StartsHidden)
                    _previewElement.Visibility.InstantHide();
                else
                    _previewElement.Visibility.InstantShow();
            }

            PreviewTargets.Clear();
            _previewElement = null;
        }
    }
}
