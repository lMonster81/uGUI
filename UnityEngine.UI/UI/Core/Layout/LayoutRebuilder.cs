using System.Collections.Generic;
using UnityEngine.Events;

namespace UnityEngine.UI
{
    // Wrapper class for managing layout rebuilding of CanvasElement.
    // 用于管理对 CanvasElement 进行布局重建的包装类。
    public class LayoutRebuilder : ICanvasElement
    {
        private RectTransform m_ToRebuild;  //要重建的 RectTransform 元素

        // There are a few of reasons we need to cache the Hash from the transform:
        //  - This is a ValueType (struct) and .Net calculates Hash from the Value Type fields.
        //  - The key of a Dictionary should have a constant Hash value.
        //  - It's possible for the Transform to get nulled from the Native side.
        // We use this struct with the IndexedSet container, which uses a dictionary as part of it's implementation
        // So this struct gets used as a key to a dictionary, so we need to guarantee a constant Hash value.
        // --------------------------------------------------
        // 我们需要缓存 transform 的哈希值，有几个原因：
        //  - 这是一个值类型（结构体），并且 .Net 从值类型的字段中计算哈希值。
        //  - Dictionary 的 key 需要是一个常量哈希值。
        //  - 从原生代码中，取到的 Transform 可能是一个 null 值。
        // 我们将此结构与 IndexedSet 容器一起使用，IndexedSet 内部使用字典作为其实现的一部分。
        private int m_CachedHashFromTransform;

        static ObjectPool<LayoutRebuilder> s_Rebuilders = new ObjectPool<LayoutRebuilder>(null, x => x.Clear());    //LayoutRebuilder 对象池

        //与一个要 Rebuild 的 RectTransform 关联起来。
        private void Initialize(RectTransform controller)
        {
            m_ToRebuild = controller;
            m_CachedHashFromTransform = controller.GetHashCode();   //缓存 RectTransform 的 hashcode 用来做判等
        }

        private void Clear()
        {
            m_ToRebuild = null;
            m_CachedHashFromTransform = 0;
        }  

        static LayoutRebuilder()
        {
            // Event that is invoked for RectTransforms that need to have their driven properties reapplied.
            // 为 RectTransform 调用的需要重新应用其驱动属性的事件。 //疑问？？？ 具体怎么触发？
            RectTransform.reapplyDrivenProperties += ReapplyDrivenProperties;   
        }

        static void ReapplyDrivenProperties(RectTransform driven)
        {
            MarkLayoutForRebuild(driven);
        }

        public Transform transform { get { return m_ToRebuild; }}

        // Has the native representation of this LayoutRebuilder been destroyed?
        // 这个 LayoutRebuilder 关联的 RectTransform 是否已被销毁?  
        public bool IsDestroyed()
        {
            return m_ToRebuild == null;
        }

        //从列表中去除未启用/未激活的 Behaviour
        static void StripDisabledBehavioursFromList(List<Component> components)
        {
            components.RemoveAll(e => e is Behaviour && !((Behaviour)e).isActiveAndEnabled);
        }

        // Forces an immediate rebuild of the layout element and child layout elements affected by the calculations.
        // Normal use of the layout system should not use this method. Instead MarkLayoutForRebuild should be used instead, which triggers a delayed layout rebuild during the next layout pass. The delayed rebuild automatically handles objects in the entire layout hierarchy in the correct order, and prevents multiple recalculations for the same layout elements.
        // However, for special layout calculation needs, ::ref::ForceRebuildLayoutImmediate can be used to get the layout of a sub-tree resolved immediately. This can even be done from inside layout calculation methods such as ILayoutController.SetLayoutHorizontal orILayoutController.SetLayoutVertical. Usage should be restricted to cases where multiple layout passes are unavaoidable despite the extra cost in performance.

        // 强制立即重建受计算影响的布局元素和子布局元素。
        // 正常的布局系统不应该使用这个方法，而是应该使用 MarkLayoutForRebuild 替代。
        // 它会在下一次布局传递期间触发一个延迟的布局重建。
        // 延迟重建会自动以正确的顺序处理整个布局层次结构中的对象，并防止对相同布局元素进行多次重新计算。  
        // 然而，对于特殊的布局计算需求，可以使用 ForceRebuildLayoutImmediate 方法立即解析子树的布局。
        // 这甚至可以通过内部布局计算方法来实现，比如 ILayoutController.SetLayoutHorizontal 或 ILayoutController.SetLayoutVertical。
        // 使用应该局限于“不可避免地，要使用多个布局通道”的情况下，在性能上有额外的成本。
        // 参数"layoutRoot"：The layout element to perform the layout rebuild on. //要在其上执行布局重建的布局元素  
        public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot)
        {
            var rebuilder = s_Rebuilders.Get(); //从池中获取一个LayoutRebuilder
            rebuilder.Initialize(layoutRoot); //初始化（与RectTransform关联）
            rebuilder.Rebuild(CanvasUpdate.Layout); //立即执行重建
            s_Rebuilders.Release(rebuilder); //将LayoutRebuilder放回池中
        }
        
