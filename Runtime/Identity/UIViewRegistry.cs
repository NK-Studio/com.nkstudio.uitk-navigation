using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;

namespace NKStudio.UITKNavigation.Identity
{
    /// <summary>
    /// Provides UI View Registry functionality.
    /// </summary>
    public static partial class UIViewRegistry
    {
        /// <summary>
        /// Gets the views.
        /// </summary>
        [AutoStaticsCleanup]
        private static Dictionary<UIKey, List<IUIVisibleView>> _views;

        /// <summary>
        /// Gets the buffer pool.
        /// </summary>
        [AutoStaticsCleanup]
        private static Stack<List<IUIVisibleView>> _bufferPool;

        private static readonly IUIVisibleView[] EmptyViews = Array.Empty<IUIVisibleView>();

        private static Dictionary<UIKey, List<IUIVisibleView>> Views =>
            _views ??= new Dictionary<UIKey, List<IUIVisibleView>>();

        private static Stack<List<IUIVisibleView>> BufferPool =>
            _bufferPool ??= new Stack<List<IUIVisibleView>>();

        /// <summary>
        /// Gets the count.
        /// </summary>
        public static int Count => Views.Count;

        /// <summary>
        /// Occurs after a view is registered under an identifier.
        /// </summary>
        public static event Action<UIKey, IUIVisibleView> Registered;
        /// <summary>
        /// Occurs after a view is removed from an identifier.
        /// </summary>
        public static event Action<UIKey, IUIVisibleView> Unregistered;

        /// <summary>
        /// Registers member.
        /// </summary>
        public static void Register(UIKey id, IUIVisibleView view)
        {
            if (!id.IsValid || view == null)
                return;

            if (!Views.TryGetValue(id, out List<IUIVisibleView> list))
            {
                list = new List<IUIVisibleView>(1);
                Views[id] = list;
            }

            if (IndexOf(list, view) >= 0)
                return;

            list.Add(view);
            Registered?.Invoke(id, view);
        }

        /// <summary>
        /// Removes a view from the specified identifier.
        /// </summary>
        /// <param name="id">The identifier under which the view was registered.</param>
        /// <param name="view">The view to remove.</param>
        /// <returns><see langword="true"/> when a matching registration was removed.</returns>
        public static bool Unregister(UIKey id, IUIVisibleView view)
        {
            if (!id.IsValid || view == null || !Views.TryGetValue(id, out List<IUIVisibleView> list))
                return false;

            int index = IndexOf(list, view);
            if (index < 0)
                return false;

            list.RemoveAt(index);
            if (list.Count == 0)
                Views.Remove(id);

            Unregistered?.Invoke(id, view);
            return true;
        }

        /// <summary>
        /// Removes every registered view without changing the view visibility states.
        /// </summary>
        public static void Clear() => Views.Clear();

        /// <summary>
        /// Gets the views.
        /// </summary>
        public static IReadOnlyList<IUIVisibleView> GetViews(UIKey id) =>
            id.IsValid && Views.TryGetValue(id, out List<IUIVisibleView> list)
                ? list
                : EmptyViews;

        /// <summary>
        /// Determines whether registered.
        /// </summary>
        public static bool IsRegistered(UIKey id) => GetViews(id).Count > 0;

        /// <summary>
        /// Shows member.
        /// </summary>
        public static void Show(UIKey id, bool instant = false) => Dispatch(id, true, instant);

        /// <summary>
        /// Hides member.
        /// </summary>
        public static void Hide(UIKey id, bool instant = false) => Dispatch(id, false, instant);

        /// <summary>
        /// Shows all views registered under the supplied identifiers.
        /// </summary>
        /// <param name="ids">The identifiers to show.</param>
        /// <param name="instant">Whether to skip Show transitions.</param>
        public static void ShowAll(IReadOnlyList<UIKey> ids, bool instant = false)
        {
            if (ids == null) return;
            for (int i = 0; i < ids.Count; i++) Show(ids[i], instant);
        }

        /// <summary>
        /// Hides all views registered under the supplied identifiers.
        /// </summary>
        /// <param name="ids">The identifiers to hide.</param>
        /// <param name="instant">Whether to skip Hide transitions.</param>
        public static void HideAll(IReadOnlyList<UIKey> ids, bool instant = false)
        {
            if (ids == null) return;
            for (int i = 0; i < ids.Count; i++) Hide(ids[i], instant);
        }

        /// <summary>
        /// Performs the resync to operation.
        /// </summary>
        public static void ResyncTo(IReadOnlyList<UIKey> visibleIds)
        {
            List<IUIVisibleView> snapshot = RentBuffer();
            List<UIKey> keys = new List<UIKey>(Views.Keys);

            for (int k = 0; k < keys.Count; k++)
            {
                UIKey key = keys[k];
                bool visible = Contains(visibleIds, key);

                snapshot.Clear();
                if (Views.TryGetValue(key, out List<IUIVisibleView> list))
                    snapshot.AddRange(list);

                for (int i = 0; i < snapshot.Count; i++)
                {
                    if (visible) snapshot[i].InstantShow();
                    else snapshot[i].InstantHide();
                }
            }

            ReturnBuffer(snapshot);
        }

        private static void Dispatch(UIKey id, bool visible, bool instant)
        {
            if (!id.IsValid || !Views.TryGetValue(id, out List<IUIVisibleView> list) || list.Count == 0)
                return;

            List<IUIVisibleView> snapshot = RentBuffer();
            snapshot.AddRange(list);

            for (int i = 0; i < snapshot.Count; i++)
            {
                IUIVisibleView view = snapshot[i];
                if (visible)
                {
                    if (instant) view.InstantShow();
                    else view.Show();
                }
                else
                {
                    if (instant) view.InstantHide();
                    else view.Hide();
                }
            }

            ReturnBuffer(snapshot);
        }

        private static List<IUIVisibleView> RentBuffer()
        {
            if (BufferPool.Count == 0)
                return new List<IUIVisibleView>(4);

            List<IUIVisibleView> buffer = BufferPool.Pop();
            buffer.Clear();
            return buffer;
        }

        private static void ReturnBuffer(List<IUIVisibleView> buffer)
        {
            buffer.Clear();
            BufferPool.Push(buffer);
        }

        private static int IndexOf(List<IUIVisibleView> list, IUIVisibleView view)
        {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], view))
                    return i;

            return -1;
        }

        private static bool Contains(IReadOnlyList<UIKey> ids, UIKey id)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] == id) return true;

            return false;
        }
    }
}
