using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Elements
{
    /// <summary>
    /// Provides an inspector-aligned label and a content container for custom UI Toolkit fields.
    /// </summary>
    /// <remarks>
    /// An empty <see cref="Label"/> drops the label entirely, so the content spans the whole row.
    /// </remarks>
    [UxmlElement]
    public partial class SimpleBaseField : BindableElement
    {
        private static readonly CustomStyleProperty<float> LabelWidthRatioProperty = new("--unity-property-field-label-width-ratio");

        private static readonly CustomStyleProperty<float> LabelExtraPaddingProperty = new("--unity-property-field-label-extra-padding");

        private static readonly CustomStyleProperty<float> LabelBaseMinWidthProperty = new("--unity-property-field-label-base-min-width");

        private static readonly CustomStyleProperty<float> LabelExtraContextWidthProperty = new("--unity-base-field-extra-context-width");

        private float _labelWidthRatio;
        private float _labelBaseMinWidth;
        private float _labelExtraPadding;
        private float _labelExtraContextWidth;

        private VisualElement _cachedContextWidthElement;
        private VisualElement _cachedInspectorElement;

        private readonly Label _label;
        private readonly VisualElement _container;

        /// <summary>
        /// Gets or sets the text displayed by the field label. An empty value hides the label.
        /// </summary>
        [UxmlAttribute("label")]
        public string Label
        {
            get => _label.text;
            set
            {
                _label.text = value ?? string.Empty;
                UpdateLabelVisibility();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleBaseField"/> class.
        /// </summary>
        public SimpleBaseField()
        {
            _label = new Label();
            _container = new VisualElement
            {
                name = "unity-content"
            };

            style.flexDirection = FlexDirection.Row;
            _container.style.flexDirection = FlexDirection.Row;
            _container.style.alignItems = Align.Center;

            _label.style.marginRight = 0;
            SetupUnityBaseTextFieldWithoutColor(_container);

            _label.text = "Label";

            AddToClassList("unity-base-field__aligned");
            AddToClassList("unity-base-field");
            AddToClassList("unity-base-field__inspector-field");
            AddToClassList("unity-base-text-field");
            AddToClassList("unity-text-field");

            _label.AddToClassList("unity-label");
            _label.AddToClassList("unity-text-element");
            _label.AddToClassList("unity-text-field__label");
            _label.AddToClassList("unity-base-field__label");
            _label.AddToClassList("unity-base-text-field__label");

            _container.AddToClassList("unity-base-field__input");
            _container.AddToClassList("unity-base-text-field__input--single-line");

            _container.style.paddingLeft = 0;
            _container.style.paddingRight = 0;
            _container.style.borderLeftWidth = 0;
            _container.style.borderRightWidth = 0;

            hierarchy.Add(_label);
            hierarchy.Add(_container);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <inheritdoc />
        public override VisualElement contentContainer => _container;

        /// <summary>
        /// Gets the label element, so callers can style it like a built-in field label.
        /// </summary>
        public Label LabelElement => _label;

        // Without a label there is nothing to align, so the aligned class is dropped as well.
        private void UpdateLabelVisibility()
        {
            bool hasLabel = !string.IsNullOrEmpty(_label.text);
            _label.style.display = hasLabel ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("unity-base-field__aligned", hasLabel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (evt.destinationPanel == null || evt.destinationPanel.contextType == ContextType.Player)
                return;

            _cachedInspectorElement = null;
            _cachedContextWidthElement = null;

            for (VisualElement parent = this.parent; parent != null; parent = parent.parent)
            {
                if (parent.ClassListContains("unity-inspector-element"))
                    _cachedInspectorElement = parent;
                if (parent.ClassListContains("unity-inspector-main-container"))
                {
                    _cachedContextWidthElement = parent;
                    break;
                }
            }

            if (_cachedInspectorElement == null)
                return;

            _labelWidthRatio = 0.45f;
            _labelExtraPadding = 37f;
            _labelBaseMinWidth = 123f;
            _labelExtraContextWidth = 1f;

            UnregisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            UnregisterCallback<GeometryChangedEvent>(OnInspectorFieldGeometryChanged);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            RegisterCallback<GeometryChangedEvent>(OnInspectorFieldGeometryChanged);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            UnregisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            UnregisterCallback<GeometryChangedEvent>(OnInspectorFieldGeometryChanged);
            _cachedInspectorElement = null;
            _cachedContextWidthElement = null;
        }

        private void AlignLabel()
        {
            if (_cachedInspectorElement == null || !ClassListContains("unity-base-field__aligned"))
                return;

            float labelExtraPadding = _labelExtraPadding;
            Rect worldBound = this.worldBound;
            double x1 = worldBound.x;
            worldBound = _cachedInspectorElement.worldBound;
            double x2 = worldBound.x;
            float num1 = (float)(x1 - x2) - _cachedInspectorElement.resolvedStyle.paddingLeft;
            float num2 = labelExtraPadding + num1 + resolvedStyle.paddingLeft;
            float a = _labelBaseMinWidth - num1 - resolvedStyle.paddingLeft;
            VisualElement visualElement = _cachedContextWidthElement ?? _cachedInspectorElement;
            _label.style.minWidth = Mathf.Max(a, 0.0f);
            float b = (visualElement.resolvedStyle.width + _labelExtraContextWidth) * _labelWidthRatio - num2;
            if (Mathf.Abs(_label.resolvedStyle.width - b) <= 1.0000000031710769E-30)
                return;

            _label.style.width = Mathf.Max(0.0f, b);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            if (evt.customStyle.TryGetValue(LabelWidthRatioProperty, out var num1))
                _labelWidthRatio = num1;
            if (evt.customStyle.TryGetValue(LabelExtraPaddingProperty, out var num2))
                _labelExtraPadding = num2;
            if (evt.customStyle.TryGetValue(LabelBaseMinWidthProperty, out var num3))
                _labelBaseMinWidth = num3;
            if (evt.customStyle.TryGetValue(LabelExtraContextWidthProperty, out var num4))
                _labelExtraContextWidth = num4;

            AlignLabel();
        }

        private void OnInspectorFieldGeometryChanged(GeometryChangedEvent evt) => AlignLabel();

        private static void SetupUnityBaseTextFieldWithoutColor(VisualElement container)
        {
            container.style.paddingLeft = 2;
            container.style.paddingRight = 2;
            container.style.paddingTop = 1;
            container.style.paddingBottom = 0;
            container.style.flexShrink = 1;
            container.style.flexGrow = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
        }
    }
}
