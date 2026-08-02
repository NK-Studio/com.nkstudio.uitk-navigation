using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using UnityEngine;

namespace NKStudio.UITKNavigation.Navigation
{
    internal enum UINavigationTriggerKind
    {
        [InspectorName("UI Button")]
        UIButton,
        [InspectorName("Time Delay")]
        TimeDelay,
        Toggle,
        Signal,
        [InspectorName("UI View")]
        UIView,
        Random
    }

    [Serializable]
    internal sealed class UINavigationTransition
    {
        [SerializeField]
        private UIKey signal;

        [SerializeField]
        private UINavigationTriggerKind triggerKind;

        [SerializeField]
        private float delaySeconds;

        [SerializeField]
        private bool toggleValue;

        [SerializeField]
        private UIToggleOutputCondition toggleCondition;

        [SerializeField]
        private UIViewOutputCondition viewCondition;

        [SerializeField]
        private float weight = 100f;

        [SerializeField]
        private string targetNodeId;

        [SerializeField]
        private UINavigationTransitionKind kind = UINavigationTransitionKind.Push;

        [SerializeField]
        private UINavigationAction[] actions = Array.Empty<UINavigationAction>();

        /// <summary>
        /// Gets the source port name.
        /// </summary>
        [SerializeField]
        private string sourcePortName;

        public UINavigationTransition(
            UIKey signal,
            string targetNodeId,
            UINavigationTransitionKind kind)
            : this(signal, targetNodeId, kind, Array.Empty<UINavigationAction>())
        {
        }

        public UINavigationTransition(
            UIKey signal,
            string targetNodeId,
            UINavigationTransitionKind kind,
            UINavigationAction[] actions)
            : this(
                UINavigationTriggerKind.Signal,
                signal,
                0f,
                false,
                targetNodeId,
                kind,
                actions)
        {
        }

        public UINavigationTransition(
            UINavigationTriggerKind triggerKind,
            UIKey signal,
            float delaySeconds,
            bool toggleValue,
            string targetNodeId,
            UINavigationTransitionKind kind,
            UINavigationAction[] actions)
            : this(
                triggerKind,
                signal,
                delaySeconds,
                toggleValue
                    ? UIToggleOutputCondition.On
                    : UIToggleOutputCondition.Off,
                UIViewOutputCondition.Show,
                100f,
                targetNodeId,
                kind,
                actions)
        {
        }

        public UINavigationTransition(
            UINavigationTriggerKind triggerKind,
            UIKey signal,
            float delaySeconds,
            UIToggleOutputCondition toggleCondition,
            UIViewOutputCondition viewCondition,
            float weight,
            string targetNodeId,
            UINavigationTransitionKind kind,
            UINavigationAction[] actions,
            string sourcePortName = null)
        {
            this.triggerKind = triggerKind;
            this.signal = signal;
            this.delaySeconds = delaySeconds;
            this.toggleCondition = toggleCondition;
            toggleValue = toggleCondition == UIToggleOutputCondition.On;
            this.viewCondition = viewCondition;
            this.weight = weight;
            this.targetNodeId = targetNodeId;
            this.kind = kind;
            this.actions = actions ?? Array.Empty<UINavigationAction>();
            this.sourcePortName = sourcePortName;
        }

        public UINavigationTriggerKind TriggerKind => triggerKind;
        public UIKey Signal => signal;
        public float DelaySeconds => delaySeconds;
        public bool ToggleValue => toggleValue;
        public UIToggleOutputCondition ToggleCondition => toggleCondition;
        public UIViewOutputCondition ViewCondition => viewCondition;
        public float Weight => weight;
        public string TargetNodeId => targetNodeId;
        public UINavigationTransitionKind Kind => kind;
        public IReadOnlyList<UINavigationAction> Actions => actions;

        /// <summary>
        /// Gets the source port name.
        /// </summary>
        public string SourcePortName => sourcePortName;

        public bool MatchesToggle(bool value)
        {
            return toggleCondition == UIToggleOutputCondition.Any ||
                   toggleCondition == (value
                       ? UIToggleOutputCondition.On
                       : UIToggleOutputCondition.Off);
        }
    }
}
