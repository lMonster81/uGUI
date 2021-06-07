using System;
using System.Collections.Generic;
using UnityEngine.UI.Collections;

namespace UnityEngine.UI
{
    // Values of 'update' called on a Canvas update.
    // 在 Canvas 更新时调用的“更新”的值。
    // Canvas 更新的 6个阶段。Layout3 + Render2 + Fninal1
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
    // 这是一个可以存在于画布上的元素
    // 实现这个接口的元素即为“画布元素”。
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
    // 画布元素可以注册自己进行重建的地方。
    public class CanvasUpdateRegistry
    {
        private static CanvasUpdateRegistry s_Instance;     //单例实例

        private bool m_PerformingLayoutUpdate;      //正在执行 LayoutUpdate 的标志
        private bool m_PerformingGraphicUpdate;     //正在执行 GraphicUpdate  的标志

        private readonly IndexedSet<ICanvasElement> m_LayoutRebuildQueue = new IndexedSet<ICanvasElement>();
        private readonly IndexedSet<ICanvasElement> m_GraphicRebuildQueue = new IndexedSet<ICanvasElement>();

        protected CanvasUpdateRegistry()
        {
            // 注册事件 Canvas.willRenderCanvases，用来触发更新。（这是更新/重建的源动力）
            // Canvas.willRenderCanvases：https://docs.unity3d.com/cn/2020.1/ScriptReference/Canvas-willRenderCanvases.html
            //    在即将开始 Canvas 渲染前调用的事件。
            //    这让您能够延时处理 /更新基于画布的元素，直到即将开始渲染它们时。  
            Canvas.willRenderCanvases += PerformUpdate; 
        }

        // Get the singleton registry instance.
        // 获取单例
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

        // 内部私有，添加 LayoutRebuild 注册
        // 1、检查确保不在 LayoutRebuild 队列中。 //注意：这里未使用 m_PerformingLayoutUpdate 判断。因为在调整游戏视图大小时会导致显示错误。
        // 2、加入 LayoutRebuild 队列。
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

       
        // Try and add the given element to the rebuild list. Will not return if successfully added.
        // 
        // 参数 "element"：The element that is needing rebuilt. 需要重建的元素。
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

        // 内部私有，添加 GraphicRebuild 注册
        // 1、检查确保 GraphicUpdate 不是正在进行。
        // 2、加入 GraphicUpdate 队列。
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

        // 内部私有，移除 LayoutRebuild 注册
        // 1、检查确保 LayoutUpdate 不是正在进行。
        // 2、调用元素 LayoutComplete 事件。
        // 3、从 LayoutRebuild 队列中移除。
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

        // 内部私有，移除 GraphicRebuild 注册
        // 1、检查确保 GraphicUpdate 不是正在进行。
        // 2、调用元素 GraphicUpdateComplete 事件。
        // 3、从 GraphicUpdate 队列中移除。
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

        // Are graphics layouts currently being calculated..
        // True if the rebuild loop is CanvasUpdate.Prelayout, CanvasUpdate.Layout or CanvasUpdate.Postlayout
        // 是否 Graphics layouts 正在被计算？
        // 当重建循环的阶段在 CanvasUpdate.Prelayout、CanvasUpdate.Layout 或 CanvasUpdate.Postlayout 时，则返回true。
        public static bool IsRebuildingLayout()
        {
            return instance.m_PerformingLayoutUpdate;
        }

        // Are graphics currently being rebuild.
        // True if the rebuild loop is CanvasUpdate.PreRender or CanvasUpdate.Render
        // 是否 Graphics renders 正在被重建？
        // 当重建循环的阶段在 CanvasUpdate.PreRender 或 CanvasUpdate.Render 时，则返回true。
        public static bool IsRebuildingGraphics()
        {
            return instance.m_PerformingGraphicUpdate;
        }
    }
}
