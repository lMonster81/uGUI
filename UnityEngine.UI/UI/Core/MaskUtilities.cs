using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    // Mask related utility class. This class provides masking-specific utility functions.
    // Mask 相关的工具类。该类提供了特定于 Mask 的工具函数。
    // Mask 相关指：Mask 和 RectMask2D。
    public class MaskUtilities
    {
        /// <summary>
        /// Notify all IClippables under the given component that they need to recalculate clipping.
        /// </summary>
        /// <param name="mask">The object thats changed for whose children should be notified.</param>
        public static void Notify2DMaskStateChanged(Component mask)
        {
            var components = ListPool<Component>.Get();
            mask.GetComponentsInChildren(components);
            for (var i = 0; i < components.Count; i++)
            {
                if (components[i] == null || components[i].gameObject == mask.gameObject)
                    continue;

                var toNotify = components[i] as IClippable;
                if (toNotify != null)
                    toNotify.RecalculateClipping();
            }
            ListPool<Component>.Release(components);
        }

        /// <summary>
        /// Notify all IMaskable under the given component that they need to recalculate masking.
        /// </summary>
        /// <param name="mask">The object thats changed for whose children should be notified.</param>
        public static void NotifyStencilStateChanged(Component mask)
        {
            var components = ListPool<Component>.Get();
            mask.GetComponentsInChildren(components);
            for (var i = 0; i < components.Count; i++)
            {
                if (components[i] == null || components[i].gameObject == mask.gameObject)
                    continue;

                var toNotify = components[i] as IMaskable;
                if (toNotify != null)
                    toNotify.RecalculateMasking();
            }
            ListPool<Component>.Release(components);
        }

        // Find a root Canvas.
        // Finds either the most root canvas, or the first canvas that overrides sorting.
        // 查找根 Canvas。
        // 查找最深根的 Canvas，或第一个“使用独立绘制顺序”的 Canvas。
        // 参数"start"：Transform to start the search at going up the hierarchy.  开始在 Hierarchy 上向上搜索的 Transform。
        public static Transform FindRootSortOverrideCanvas(Transform start)
        {
            var canvasList = ListPool<Canvas>.Get();
            start.GetComponentsInParent(false, canvasList); //获取所有父Canvas（结果是从下往上的）
            Canvas canvas = null;

            for (int i = 0; i < canvasList.Count; ++i)
            {
                canvas = canvasList[i];

                // We found the canvas we want to use break
                // 找到“使用独立绘制顺序”的 Canvas，遂 break
                if (canvas.overrideSorting)
                    break;
            }
            ListPool<Canvas>.Release(canvasList);

            //返回目标 Canvas 的 Transform。
            return canvas != null ? canvas.transform : null;
        }

        // Find the stencil depth for a given element.
        // 通过给定的元素计算模板测试深度。 
        // 参数"transform"：The starting transform to search.  开始搜索的 Transform。
        // 参数"stopAfter"：Where the search of parents should stop. 结束搜索的 Transform。
        public static int GetStencilDepth(Transform transform, Transform stopAfter)
        {
            var depth = 0;      //默认0
            if (transform == stopAfter)
                return depth;   //直接结束了，返回0

            //从开始的 Transform 开始，递归向上查找父物体。
            var t = transform.parent;
            var components = ListPool<Mask>.Get();
            while (t != null)
            {
                t.GetComponents<Mask>(components);      //获取t的 Mask 组件
                for (var i = 0; i < components.Count; ++i)
                {
                    // 若Mask存在 且 Mask开启（激活且关联的Graphic存在） 且 关联的Graphic存在激活。
                    // 这里可以看出，深度指：有效的 Mask 嵌套的层数。
                    if (components[i] != null && components[i].MaskEnabled() && components[i].graphic.IsActive())   
                    {
                        ++depth;    //深度+1
                        break;
                    }
                }

                if (t == stopAfter)
                    break;

                t = t.parent;
            }
            ListPool<Mask>.Release(components);
            return depth;
        }

        // Helper function to determine if the child is a descendant of father or is father.
        // Is child equal to father or is a descendant.
        // 辅助方法，判断 B与A相同，或B是A的子孙。
        // 参数"father"：A。
        // 参数"child"：B。
        public static bool IsDescendantOrSelf(Transform father, Transform child)
        {
            if (father == null || child == null)
                return false;

            if (father == child)
                return true;

            while (child.parent != null)
            {
                if (child.parent == father)
                    return true;

                child = child.parent;
            }

            return false;
        }

        // Find the correct RectMask2D for a given IClippable.
        // 为给定的 IClippable 查找其正确的 RectMask2D。
        // 1、取 clippable 所有的父 RectMask2D 组件。
        // 2、若不存在返回 null。
        // 3、开始遍历查找。
        //    若是 clippable 自身上的 RectMask2D，则跳过当前继续查找。
        //    若 RectMask2D 组件的物体未激活，或组件未启用，则跳过当前继续查找。
        //    取 clippable 所有的父 Canvas 组件。进行遍历判断。
        //       看 clippable 与 RectMask2D 层级之间是否夹有“使用独立绘制顺序”的 Canvas。
        //       若是，则说明不存在生效的 RectMask2D。返回null（依次打断内层循环、外层循环）。
        //    若存在生效的 RectMask2D 则返回。
        public static RectMask2D GetRectMaskForClippable(IClippable clippable)
        {
            List<RectMask2D> rectMaskComponents = ListPool<RectMask2D>.Get();
            List<Canvas> canvasComponents = ListPool<Canvas>.Get();
            RectMask2D componentToReturn = null;
            
            clippable.gameObject.GetComponentsInParent(false, rectMaskComponents);  // 取 clippable 所有的父 RectMask2D 组件（不包含未激活的）（包含自身）。

            if (rectMaskComponents.Count > 0)
            {
                for (int rmi = 0; rmi < rectMaskComponents.Count; rmi++)        //遍历 RectMask2D
                {
                    componentToReturn = rectMaskComponents[rmi];
                    if (componentToReturn.gameObject == clippable.gameObject)   //若是 clippable 自身上的 RectMask2D，则跳过。
                    {
                        componentToReturn = null;
                        continue;
                    }
                    if (!componentToReturn.isActiveAndEnabled)      //若 RectMask2D 组件的物体未激活或组件未启用，则跳过。（实际上似乎多余判断，因为取组件时已经不包含未激活的了。
                    {
                        componentToReturn = null;
                        continue;
                    }
                    
                    clippable.gameObject.GetComponentsInParent(false, canvasComponents);    // 取 clippable 所有的父 Canvas 组件（不包含未激活的）（包含自身）。
                    for (int i = canvasComponents.Count - 1; i >= 0; i--)        //遍历 Canvas
                    {
                        // 该 RectMask2D 不与 该 Canvas 同物体，也不是该 Canvas 的子孙物体 且 该 Canvas 使用独立绘制顺序。
                        // 即：若“使用独立绘制顺序”的 Canvas，出现在 RectMask2D 和 clippable 物体的中间。 则此时应使 RectMask2D 不对 clippable 生效。
                        // 如，以下层级关系中, 由于Canvas2的存在，RectMask2D 将 不对 Image 生效。
                        //---------------------------------------------------------
                        // --Canvas1
                        // ----RectMask2D
                        // ------Canvas2（使用独立绘制顺序）
                        // --------Image（clippable）
                        //---------------------------------------------------------
                        if (!IsDescendantOrSelf(canvasComponents[i].transform, componentToReturn.transform) && canvasComponents[i].overrideSorting)
                        {
                            componentToReturn = null;
                            break;
                        }
                    }

                    break;
                }
            }

            ListPool<RectMask2D>.Release(rectMaskComponents);
            ListPool<Canvas>.Release(canvasComponents);

            return componentToReturn;
        }

        // Search for all RectMask2D that apply to the given RectMask2D (includes self).
        // 查找所有适应于给定 RectMask2D 的 RectMask2D。
        // 适应指：与给定 Rectmask2D 共同实际生效的父 RectMask2D。（中间未穿插“使用独立绘制顺序”的 Canvas）
        // 参数"clipper"：Starting clipping object.   开始裁剪的对象。
        // 参数"masks"：The list of Rect masks</param>   RectMask2D 结果列表。
        public static void GetRectMasksForClip(RectMask2D clipper, List<RectMask2D> masks)
        {
            masks.Clear();

            List<Canvas> canvasComponents = ListPool<Canvas>.Get();
            List<RectMask2D> rectMaskComponents = ListPool<RectMask2D>.Get();
            clipper.transform.GetComponentsInParent(false, rectMaskComponents);     // 取 clipper 所有的父 RectMask2D 组件（不包含未激活的）（包含自身）。

            if (rectMaskComponents.Count > 0)
            {
                clipper.transform.GetComponentsInParent(false, canvasComponents);   // 取 clipper 所有的父 Canvas 组件（不包含未激活的）（包含自身）。
                for (int i = rectMaskComponents.Count - 1; i >= 0; i--)     //遍历父 RectMask2D 列表
                {
                    if (!rectMaskComponents[i].IsActive())    //若未激活，则跳过。（实际上似乎多余判断，因为取组件时已经不包含未激活的了。     
                        continue;

                    bool shouldAdd = true;  //是否应该加入结果列表
                    for (int j = canvasComponents.Count - 1; j >= 0; j--)   //遍历父 Canvas  
                    {
                        // 该 RectMask2D 不与 该 Canvas 同物体，也不是该 Canvas 的子孙物体 且 该 Canvas 使用独立绘制顺序。
                        // 即：若“使用独立绘制顺序”的 Canvas，出现在 RectMask2D 和 clippable 物体的中间。 则此时应使 RectMask2D 不对 clippable 生效。
                        // 如，以下层级关系中, 由于Canvas2的存在，RectMask2D 将 不对 Image 生效。
                        //---------------------------------------------------------
                        // --Canvas1
                        // ----RectMask2D
                        // ------Canvas2（使用独立绘制顺序）
                        // --------Image（clippable）
                        //---------------------------------------------------------
                        if (!IsDescendantOrSelf(canvasComponents[j].transform, rectMaskComponents[i].transform) && canvasComponents[j].overrideSorting)
                        {
                            shouldAdd = false;
                            break;
                        }
                    }
                    if (shouldAdd)
                        masks.Add(rectMaskComponents[i]);   //加入返回列表
                }
            }

            ListPool<RectMask2D>.Release(rectMaskComponents);
            ListPool<Canvas>.Release(canvasComponents);
        }
    }
}
