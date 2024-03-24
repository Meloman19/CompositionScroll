using Avalonia;
using Avalonia.Layout;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Server;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompositionScroll
{
    public class CompositionEffectiveViewportManager
    {
        private static Harmony _harmony;
        private static List<EffectiveViewportListener> _effectiveViewportListeners = new();

        public static readonly AttachedProperty<bool> EffectiveViewportRootProperty
            = AvaloniaProperty.RegisterAttached<CompositionEffectiveViewportManager, Visual, bool>("EffectiveViewportRoot", defaultValue: false);

        public static void SetEffectiveViewportRoot(Visual element, bool value)
        {
            element.SetValue(EffectiveViewportRootProperty, value);
        }

        public static bool GetEffectiveViewportRoot(Visual element)
        {
            return element.GetValue(EffectiveViewportRootProperty);
        }

        public static void Init()
        {
            var methods = typeof(LayoutManager).GetInterfaceMap(typeof(ILayoutManager)).TargetMethods;
            var registerMethod = methods.First(m => m.Name == "Avalonia.Layout.ILayoutManager.RegisterEffectiveViewportListener");
            var unregisterMethod = methods.First(m => m.Name == "Avalonia.Layout.ILayoutManager.UnregisterEffectiveViewportListener");

            _harmony = new Harmony(nameof(CompositionEffectiveViewportManager));

            var replacementRegister = typeof(CompositionEffectiveViewportManager).GetMethod(nameof(RegisterEffectiveViewportListener), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var replacementUnregister = typeof(CompositionEffectiveViewportManager).GetMethod(nameof(UnregisterEffectiveViewportListener), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            _harmony.Patch(registerMethod, prefix: new HarmonyMethod(replacementRegister));
            _harmony.Patch(unregisterMethod, prefix: new HarmonyMethod(replacementUnregister));
        }

        private static bool RegisterEffectiveViewportListener(Layoutable control)
        {
            var listener = new EffectiveViewportListener(control);
            listener.Init();
            _effectiveViewportListeners.Add(listener);
            return false;
        }

        private static bool UnregisterEffectiveViewportListener(Layoutable control)
        {
            for (var i = _effectiveViewportListeners.Count - 1; i >= 0; --i)
            {
                if (_effectiveViewportListeners[i].Layoutable == control)
                {
                    _effectiveViewportListeners[i].Dispose();
                    _effectiveViewportListeners.RemoveAt(i);
                }
            }
            return false;
        }

        private class EffectiveViewportListener : CompositionObject
        {
            private Visual _root;

            public EffectiveViewportListener(Layoutable layoutable)
                : base(layoutable.CompositionVisual.Compositor, new ServerEffectiveViewportListener(layoutable.CompositionVisual.Server))
            {
                Layoutable = layoutable;
            }

            public Layoutable Layoutable { get; }

            internal void Init()
            {
                var server = Server as ServerEffectiveViewportListener;
                server.Init(this);
                UpdateRoot();
            }

            private void UpdateRoot()
            {
                var root = Layoutable.GetVisualAncestors().FirstOrDefault(GetEffectiveViewportRoot);
                if (root == _root)
                    return;

                _root = root;
                (Server as ServerEffectiveViewportListener)?.SetRoot(_root.CompositionVisual.Server);
            }

            private readonly object _locker = new();
            private Rect? _viewport;

            public void UpdateViewport(Rect viewport)
            {
                bool dispatch = false;
                lock (_locker)
                {
                    dispatch = _viewport == null;
                    _viewport = viewport;
                }

                if (dispatch)
                    Compositor.Dispatcher.InvokeAsync(UpdateViewportUI, DispatcherPriority.Input);
            }

            private void UpdateViewportUI()
            {
                Rect viewport;
                lock (_locker)
                {
                    viewport = _viewport.Value;
                    _viewport = null;
                }

                Layoutable.RaiseEffectiveViewportChanged(new EffectiveViewportChangedEventArgs(viewport));
            }
        }

        private class ServerEffectiveViewportListener : ServerObject, IDisposable, IServerClockItem
        {
            private EffectiveViewportListener _listener;
            private ServerCompositionVisual _targetVisual;
            private ServerCompositionVisual _root;
            private Rect _viewport;

            public ServerEffectiveViewportListener(ServerCompositionVisual visual)
                : base(visual.Compositor)
            {
                _targetVisual = visual;
            }

            public void Init(EffectiveViewportListener listener)
            {
                _listener = listener;
                Compositor.AddToClock(this);
            }

            public void Dispose()
            {
                Compositor.RemoveFromClock(this);
            }

            public void OnTick()
            {
                var newViewport = CalculateEffectiveViewport(_targetVisual, _root);
                if (_viewport == newViewport)
                    return;

                _viewport = newViewport;
                _listener.UpdateViewport(_viewport);
            }

            public void SetRoot(ServerCompositionVisual root)
            {
                _root = root;
            }

            private static Rect CalculateEffectiveViewport(ServerCompositionVisual control, ServerCompositionVisual root)
            {
                var viewport = new Rect(0, 0, double.PositiveInfinity, double.PositiveInfinity);
                CalculateEffectiveViewport(control, control, root, ref viewport);
                return viewport;
            }

            private static void CalculateEffectiveViewport(ServerCompositionVisual target, ServerCompositionVisual control, ServerCompositionVisual root, ref Rect viewport)
            {
                var controlSize = control.Size;
                var controlOffset = control.Offset;

                if (control.Parent is object && control.Parent != root)
                {
                    CalculateEffectiveViewport(target, control.Parent, root, ref viewport);
                }
                else
                {
                    viewport = new Rect(0, 0, controlSize.X, controlSize.Y);
                }

                if (control != target && control.ClipToBounds)
                {
                    var bounds = new Rect(controlOffset.X, controlOffset.Y, controlSize.X, controlSize.Y);
                    viewport = bounds.Intersect(viewport);
                }

                var position = new Vector(controlOffset.X, controlOffset.Y);
                viewport = viewport.Translate(-position);

                if (control != target && control.TransformMatrix != Matrix.Identity)
                {
                    if (control.TransformMatrix.TryInvert(out var invertedTransform))
                        viewport = viewport.TransformToAABB(invertedTransform);
                    else
                        viewport = default;
                }
            }
        }
    }
}