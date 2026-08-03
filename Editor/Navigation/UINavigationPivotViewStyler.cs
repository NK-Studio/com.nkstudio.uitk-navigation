using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Provides UI Navigation Pivot View Styler functionality.
    /// </summary>
    internal static class UINavigationPivotViewStyler
    {
        private const string NodeClass = "ge-node";
        private const string NodeOptionsClass = "ge-node__node-options";
        private const string PortClass = "ge-port";
        private const string InputPortClass = "ge-port--direction-input";
        private const string OutputPortClass = "ge-port--direction-output";
        private const string RightClass = "pivot--right";
        private const string DownClass = "pivot--down";
        private const string LeftClass = "pivot--left";
        private const string UpClass = "pivot--up";
        private const string RotateButtonName = "pivot-rotate-button";
        private const string RotateButtonClass = "pivot__rotate";

        private const string RotateIconPath =
            "Packages/com.nkstudio.uitk-navigation/Editor/Assets/RefreshIcon.svg";

        private static readonly int RotationCount = Enum.GetValues(typeof(UINavigationPivotRotation)).Length;

        private const double SyncInterval = 0.1d;

        private static readonly List<VisualElement> Nodes = new();
        private static readonly List<VisualElement> Wires = new();
        private static readonly Dictionary<object, VisualElement> PortViewsByModel = new();

        private static readonly HashSet<VisualElement> ReversedInputs = new();
        private static readonly HashSet<VisualElement> ReversedOutputs = new();
        private static readonly HashSet<VisualElement> VerticalInputs = new();
        private static readonly HashSet<VisualElement> VerticalOutputs = new();

        private static readonly HashSet<VisualElement> Hooked = new();
        private static readonly List<VisualElement> Detached = new();
        private static readonly HashSet<VisualElement> HookedPorts = new();
        private static readonly List<VisualElement> DetachedPorts = new();

        private static double _nextSyncTime;
        private static bool _wireReflectionFailed;
        private static bool _applying;

        private static FieldInfo _fromPortField;
        private static FieldInfo _toPortField;
        private static PropertyInfo _wireControlProperty;
        private static PropertyInfo _fromDirectionProperty;
        private static PropertyInfo _toDirectionProperty;
        private static PropertyInfo _fromOrientationProperty;
        private static PropertyInfo _toOrientationProperty;
        private static PropertyInfo _wireModelProperty;
        private static PropertyInfo _wireModelFromPortProperty;
        private static PropertyInfo _wireModelToPortProperty;
        private static PropertyInfo _portModelProperty;
        private static PropertyInfo _portModelOrientationProperty;
        private static MethodInfo _updateLayoutMethod;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= Sync;
            EditorApplication.update += Sync;
        }

        private static void Sync()
        {
            if (EditorApplication.timeSinceStartup < _nextSyncTime)
                return;

            _nextSyncTime = EditorApplication.timeSinceStartup + SyncInterval;

            try
            {
                ReversedInputs.Clear();
                ReversedOutputs.Clear();
                VerticalInputs.Clear();
                VerticalOutputs.Clear();
                PortViewsByModel.Clear();

                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (!IsGraphWindow(window))
                        continue;

                    VisualElement root = window.rootVisualElement;
                    if (root == null)
                        continue;

                    Nodes.Clear();
                    Wires.Clear();
                    Collect(root);

                    foreach (VisualElement node in Nodes)
                        Apply(node);

                    if (!_wireReflectionFailed)
                        ApplyWires();
                }

                ForgetDetachedHooks();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                Nodes.Clear();
                Wires.Clear();
            }
        }

        private static bool IsGraphWindow(EditorWindow window)
        {
            string name = window == null ? null : window.GetType().FullName;
            return name != null && name.Contains("GraphViewEditorWindow");
        }

        private static void Apply(VisualElement node)
        {
            VisualElement options = node.Q(className: NodeOptionsClass);
            EnumField field = options?.Q<EnumField>();
            if (field?.value is not UINavigationPivotRotation rotation)
                return;

            bool reversed = UINavigationPivotNode.IsReversed(rotation);
            bool vertical = UINavigationPivotNode.IsVertical(rotation);

            if (vertical)
            {
                node.style.width = 30f;
                node.style.height = 68f;
            }
            else
            {
                node.style.width = 68f;
                node.style.height = 30f;
            }

            EnsureRotateButton(node, options, field);
            PlaceRotateButton(node);

            node.EnableInClassList(RightClass, rotation == UINavigationPivotRotation.Right);
            node.EnableInClassList(DownClass, rotation == UINavigationPivotRotation.Down);
            node.EnableInClassList(LeftClass, rotation == UINavigationPivotRotation.Left);
            node.EnableInClassList(UpClass, rotation == UINavigationPivotRotation.Up);

            node.Query(className: PortClass).ForEach(port =>
            {
                EnsureHorizontalPortOrientation(port);

                if (port.ClassListContains(InputPortClass))
                {
                    if (reversed)
                        ReversedInputs.Add(port);
                    if (vertical)
                        VerticalInputs.Add(port);
                }
                else if (port.ClassListContains(OutputPortClass))
                {
                    if (reversed)
                        ReversedOutputs.Add(port);
                    if (vertical)
                        VerticalOutputs.Add(port);
                }
            });
        }

        private static void EnsureHorizontalPortOrientation(VisualElement port)
        {
            if (!EnsurePortReflection(port))
                return;

            object portModel = _portModelProperty.GetValue(port);
            if (portModel == null)
                return;

            object orientation = Enum.Parse(
                _portModelOrientationProperty.PropertyType,
                "Horizontal");

            if (!Equals(_portModelOrientationProperty.GetValue(portModel), orientation))
                _portModelOrientationProperty.SetValue(portModel, orientation);
        }

        private static bool EnsurePortReflection(VisualElement port)
        {
            if (_portModelOrientationProperty != null)
                return true;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            _portModelProperty = port.GetType().GetProperty("PortModel", Flags);
            if (_portModelProperty == null)
                return false;

            _portModelOrientationProperty =
                _portModelProperty.PropertyType.GetProperty("Orientation", Flags);

            return _portModelOrientationProperty?.CanWrite == true;
        }

        private static void OnPortMouseMove(MouseMoveEvent evt)
        {
            if (evt.currentTarget is not VisualElement port)
                return;

            RecalculateWires(GetRoot(port));
        }

        private static void OnPortMouseUp(MouseUpEvent evt)
        {
            if (evt.button != 0 || evt.currentTarget is not VisualElement port)
                return;

            VisualElement root = GetRoot(port);

            RecalculateWires(root);

            root.schedule.Execute(() => RecalculateWires(root)).ExecuteLater(0);
            root.schedule.Execute(() => RecalculateWires(root)).ExecuteLater(16);
        }

        private static VisualElement GetRoot(VisualElement element)
        {
            while (element.parent != null)
                element = element.parent;

            return element;
        }

        private static void RecalculateWires(VisualElement root)
        {
            if (_applying || _wireReflectionFailed || root?.panel == null)
                return;

            _applying = true;

            try
            {
                ApplyDraggedWires(root);
            }
            catch (Exception exception)
            {
                _wireReflectionFailed = true;
                Debug.LogException(exception);
            }
            finally
            {
                Wires.Clear();
                _applying = false;
            }
        }

        private static void ApplyDraggedWires(VisualElement root)
        {
            Wires.Clear();
            CollectDragViews(root);
            if (Wires.Count == 0)
                return;

            if (!EnsureWireReflection(Wires[0]))
            {
                _wireReflectionFailed = true;
                return;
            }

            foreach (VisualElement wire in Wires)
            {
                if (_wireControlProperty.GetValue(wire) is VisualElement control)
                    Fix(wire, control);
            }
        }

        private static void CollectDragViews(VisualElement element)
        {
            if (element.GetType().Name == "WireView")
                Wires.Add(element);

            if (element.ClassListContains(PortClass))
                CachePortView(element);

            foreach (VisualElement child in element.Children())
                CollectDragViews(child);
        }

        /// <summary>
        /// Performs the ensure rotate button operation.
        /// </summary>
        private static void EnsureRotateButton(VisualElement node, VisualElement options, EnumField field)
        {
            if (node.Q(name: RotateButtonName) != null)
                return;

            var button = new VisualElement
            {
                name = RotateButtonName,
                tooltip = "누를 때마다 포트 배치가 시계방향으로 회전합니다.",
            };

            button.AddToClassList(RotateButtonClass);

            button.style.unityBackgroundImageTintColor = EditorGUIUtility.isProSkin
                ? new Color(0.78f, 0.78f, 0.78f)
                : new Color(0.25f, 0.25f, 0.25f);

            var icon = AssetDatabase.LoadAssetAtPath<VectorImage>(RotateIconPath);
            if (icon != null)
                button.style.backgroundImage = new StyleBackground(icon);

            button.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                Advance(field);
            });

            options.Insert(0, button);
        }

        /// <summary>
        /// Performs the place rotate button operation.
        /// </summary>
        private static void PlaceRotateButton(VisualElement node)
        {
            VisualElement button = node.Q(name: RotateButtonName);
            if (button == null)
                return;

            if (button.parent == node)
                return;

            node.Add(button);
        }

        private static void Advance(EnumField field)
        {
            if (field.value is not UINavigationPivotRotation rotation)
                return;

            field.value = (UINavigationPivotRotation)(((int)rotation + 1) % RotationCount);
        }

        /// <summary>
        /// Applies w ir es.
        /// </summary>
        private static void ApplyWires()
        {
            if (Wires.Count == 0)
                return;

            if (!EnsureWireReflection(Wires[0]))
            {
                _wireReflectionFailed = true;
                return;
            }

            foreach (VisualElement wire in Wires)
            {
                if (_wireControlProperty.GetValue(wire) is not VisualElement control)
                    continue;

                Fix(wire, control);

                if (Hooked.Add(control))
                    control.RegisterCallback<GeometryChangedEvent>(OnWireGeometryChanged);
            }
        }

        private static void OnWireGeometryChanged(GeometryChangedEvent evt)
        {
            if (_applying || _wireReflectionFailed)
                return;

            if (evt.target is not VisualElement control || control.parent == null)
                return;

            _applying = true;

            try
            {
                Fix(control.parent, control);
            }
            catch (Exception exception)
            {
                _wireReflectionFailed = true;
                Debug.LogException(exception);
            }
            finally
            {
                _applying = false;
            }
        }

        private static void Fix(VisualElement wire, VisualElement control)
        {
            VisualElement fromPort = ResolvePortView(wire, _fromPortField, _wireModelFromPortProperty);
            VisualElement toPort = ResolvePortView(wire, _toPortField, _wireModelToPortProperty);

            PortDirection from = GetFromDirection(fromPort);
            PortDirection to = GetToDirection(toPort);

            if (fromPort == null && toPort != null)
                from = Reverse(to);
            if (toPort == null && fromPort != null)
                to = Reverse(from);

            bool fromVertical = IsVerticalPort(fromPort) ||
                                fromPort == null && IsVerticalPort(toPort);
            bool toVertical = IsVerticalPort(toPort) ||
                              toPort == null && IsVerticalPort(fromPort);

            object fromOrientation = Enum.Parse(
                _fromOrientationProperty.PropertyType,
                fromVertical ? "Vertical" : "Horizontal");

            object toOrientation = Enum.Parse(
                _toOrientationProperty.PropertyType,
                toVertical ? "Vertical" : "Horizontal");

            bool changed = false;
            if (!Equals(_fromDirectionProperty.GetValue(control), from))
            {
                _fromDirectionProperty.SetValue(control, from);
                changed = true;
            }

            if (!Equals(_toDirectionProperty.GetValue(control), to))
            {
                _toDirectionProperty.SetValue(control, to);
                changed = true;
            }

            if (!Equals(_fromOrientationProperty.GetValue(control), fromOrientation))
            {
                _fromOrientationProperty.SetValue(control, fromOrientation);
                changed = true;
            }

            if (!Equals(_toOrientationProperty.GetValue(control), toOrientation))
            {
                _toOrientationProperty.SetValue(control, toOrientation);
                changed = true;
            }

            if (changed)
                _updateLayoutMethod?.Invoke(control, null);
        }

        private static PortDirection GetFromDirection(VisualElement port)
        {
            return port != null && ReversedOutputs.Contains(port)
                ? PortDirection.Input
                : PortDirection.Output;
        }

        private static PortDirection GetToDirection(VisualElement port)
        {
            return port != null && ReversedInputs.Contains(port)
                ? PortDirection.Output
                : PortDirection.Input;
        }

        private static PortDirection Reverse(PortDirection direction)
        {
            return direction == PortDirection.Input
                ? PortDirection.Output
                : PortDirection.Input;
        }

        private static bool IsVerticalPort(VisualElement port)
        {
            return port != null &&
                   (VerticalInputs.Contains(port) || VerticalOutputs.Contains(port));
        }

        private static VisualElement ResolvePortView(
            VisualElement wire,
            FieldInfo cachedPortField,
            PropertyInfo modelPortProperty)
        {
            object wireModel = _wireModelProperty?.GetValue(wire);
            if (wireModel != null && modelPortProperty != null)
            {
                object portModel = modelPortProperty.GetValue(wireModel);
                if (portModel == null)
                    return null;

                if (PortViewsByModel.TryGetValue(portModel, out VisualElement portView))
                    return portView;

                return cachedPortField?.GetValue(wire) as VisualElement;
            }

            return cachedPortField?.GetValue(wire) as VisualElement;
        }

        private static void CachePortView(VisualElement port)
        {
            if (!EnsurePortReflection(port))
                return;

            object portModel = _portModelProperty.GetValue(port);
            if (portModel != null)
                PortViewsByModel[portModel] = port;
        }

        /// <summary>
        /// Performs the forget detached hooks operation.
        /// </summary>
        private static void ForgetDetachedHooks()
        {
            foreach (VisualElement control in Hooked)
            {
                if (control.panel == null)
                    Detached.Add(control);
            }

            foreach (VisualElement control in Detached)
            {
                control.UnregisterCallback<GeometryChangedEvent>(OnWireGeometryChanged);
                Hooked.Remove(control);
            }

            Detached.Clear();

            foreach (VisualElement port in HookedPorts)
            {
                if (port.panel == null)
                    DetachedPorts.Add(port);
            }

            foreach (VisualElement port in DetachedPorts)
            {
                port.UnregisterCallback<MouseMoveEvent>(OnPortMouseMove);
                port.UnregisterCallback<MouseUpEvent>(OnPortMouseUp);
                HookedPorts.Remove(port);
            }

            DetachedPorts.Clear();
        }

        private static bool EnsureWireReflection(VisualElement wire)
        {
            if (_toDirectionProperty != null)
                return true;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            Type wireType = wire.GetType();
            _fromPortField = wireType.GetField("m_LastUsedFromPort", Flags);
            _toPortField = wireType.GetField("m_LastUsedToPort", Flags);
            _wireControlProperty = wireType.GetProperty("WireControl", Flags);
            _wireModelProperty = wireType.GetProperty("WireModel", Flags);
            if (_fromPortField == null ||
                _toPortField == null ||
                _wireControlProperty == null ||
                _wireModelProperty == null)
                return false;

            Type wireModelType = _wireModelProperty.PropertyType;
            _wireModelFromPortProperty = wireModelType.GetProperty("FromPort", Flags);
            _wireModelToPortProperty = wireModelType.GetProperty("ToPort", Flags);

            Type controlType = _wireControlProperty.PropertyType;
            _fromDirectionProperty = controlType.GetProperty("FromDirection", Flags);
            _toDirectionProperty = controlType.GetProperty("ToDirection", Flags);
            _fromOrientationProperty = controlType.GetProperty("FromOrientation", Flags);
            _toOrientationProperty = controlType.GetProperty("ToOrientation", Flags);
            _updateLayoutMethod = controlType.GetMethod("UpdateLayout", Flags, null, Type.EmptyTypes, null);
            return _fromDirectionProperty != null &&
                   _toDirectionProperty != null &&
                   _fromOrientationProperty != null &&
                   _toOrientationProperty != null &&
                   _wireModelFromPortProperty != null &&
                   _wireModelToPortProperty != null &&
                   _fromDirectionProperty.CanWrite &&
                   _toDirectionProperty.CanWrite &&
                   _fromOrientationProperty.CanWrite &&
                   _toOrientationProperty.CanWrite;
        }

        private static void Collect(VisualElement element)
        {
            if (element.ClassListContains(NodeClass))
                Nodes.Add(element);
            else if (element.GetType().Name == "WireView")
                Wires.Add(element);

            if (element.ClassListContains(PortClass) && HookedPorts.Add(element))
            {
                element.RegisterCallback<MouseMoveEvent>(OnPortMouseMove);
                element.RegisterCallback<MouseUpEvent>(OnPortMouseUp);
            }

            if (element.ClassListContains(PortClass))
                CachePortView(element);

            foreach (VisualElement child in element.Children())
                Collect(child);
        }
    }
}