        public void Rebuild(CanvasUpdate executing)
        {
            switch (executing)
            {
                case CanvasUpdate.Layout:
                    // It's unfortunate that we'll perform the same GetComponents querys for the tree 2 times,
                    // but each tree have to be fully iterated before going to the next action,
                    // so reusing the results would entail storing results in a Dictionary or similar,
                    // which is probably a bigger overhead than performing GetComponents multiple times.
                    // 不幸的是, 我们在查询节点树时要执行2次相同的 GetComponents 。（水平方向一次，竖直方向一次）
                    // 因为在进行下一个操作之前，必须对每个树进行完整的迭代。
                    // 所以，想要重用，就需要将结果存储在字典或类似的对象中。
                    // 但这相比执行2次 GetComponents，可能是一个更大的开销，所以不那么做。
                    //--------------------------------------------------------------------------
                    //执行 ILayoutElement 水平方向计算。 //对所有子元素及自身的 minWidth、preferredWidth、flexibleWidth 赋值/提供值。
                    PerformLayoutCalculation(m_ToRebuild, e => (e as ILayoutElement).CalculateLayoutInputHorizontal());
                    //执行 ILayoutController 水平方向排布。 //利用布局组件的属性及子元素的 minWidth、preferredWidth、flexibleWidth 进行水平排布。
                    PerformLayoutControl(m_ToRebuild, e => (e as ILayoutController).SetLayoutHorizontal());

                    //执行 ILayoutElement 竖直方向计算。 //对所有子元素及自身的 minHeight、preferredHeight、flexibleHeight 赋值/提供值。
                    PerformLayoutCalculation(m_ToRebuild, e => (e as ILayoutElement).CalculateLayoutInputVertical());
                    //执行 ILayoutController 竖直方向排布。 //利用布局组件的属性及子元素的 minHeight、preferredHeight、flexibleHeight 进行水平排布。
                    PerformLayoutControl(m_ToRebuild, e => (e as ILayoutController).SetLayoutVertical());
                    break;
            }
        }

        //执行布局控制操作（即：对子元素进行排布）
        private void PerformLayoutControl(RectTransform rect, UnityAction<Component> action)
        {
            if (rect == null)
                return;

            var components = ListPool<Component>.Get();
            rect.GetComponents(typeof(ILayoutController), components);  //取 RectTransform 上的所有实现了 ILayoutController 接口的组件
            StripDisabledBehavioursFromList(components);    //去除其中未启用/未激活的

            // If there are no controllers on this rect we can skip this entire sub-tree
            // We don't need to consider controllers on children deeper in the sub-tree either,
            // since they will be their own roots.
            // 如果这个 RectTransform 上没有任何布局控制器，我们可以跳过整个子树。
            // 我们也不需要考虑子树深处的子树控制器，因为它们将用自己作为根去处理。
            if (components.Count > 0)
            {
                // Layout control needs to executed top down with parents being done before their children,
                // because the children rely on the sizes of the parents.
                // 布局控制需要自上而下执行，父节点要在子节点之前完成，
                // 因为子依赖于父节点的大小。（父节点给子节点分配空间）
                // ----------------------------------------------------------------
                // First call layout controllers that may change their own RectTransform
                // 首先调用改变自身大小的布局控制器（ILayoutSelfController），可能会改变自己的 RectTransform。
                for (int i = 0; i < components.Count; i++)
                    if (components[i] is ILayoutSelfController)
                        action(components[i]);

                // Then call the remaining, such as layout groups that change their children, taking their own RectTransform size into account.
                // 然后调用其余的，例如 LayoutGroup 一类的组件, 会根据自身大小对其子节点进行布局。
                for (int i = 0; i < components.Count; i++)
                    if (!(components[i] is ILayoutSelfController))
                        action(components[i]);

                //递归处理子节点
                for (int i = 0; i < rect.childCount; i++)
                    PerformLayoutControl(rect.GetChild(i) as RectTransform, action);
            }

            ListPool<Component>.Release(components);
        }

