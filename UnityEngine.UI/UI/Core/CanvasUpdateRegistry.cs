using System;
using System.Collections.Generic;
using UnityEngine.UI.Collections;

namespace UnityEngine.UI
{
    // Values of 'update' called on a Canvas update.
    // 在Canvas更新时调用的“更新”的值。
    // Canvas 更新的 6个阶段。
    public enum CanvasUpdate
    {
        Prelayout = 0,      //Called before layout. Layout前。
        Layout = 1,         //Called for layout. Layout时。
        PostLayout = 2,     // Called after layout. Layout后。
        PreRender = 3,      //Called before rendering. Rendering前。
        LatePreRender = 4,  //Called late, before render. Rendering后。
        MaxUpdateValue = 5  // Max enum value. Always last. 最大值，总在最后。
    }

    // This is an element that can live on a Canvas.
    // 这是一个可以存在于画布上的元素（实现这个接口）。
    public interface ICanvasElement
    {
        // Rebuild the element for the given stage.
        // 对给定 CanvasUpdate stage 重建元素。
        // 参数"executing"：给定的 CanvasUpdate stage。
        void Rebuild(CanvasUpdate executing);

        // Get the transform associated with the ICanvasElement.
        // 实现了 ICanvasElement 接口的元素的 Transform 组件。
        Transform transform { get; }

        // Callback sent when this ICanvasElement has completed layout.
        // 当 ICanvasElement 完成 Layout 时触发回调。
        void LayoutComplete();

        // Callback sent when this ICanvasElement has completed Graphic rebuild.
        // 当 ICanvasElement 完成图形重建时触发回调。
        void GraphicUpdateComplete();

        // Used if the native representation has been destroyed.
        // Return true if the element is considered destroyed.
        // 如果该元素被认为已销毁，则返回true。
        bool IsDestroyed();
    }

    // A place where CanvasElements can register themselves for rebuilding.
    // 画布元素可以注册自己，进行重建的地方。
    public class CanvasUpdateRegistry
    {
        private static CanvasUpdateRegistry s_Instance;

        private bool m_PerformingLayoutUpdate;
        private bool m_PerformingGraphicUpdate;

        private readonly IndexedSet<ICanvasElement> m_LayoutRebuildQueue = new IndexedSet<ICanvasElement>();
        private readonly IndexedSet<ICanvasElement> m_GraphicRebuildQueue = new IndexedSet<ICanvasElement>();

        protected CanvasUpdateRegistry()
        {
            Canvas.willRenderCanvases += PerformUpdate;
        }

