using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    /// <summary>
    /// Mask related utility class. This class provides masking-specific utility functions.
    /// </summary>
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

        /// <summary>
        /// Find a root Canvas.
        /// </summary>
        /// <param name="start">Transform to start the search at going up the hierarchy.</param>
        /// <returns>Finds either the most root canvas, or the first canvas that overrides sorting.</returns>
        public static Transform FindRootSortOverrideCanvas(Transform start)
        {
            var canvasList = ListPool<Canvas>.Get();
            start.GetComponentsInParent(false, canvasList);
            Canvas canvas = null;

            for (int i = 0; i < canvasList.Count; ++i)
            {
                canvas = canvasList[i];

                // We found the canvas we want to use break
                if (canvas.overrideSorting)
                    break;
            }
            ListPool<Canvas>.Release(canvasList);

            return canvas != null ? canvas.transform : null;
        }

        /// <summary>
        /// Find the stencil depth for a given element.
        /// </summary>
        /// <param name="transform">The starting transform to search.</param>
        /// <param name="stopAfter">Where the search of parents should stop</param>
        /// <returns>What the proper stencil buffer index should be.</returns>
        public static int GetStencilDepth(Transform transform, Transform stopAfter)
        {
            var depth = 0;
            if (transform == stopAfter)
                return depth;

            var t = transform.parent;
            var components = ListPool<Mask>.Get();
            while (t != null)
            {
                t.GetComponents<Mask>(components);
                for (var i = 0; i < components.Count; ++i)
                {
                    if (components[i] != null && components[i].MaskEnabled() && components[i].graphic.IsActive())
                    {
                        ++depth;
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
        //       看 clippable 与 RectMask2D 层级之间是否夹有使用独立 SortOrder 的 Canvas。
        //       若是，则说明不存在生效的 RectMask2D。返回null（依次打断内层循环、外层循环）。
        //    若存在生效的 RectMask2D 则返回。
        public static RectMask2D GetRectMaskForClippable(IClippable clippable)
        {
            List<RectMask2D> rectMaskComponents = ListPool<RectMask2D>.Get();
            List<Canvas> canvasComponents = ListPool<Canvas>.Get();
            RectMask2D componentToReturn = null;
            
            clippable.gameObject.GetComponentsInParent(false, rectMaskComponents);  // 取 clippable 所有的父 RectMask2D 组件（不包含未激活的）。

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
                    if (!componentToReturn.isActiveAndEnabled)      //若 RectMask2D 组件的物体未激活或组件未启用，则跳过。可细微优化，取组件时已经是未激活的了。
                    {
                        componentToReturn = null;
                        continue;
                    }
                    
                    clippable.gameObject.GetComponentsInParent(false, canvasComponents);    // 取 clippable 所有的父 Canvas 组件。
                    for (int i = canvasComponents.Count - 1; i >= 0; i--)        //遍历 Canvas
                    {
                        // 该 RectMask2D 不与 该 Canvas 同物体，也不是该 Canvas 的子孙物体 且 该 Canvas 使用独立的 SortOrder。
                        // 即：若“使用独立 SortOrder”的 Canvas，出现在 RectMask2D 和 clippable 物体的中间。 则此时应使 RectMask2D 不对 clippable 生效。
                        // 如，以下层级关系中, 由于Canvas2的存在，RectMask2D 将 不对 Image 生效。
                        //---------------------------------------------------------
                        // --Canvas1
                        // ----RectMask2D
                        // ------Canvas2（使用独立的SortOrder）
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

        /// <summary>
        /// Search for all RectMask2D that apply to the given RectMask2D (includes self).
        /// </summary>
        /// <param name="clipper">Starting clipping object.</param>
        /// <param name="masks">The list of Rect masks</param>
        public static void GetRectMasksForClip(RectMask2D clipper, List<RectMask2D> masks)
        {
            masks.Clear();

            List<Canvas> canvasComponents = ListPool<Canvas>.Get();
            List<RectMask2D> rectMaskComponents = ListPool<RectMask2D>.Get();
            clipper.transform.GetComponentsInParent(false, rectMaskComponents);

            if (rectMaskComponents.Count > 0)
            {
                clipper.transform.GetComponentsInParent(false, canvasComponents);
                for (int i = rectMaskComponents.Count - 1; i >= 0; i--)
                {
                    if (!rectMaskComponents[i].IsActive())
                        continue;
                    bool shouldAdd = true;
                    for (int j = canvasComponents.Count - 1; j >= 0; j--)
                    {
                        if (!IsDescendantOrSelf(canvasComponents[j].transform, rectMaskComponents[i].transform) && canvasComponents[j].overrideSorting)
                        {
                            shouldAdd = false;
                            break;
                        }
                    }
                    if (shouldAdd)
                        masks.Add(rectMaskComponents[i]);
                }
            }

            ListPool<RectMask2D>.Release(rectMaskComponents);
            ListPool<Canvas>.Release(canvasComponents);
        }
    }
}
