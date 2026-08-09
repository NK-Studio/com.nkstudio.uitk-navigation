using System;
using System.Collections.Generic;
using LitMotion;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Provides UI Animator functionality.
    /// </summary>
    internal sealed class UIAnimator : IDisposable
    {
        private static readonly StyleList<TimeValue> SuppressedTransitionDuration =
            new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0f) });

        private readonly List<UIAnimationBinding> _bindings = new List<UIAnimationBinding>();
        private readonly List<TransitionOverride> _transitionOverrides = new List<TransitionOverride>();
        private readonly HashSet<VisualElement> _overriddenElements = new HashSet<VisualElement>();
        private int _transitionOverrideCount;

        private MotionHandle _handle;
        private Action _onComplete;
        private int _generation;
        private float _totalDuration;
        private IMotionScheduler _scheduler = MotionScheduler.UpdateRealtime;

        /// <summary>
        /// Gets or sets the progress.
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// Gets the inverse progress.
        /// </summary>
        public float InverseProgress => 1f - Progress;

        /// <summary>
        /// Gets the scheduler.
        /// </summary>
        public IMotionScheduler Scheduler
        {
            get => _scheduler;
            set => _scheduler = value ?? MotionScheduler.UpdateRealtime;
        }

        /// <summary>
        /// Determines whether active.
        /// </summary>
        public bool IsPlaying => _handle.IsActive();

        /// <summary>
        /// Performs the play operation.
        /// </summary>
        public void Play(UIAnimationBinding binding, float startProgress, Action onComplete)
        {
            Stop();
            _bindings.Clear();

            if (binding.IsValid)
                _bindings.Add(binding);

            StartMotion(startProgress, onComplete);
        }

        /// <summary>
        /// Performs the play operation.
        /// </summary>
        public void Play(IReadOnlyList<UIAnimationBinding> bindings, float startProgress, Action onComplete)
        {
            Stop();
            _bindings.Clear();

            if (bindings != null)
            {
                for (int i = 0; i < bindings.Count; i++)
                {
                    if (bindings[i].IsValid)
                        _bindings.Add(bindings[i]);
                }
            }

            StartMotion(startProgress, onComplete);
        }

        /// <summary>
        /// Performs the stop operation.
        /// </summary>
        public void Stop()
        {
            _generation++;
            _onComplete = null;
            RestoreTransitions();

            if (_handle.IsActive())
                _handle.Cancel();

            _handle = default;
        }

        /// <summary>
        /// Performs the stop and clear styles operation.
        /// </summary>
        public void StopAndClearStyles(bool forceClearOpacity)
        {
            Stop();
            ClearStyles(forceClearOpacity);
        }

        /// <summary>
        /// Clears s ty le s.
        /// </summary>
        public void ClearStyles(bool forceClearOpacity)
        {
            for (int i = 0; i < _bindings.Count; i++)
                _bindings[i].ClearStyles(forceClearOpacity);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            StopAndClearStyles(true);
            _bindings.Clear();
        }

        private void StartMotion(float startProgress, Action onComplete)
        {
            SuppressTransitions();
            _totalDuration = 0f;
            bool hasChannel = false;
            bool infinite = false;

            for (int i = 0; i < _bindings.Count; i++)
            {
                _bindings[i].Prepare();
                _totalDuration = Mathf.Max(_totalDuration, _bindings[i].TotalDuration);
                hasChannel |= _bindings[i].Animation.HasEnabledChannel;
                infinite |= _bindings[i].Animation.IsInfinite;
            }

            startProgress = Mathf.Clamp01(startProgress);
            float remaining = infinite
                ? _totalDuration
                : _totalDuration * (1f - startProgress);

            if (_bindings.Count == 0 || !hasChannel || remaining <= 0f)
            {
                ApplyProgress(1f);
                onComplete?.Invoke();
                return;
            }

            _onComplete = onComplete;
            int generation = _generation;

            var builder = LMotion.Create(startProgress, 1f, remaining)
                .WithEase(Ease.Linear)
                .WithScheduler(_scheduler)
                .WithImmediateBind();

            if (infinite)
                builder = builder.WithLoops(-1, LoopType.Restart);
            else
                builder = builder.WithOnComplete(() => OnMotionComplete(generation));

            _handle = builder.Bind(
                this,
                static (progress, animator) => animator.ApplyProgress(progress));
        }

        private void SuppressTransitions()
        {
            RestoreTransitions();
            for (int i = 0; i < _bindings.Count; i++)
            {
                VisualElement element = _bindings[i].Element;
                if (element == null || !_overriddenElements.Add(element))
                    continue;

                TransitionOverride transitionOverride;
                if (_transitionOverrideCount < _transitionOverrides.Count)
                {
                    transitionOverride = _transitionOverrides[_transitionOverrideCount];
                }
                else
                {
                    transitionOverride = new TransitionOverride();
                    _transitionOverrides.Add(transitionOverride);
                }

                transitionOverride.Capture(element, element.style.transitionDuration);
                _transitionOverrideCount++;
                element.style.transitionDuration = SuppressedTransitionDuration;
            }
        }

        private void RestoreTransitions()
        {
            for (int i = 0; i < _transitionOverrideCount; i++)
            {
                TransitionOverride item = _transitionOverrides[i];
                if (item.Element != null)
                    item.Restore();
            }
            _transitionOverrideCount = 0;
            _overriddenElements.Clear();
        }

        private sealed class TransitionOverride
        {
            private readonly List<TimeValue> _values = new List<TimeValue>();
            private StyleKeyword _keyword;

            public VisualElement Element { get; private set; }

            public void Capture(VisualElement element, StyleList<TimeValue> duration)
            {
                Element = element;
                _keyword = duration.keyword;
                _values.Clear();
                if (duration.value != null)
                    _values.AddRange(duration.value);
            }

            public void Restore()
            {
                Element.style.transitionDuration =
                    _keyword == StyleKeyword.Undefined
                        ? new StyleList<TimeValue>(_values)
                        : new StyleList<TimeValue>(_keyword);
                Element = null;
            }
        }
        private void ApplyProgress(float progress)
        {
            Progress = progress;
            float time = progress * _totalDuration;

            for (int i = 0; i < _bindings.Count; i++)
                _bindings[i].ApplyAt(time);
        }

        private void OnMotionComplete(int generation)
        {
            if (generation != _generation)
                return;

            ApplyProgress(1f);
            RestoreTransitions();

            Action callback = _onComplete;
            _onComplete = null;
            _handle = default;
            callback?.Invoke();
        }
    }
}
