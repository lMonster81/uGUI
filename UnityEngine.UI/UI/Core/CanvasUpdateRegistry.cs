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
        // 对指定 CanvasUpdate stage 重建元素。
        // 参数"executing"：指定的 CanvasUpdate stage。
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
        
        // 注册事件 Canvas.willRenderCanvases，用来触发更新。（这是更新/重建的源动力）
        // Canvas.willRenderCanvases：https://docs.unity3d.com/cn/2020.1/ScriptReference/Canvas-willRenderCanvases.html
        // 在即将开始 Canvas 渲染前调用的事件。这让您能够延时处理 /更新基于画布的元素，直到即将开始渲染它们时。  
        protected CanvasUpdateRegistry()
        {
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

        // 判断对象对于更新是否有效。
        // 有效指：非null、是UnityObject、存活。（实际肯定是非null，因为已经在CleanInvalidItems排除了）
        private bool ObjectValidForUpdate(ICanvasElement element)
        {
            var valid = element != null;

            var isUnityObject = element is Object;
            if (isUnityObject)
                valid = (element as Object) != null; //Here we make use of the overloaded UnityEngine.Object == null, that checks if the native object is alive.

            return valid;
        }

        // 清理无效项 无效指： null 对象 或 已销毁的对象。
        // 1、将无效项从 LayoutRebuild 队列中移除。若对象已被销毁，则直接触发 LayoutComplete 事件。
        // 2、将无效项从 GraphicRebuild 队列中移除。若对象已被销毁，则直接触发 GraphicUpdateComplete 事件。
        private void CleanInvalidItems()
        {
            // So MB's override the == operator for null equality, which checks
            // if they are destroyed. This is fine if you are looking at a concrete
            // mb, but in this case we are looking at a list of ICanvasElement
            // this won't forward the == operator to the MB, but just check if the
            // interface is null. IsDestroyed will return if the backend is destroyed.

            // MonoBehaviour 重写了 == 操作符。可以用 “== null” 来判断物体否被销毁。
            // 因此，如果我们正在查看一个具体的 MonoBehaviour，用“== null”来判断物体否被销毁是没问题的。
            // 但在本例中，我们正在查看一个 ICanvasElement 列表。这不会将 == 操作符转发到 MonoBehaviour，而只是检查该接口类型的对象是否为null。
            // 所以，要调用 IsDestroyed() 方法来确定实现了接口 ICanvasElement 的物体是否被销毁。
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

        // 执行更新！
        // 1、清理无效项。
        // 2、执行 LayoutUpdate。 注意两层for循环的顺序，每个阶段所有物体执行完Rebuild()，才进入下一个更新阶段。
        // 3、执行 Culling。
        // 4、执行 GraphicUpdate。注意两层for循环的顺序，每个阶段所有物体执行完Rebuild()，才进入下一个更新阶段。
        private void PerformUpdate()
        {
            UISystemProfilerApi.BeginSample(UISystemProfilerApi.SampleType.Layout);     //性能采样。 疑问??? 为什么只有 SampleType.Layout，没有 SampleType.Render？
            CleanInvalidItems();                //清理无效项

            m_PerformingLayoutUpdate = true;    //标记为正在进行 LayoutUpdate（开始）。

            m_LayoutRebuildQueue.Sort(s_SortLayoutFunction);            //按父子嵌套层数降序排序。
            for (int i = 0; i <= (int)CanvasUpdate.PostLayout; i++)     //依次执行 Prelayout、Layout、PostLayout。
            {
                for (int j = 0; j < m_LayoutRebuildQueue.Count; j++)    //排序后的队列元素依次执行。
                {
                    var rebuild = instance.m_LayoutRebuildQueue[j];
                    try
                    {
                        if (ObjectValidForUpdate(rebuild))              //检查对象更新有效性。
                            rebuild.Rebuild((CanvasUpdate)i);           //执行元素的 Rebuild()。
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, rebuild.transform);
                    }
                }
            }

            for (int i = 0; i < m_LayoutRebuildQueue.Count; ++i)
                m_LayoutRebuildQueue[i].LayoutComplete();               //触发 LayoutComplete 事件。

            instance.m_LayoutRebuildQueue.Clear();  //清空 LayoutRebuild 队列。
            m_PerformingLayoutUpdate = false;       //标记为不在进行 LayoutUpdate（结束）。

            // now layout is complete do culling...
            // 现在，layout结束了，执行剔除逻辑。
            ClipperRegistry.instance.Cull();

            m_PerformingGraphicUpdate = true;   //标记为正在进行 GraphicUpdate（开始）。
            for (var i = (int)CanvasUpdate.PreRender; i < (int)CanvasUpdate.MaxUpdateValue; i++)    //依次执行 PreRender、LatePreRender。
            {
                for (var k = 0; k < instance.m_GraphicRebuildQueue.Count; k++)  //队列元素依次执行。
                {
                    try
                    {
                        var element = instance.m_GraphicRebuildQueue[k];
                        if (ObjectValidForUpdate(element))                      //检查对象更新有效性。                        
                            element.Rebuild((CanvasUpdate)i);                   //执行元素的 Rebuild()。
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, instance.m_GraphicRebuildQueue[k].transform);
                    }
                }
            }

            for (int i = 0; i < m_GraphicRebuildQueue.Count; ++i)
                m_GraphicRebuildQueue[i].GraphicUpdateComplete();               //触发 GraphicUpdateComplete 事件。

            instance.m_GraphicRebuildQueue.Clear();     //清空 GraphicRebuildQueue 队列。
            m_PerformingGraphicUpdate = false;          //标记为不在进行 GraphicUpdate（结束）。
            UISystemProfilerApi.EndSample(UISystemProfilerApi.SampleType.Layout);
        }

        //取父物体数量（父子嵌套层数）
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

        // 对 LayoutList 排序的比较方法。
        // 按父物体数量（父子嵌套层数）降序。
        private static int SortLayoutList(ICanvasElement x, ICanvasElement y)
        {
            Transform t1 = x.transform;
            Transform t2 = y.transform;

            return ParentCount(t1) - ParentCount(t2);
        }

        // Try and add the given element to the layout rebuild list.
        // Will not return if successfully added. 
        // 尝试将指定元素添加到 LayoutRebuild 队列中。
        // 添加成功无返回值。
        // 参数 "element"：The element that is needing rebuilt. 需要重建的元素。
        public static void RegisterCanvasElementForLayoutRebuild(ICanvasElement element)
        {
            instance.InternalRegisterCanvasElementForLayoutRebuild(element);
        }

        // Try and add the given element to the layout rebuild list.
        //"element"：The element that is needing rebuilt. 需要重建的元素。
        // True if the element was successfully added to the rebuilt list.
        // False if either already inside a Graphic Update loop OR has already been added to the list.
        // 尝试将指定元素添加到 LayoutRebuild 队列中。
        // 若添加成功返回true，若正在执行更新或已存在于 LayoutRebuild 队列中则返回false。
        public static bool TryRegisterCanvasElementForLayoutRebuild(ICanvasElement element)
        {
            return instance.InternalRegisterCanvasElementForLayoutRebuild(element);
        }

        // 内部私有，添加 LayoutRebuild 注册
        // 1、检查确保不在 LayoutRebuild 队列中。 //注意：这里未使用 m_PerformingLayoutUpdate 判断。因为有个待处理 bug。
        // 2、加入 LayoutRebuild 队列。
        private bool InternalRegisterCanvasElementForLayoutRebuild(ICanvasElement element)
        {
            if (m_LayoutRebuildQueue.Contains(element))
                return false;

            // 待处理 bug：在调整游戏视图大小时会导致显示错误。
            /* TODO: this likely should be here but causes the error to show just resizing the game view (case 739376)
            if (m_PerformingLayoutUpdate)
            {
                Debug.LogError(string.Format("Trying to add {0} for layout rebuild while we are already inside a layout rebuild loop. This is not supported.", element));
                return false;
            }*/

            return m_LayoutRebuildQueue.AddUnique(element);
        }


        // Try and add the given element to the rebuild list.
        // Will not return if successfully added. 
        // 尝试将指定元素添加到 GraphicRebuild 队列中。
        // 添加成功无返回值。
        // 参数 "element"：The element that is needing rebuilt. 需要重建的元素。
        public static void RegisterCanvasElementForGraphicRebuild(ICanvasElement element)
        {
            instance.InternalRegisterCanvasElementForGraphicRebuild(element);
        }

        // Try and add the given element to the rebuild list.
        //"element"：The element that is needing rebuilt. 需要重建的元素。
        // True if the element was successfully added to the rebuilt list.
        // False if either already inside a Graphic Update loop OR has already been added to the list.
        // 尝试将指定元素添加到 GraphicRebuild 队列中。
        // 若添加成功返回true，若正在执行更新或已存在于 GraphicRebuild 队列中则返回false。
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

        // Remove the given element from both the graphic and the layout rebuild lists.
        // 将指定元素从 LayoutRebuild 队列 和 GraphicRebuild 队列中移除。
        // 参数 "element"：需要移除的元素。
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