        //执行布局计算（即：计算子元素大小）
        private void PerformLayoutCalculation(RectTransform rect, UnityAction<Component> action)
        {
            if (rect == null)
                return;

            var components = ListPool<Component>.Get();
            rect.GetComponents(typeof(ILayoutElement), components);  //取 RectTransform 上的所有实现了 ILayoutElement 接口的组件
            StripDisabledBehavioursFromList(components); //去除其中未启用/未激活的

            // If there are no controllers on this rect we can skip this entire sub-tree
            // We don't need to consider controllers on children deeper in the sub-tree either,
            // since they will be their own roots.
            // 如果这个 RectTransform 上没有任何布局控制器，我们可以跳过整个子树。
            // 我们也不需要考虑子树深处的子树控制器，因为它们将用自己作为根去处理。
            if (components.Count > 0  || rect.GetComponent(typeof(ILayoutGroup)))
            {
                // Layout calculations needs to executed bottom up with children being done before their parents,
                // because the parent calculated sizes rely on the sizes of the children.
                // 布局计算需要自下而上执行，子节点在父节点之前完成，
                // 因为父节点计算的大小依赖于子节点的大小。
                //----------------------------------------------
                // 首先递归处理子节点
                for (int i = 0; i < rect.childCount; i++)
                    PerformLayoutCalculation(rect.GetChild(i) as RectTransform, action);

                // 调用计算
                for (int i = 0; i < components.Count; i++)
                    action(components[i]);
            }

            ListPool<Component>.Release(components);
        }

        /// <summary>
        /// Mark the given RectTransform as needing it's layout to be recalculated during the next layout pass.
        /// </summary>
        /// <param name="rect">Rect to rebuild.</param>
        public static void MarkLayoutForRebuild(RectTransform rect)
        {
            if (rect == null || rect.gameObject == null)
                return;

            var comps = ListPool<Component>.Get();
            bool validLayoutGroup = true;
            RectTransform layoutRoot = rect;
            var parent = layoutRoot.parent as RectTransform;
            while (validLayoutGroup && !(parent == null || parent.gameObject == null))
            {
                validLayoutGroup = false;
                parent.GetComponents(typeof(ILayoutGroup), comps);

                for (int i = 0; i < comps.Count; ++i)
                {
                    var cur = comps[i];
                    if (cur != null && cur is Behaviour && ((Behaviour)cur).isActiveAndEnabled)
                    {
                        validLayoutGroup = true;
                        layoutRoot = parent;
                        break;
                    }
                }

                parent = parent.parent as RectTransform;
            }

            // We know the layout root is valid if it's not the same as the rect,
            // since we checked that above. But if they're the same we still need to check.
            if (layoutRoot == rect && !ValidController(layoutRoot, comps))
            {
                ListPool<Component>.Release(comps);
                return;
            }

            MarkLayoutRootForRebuild(layoutRoot);
            ListPool<Component>.Release(comps);
        }

        private static bool ValidController(RectTransform layoutRoot, List<Component> comps)
        {
            if (layoutRoot == null || layoutRoot.gameObject == null)
                return false;

            layoutRoot.GetComponents(typeof(ILayoutController), comps);
            for (int i = 0; i < comps.Count; ++i)
            {
                var cur = comps[i];
                if (cur != null && cur is Behaviour && ((Behaviour)cur).isActiveAndEnabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static void MarkLayoutRootForRebuild(RectTransform controller)
        {
            if (controller == null)
                return;

            var rebuilder = s_Rebuilders.Get();
            rebuilder.Initialize(controller);
            if (!CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(rebuilder))
                s_Rebuilders.Release(rebuilder);
        }

        public void LayoutComplete()
        {
            s_Rebuilders.Release(this);  //LayoutRebuilder使用完毕，放回对象池。
        }

        public void GraphicUpdateComplete()
        {}

        // 重写 System.Object 的方法
        public override int GetHashCode()
        {
            return m_CachedHashFromTransform;
        }

        // 重写 System.Object 的方法
        // Does the passed rebuilder point to the same CanvasElement.
        // 用来判断传入的 rebuilder 是否指向同一个 CanvasElement。
        // 参数 "obj"：The other object to compare. //另一个与此比较的对象。
        // 返回值：是否相等。
        public override bool Equals(object obj)
        {
            return obj.GetHashCode() == GetHashCode();
        }

        // 重写 System.Object 的方法
        public override string ToString()
        {
            return "(Layout Rebuilder for) " + m_ToRebuild;
        }
    }
}