        /// <summary>
        /// Get the singleton registry instance.
        /// </summary>
        public static CanvasUpdateRegistry instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = new CanvasUpdateRegistry();
                return s_Instance;
            }
        }

        private bool ObjectValidForUpdate(ICanvasElement element)
        {
            var valid = element != null;

            var isUnityObject = element is Object;
            if (isUnityObject)
                valid = (element as Object) != null; //Here we make use of the overloaded UnityEngine.Object == null, that checks if the native object is alive.

            return valid;
        }

        private void CleanInvalidItems()
        {
            // So MB's override the == operator for null equality, which checks
            // if they are destroyed. This is fine if you are looking at a concrete
            // mb, but in this case we are looking at a list of ICanvasElement
            // this won't forward the == operator to the MB, but just check if the
            // interface is null. IsDestroyed will return if the backend is destroyed.

            for (int i = m_LayoutRebuildQueue.Count - 1; i >= 0; --i)
            {
                var item = m_LayoutRebuildQueue[i];
                if (item == null)
                {
                    m_LayoutRebuildQueue.RemoveAt(i);
                    continue;
                }

                if (item.IsDestroyed())
                {
                    m_LayoutRebuildQueue.RemoveAt(i);
                    item.LayoutComplete();
                }
            }

            for (int i = m_GraphicRebuildQueue.Count - 1; i >= 0; --i)
            {
                var item = m_GraphicRebuildQueue[i];
                if (item == null)
                {
                    m_GraphicRebuildQueue.RemoveAt(i);
                    continue;
                }

                if (item.IsDestroyed())
                {
                    m_GraphicRebuildQueue.RemoveAt(i);
                    item.GraphicUpdateComplete();
                }
            }
        }

        private static readonly Comparison<ICanvasElement> s_SortLayoutFunction = SortLayoutList;
        private void PerformUpdate()
        {
            UISystemProfilerApi.BeginSample(UISystemProfilerApi.SampleType.Layout);
            CleanInvalidItems();

            m_PerformingLayoutUpdate = true;

            m_LayoutRebuildQueue.Sort(s_SortLayoutFunction);
            for (int i = 0; i <= (int)CanvasUpdate.PostLayout; i++)
            {
                for (int j = 0; j < m_LayoutRebuildQueue.Count; j++)
                {
                    var rebuild = instance.m_LayoutRebuildQueue[j];
                    try
                    {
                        if (ObjectValidForUpdate(rebuild))
                            rebuild.Rebuild((CanvasUpdate)i);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, rebuild.transform);
                    }
                }
            }

            for (int i = 0; i < m_LayoutRebuildQueue.Count; ++i)
                m_LayoutRebuildQueue[i].LayoutComplete();

            instance.m_LayoutRebuildQueue.Clear();
            m_PerformingLayoutUpdate = false;

            // now layout is complete do culling...
            ClipperRegistry.instance.Cull();

            m_PerformingGraphicUpdate = true;
            for (var i = (int)CanvasUpdate.PreRender; i < (int)CanvasUpdate.MaxUpdateValue; i++)
            {
                for (var k = 0; k < instance.m_GraphicRebuildQueue.Count; k++)
                {
                    try
                    {
                        var element = instance.m_GraphicRebuildQueue[k];
                        if (ObjectValidForUpdate(element))
                            element.Rebuild((CanvasUpdate)i);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, instance.m_GraphicRebuildQueue[k].transform);
                    }
                }
            }

            for (int i = 0; i < m_GraphicRebuildQueue.Count; ++i)
                m_GraphicRebuildQueue[i].GraphicUpdateComplete();

            instance.m_GraphicRebuildQueue.Clear();
            m_PerformingGraphicUpdate = false;
            UISystemProfilerApi.EndSample(UISystemProfilerApi.SampleType.Layout);
        }

        private static int ParentCount(Transform child)
        {
            if (child == null)
                return 0;

            var parent = child.parent;
            int count = 0;
            while (parent != null)
            {
                count++;
                parent = parent.parent;
            }
            return count;
        }

        private static int SortLayoutList(ICanvasElement x, ICanvasElement y)
        {
            Transform t1 = x.transform;
            Transform t2 = y.transform;

            return ParentCount(t1) - ParentCount(t2);
        }

        /// <summary>
        /// Try and add the given element to the layout rebuild list.
        /// Will not return if successfully added.
        /// </summary>
        /// <param name="element">The element that is needing rebuilt.</param>
        public static void RegisterCanvasElementForLayoutRebuild(ICanvasElement element)
        {
            instance.InternalRegisterCanvasElementForLayoutRebuild(element);
        }

        /// <summary>
        /// Try and add the given element to the layout rebuild list.
        /// </summary>
        /// <param name="element">The element that is needing rebuilt.</param>
        /// <returns>
        /// True if the element was successfully added to the rebuilt list.
        /// False if either already inside a Graphic Update loop OR has already been added to the list.
        /// </returns>
        public static bool TryRegisterCanvasElementForLayoutRebuild(ICanvasElement element)
        {
            return instance.InternalRegisterCanvasElementForLayoutRebuild(element);
        }

        private bool InternalRegisterCanvasElementForLayoutRebuild(ICanvasElement element)
        {
            if (m_LayoutRebuildQueue.Contains(element))
                return false;

            /* TODO: this likely should be here but causes the error to show just resizing the game view (case 739376)
            if (m_PerformingLayoutUpdate)
            {
                Debug.LogError(string.Format("Trying to add {0} for layout rebuild while we are already inside a layout rebuild loop. This is not supported.", element));
                return false;
            }*/

            return m_LayoutRebuildQueue.AddUnique(element);
        }

        /// <summary>
        /// Try and add the given element to the rebuild list.
        /// Will not return if successfully added.
        /// </summary>
        /// <param name="element">The element that is needing rebuilt.</param>
        public static void RegisterCanvasElementForGraphicRebuild(ICanvasElement element)
        {
            instance.InternalRegisterCanvasElementForGraphicRebuild(element);
        }

        /// <summary>
        /// Try and add the given element to the rebuild list.
        /// </summary>
        /// <param name="element">The element that is needing rebuilt.</param>
        /// <returns>
        /// True if the element was successfully added to the rebuilt list.
        /// False if either already inside a Graphic Update loop OR has already been added to the list.
        /// </returns>
        public static bool TryRegisterCanvasElementForGraphicRebuild(ICanvasElement element)
        {
            return instance.InternalRegisterCanvasElementForGraphicRebuild(element);
        }

        private bool InternalRegisterCanvasElementForGraphicRebuild(ICanvasElement element)
        {
            if (m_PerformingGraphicUpdate)
            {
                Debug.LogError(string.Format("Trying to add {0} for graphic rebuild while we are already inside a graphic rebuild loop. This is not supported.", element));
                return false;
            }

            return m_GraphicRebuildQueue.AddUnique(element);
        }

        /// <summary>
        /// Remove the given element from both the graphic and the layout rebuild lists.
        /// </summary>
        /// <param name="element"></param>
        public static void UnRegisterCanvasElementForRebuild(ICanvasElement element)
        {
            instance.InternalUnRegisterCanvasElementForLayoutRebuild(element);
            instance.InternalUnRegisterCanvasElementForGraphicRebuild(element);
        }

        private void InternalUnRegisterCanvasElementForLayoutRebuild(ICanvasElement element)
        {
            if (m_PerformingLayoutUpdate)
            {
                Debug.LogError(string.Format("Trying to remove {0} from rebuild list while we are already inside a rebuild loop. This is not supported.", element));
                return;
            }

            element.LayoutComplete();
            instance.m_LayoutRebuildQueue.Remove(element);
        }

        private void InternalUnRegisterCanvasElementForGraphicRebuild(ICanvasElement element)
        {
            if (m_PerformingGraphicUpdate)
            {
                Debug.LogError(string.Format("Trying to remove {0} from rebuild list while we are already inside a rebuild loop. This is not supported.", element));
                return;
            }
            element.GraphicUpdateComplete();
            instance.m_GraphicRebuildQueue.Remove(element);
        }

        /// <summary>
        /// Are graphics layouts currently being calculated..
        /// </summary>
        /// <returns>True if the rebuild loop is CanvasUpdate.Prelayout, CanvasUpdate.Layout or CanvasUpdate.Postlayout</returns>
        public static bool IsRebuildingLayout()
        {
            return instance.m_PerformingLayoutUpdate;
        }

        /// <summary>
        /// Are graphics currently being rebuild.
        /// </summary>
        /// <returns>True if the rebuild loop is CanvasUpdate.PreRender or CanvasUpdate.Render</returns>
        public static bool IsRebuildingGraphics()
        {
            return instance.m_PerformingGraphicUpdate;
        }
    }
}
