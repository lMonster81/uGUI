using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

namespace UnityEngine.UI
{
    // Utility functions for querying layout elements for their minimum, preferred, and flexible sizes.
    // All components on the GameObject that implement the ILayoutElement are queried. The one with the highest priority which has a value for this setting is used.
    // If multiple componets have this setting and have the same priority, the maximum value out of those is used.
    // 用于查询布局元素的最小、偏好和灵活大小的工具函数。
    // 取值规则：
    //    RectTransform 所在游戏对象的所有实现了 ILayoutElement 接口的组件都会被查询。
    //    将取其中优先级最高的组件的值。如果多个组件具有此设置并具有相同的优先级，则取其中的最大值。
    public static class LayoutUtility
    {
        // Returns the minimum size of the layout element.
        // 返回布局元素的最小尺寸。(取值规见上)
        public static float GetMinSize(RectTransform rect, int axis)
        {
            if (axis == 0)
                return GetMinWidth(rect);
            return GetMinHeight(rect);
        }

        // Returns the preferred size of the layout element.
        // 返回布局元素的偏好尺寸。(取值规见上)
        public static float GetPreferredSize(RectTransform rect, int axis)
        {
            if (axis == 0)
                return GetPreferredWidth(rect);
            return GetPreferredHeight(rect);
        }

        // Returns the flexible size of the layout element.
        // 返回布局元素的灵活尺寸。(取值规见上)
        public static float GetFlexibleSize(RectTransform rect, int axis)
        {
            if (axis == 0)
                return GetFlexibleWidth(rect);
            return GetFlexibleHeight(rect);
        }

        // Returns the minimum width of the layout element.
        // 返回布局元素的最小宽度。(取值规见上)
        public static float GetMinWidth(RectTransform rect)
        {
            return GetLayoutProperty(rect, e => e.minWidth, 0);
        }

        // Returns the preferred width of the layout element.
        // 返回布局元素的偏好宽度。(取值规见上)
        public static float GetPreferredWidth(RectTransform rect)
        {
            return Mathf.Max(GetLayoutProperty(rect, e => e.minWidth, 0), GetLayoutProperty(rect, e => e.preferredWidth, 0));
        }

        // Returns the flexible width of the layout element.
        // 返回布局元素的灵活宽度。(取值规见上)
        public static float GetFlexibleWidth(RectTransform rect)
        {
            return GetLayoutProperty(rect, e => e.flexibleWidth, 0);
        }

        // Returns the minimum height of the layout element.
        // 返回布局元素的最小高度。(取值规见上)
        public static float GetMinHeight(RectTransform rect)
        {
            return GetLayoutProperty(rect, e => e.minHeight, 0);
        }

        // Returns the preferred height of the layout element.
        // 返回布局元素的偏好高度。(取值规见上)
        public static float GetPreferredHeight(RectTransform rect)
        {
            return Mathf.Max(GetLayoutProperty(rect, e => e.minHeight, 0), GetLayoutProperty(rect, e => e.preferredHeight, 0));
        }

        // Returns the flexible height of the layout element.
        // 返回布局元素的灵活高度。(取值规见上)
        public static float GetFlexibleHeight(RectTransform rect)
        {
            return GetLayoutProperty(rect, e => e.flexibleHeight, 0);
        }

        // Gets a calculated layout property for the layout element with the given RectTransform.
        // 用给定的 RectTransform //为布局元素获取经过计算的布局属性。
        // 参数"rect"：The RectTransform of the layout element to get a property for. //要获取布局属性的布局元素的 RectTransform。
        // 参数"property"：The property to calculate. //要计算的属性。
        // 参数"defaultValue"：The default value to use if no component on the layout element supplies the given property. //如果布局元素上没有组件提供给定的属性，则使用默认值。
        // 返回值：The calculated value of the layout property. //布局属性经过计算的值
        public static float GetLayoutProperty(RectTransform rect, System.Func<ILayoutElement, float> property, float defaultValue)
        {
            ILayoutElement dummy;
            return GetLayoutProperty(rect, property, defaultValue, out dummy);
        }

        // Gets a calculated layout property for the layout element with the given RectTransform.
        // 用给定的 RectTransform 为布局元素获取经过计算的布局属性。
        // 参数"rect"：The RectTransform of the layout element to get a property for. //要获取布局属性的布局元素的 RectTransform。
        // 参数"property"：The property to calculate. //要计算的属性。
        // 参数"defaultValue"：The default value to use if no component on the layout element supplies the given property. //如果布局元素上没有组件提供给定的属性，则使用默认值。
        // 参数"source"：Optional out parameter to get the component that supplied the calculated value. //可选的out参数，用来获取提供计算值的组件。(实际生效的组件)
        // 返回值：The calculated value of the layout property. //布局属性经过计算的值
        public static float GetLayoutProperty(RectTransform rect, System.Func<ILayoutElement, float> property, float defaultValue, out ILayoutElement source)
        {
            source = null;
            if (rect == null)
                return 0;
            float min = defaultValue;   //先将结果设为默认值
            int maxPriority = System.Int32.MinValue;    //当前最大优先级，用来比较取最大
            var components = ListPool<Component>.Get();
            rect.GetComponents(typeof(ILayoutElement), components); //取所有实现了 ILayoutElement 接口的组件

            for (int i = 0; i < components.Count; i++)  //遍历
            {
                var layoutComp = components[i] as ILayoutElement;
                if (layoutComp is Behaviour && !((Behaviour)layoutComp).isActiveAndEnabled)
                    continue;   //如果是 Behaviour，但未激活或未启用则跳过。

                int priority = layoutComp.layoutPriority;
                // If this layout components has lower priority than a previously used, ignore it.
                // 如果此布局组件的优先级低于之前的，则跳过。（保证取最高优先级的）
                if (priority < maxPriority)
                    continue;

                float prop = property(layoutComp);  //用外部提供的工具方法计算布局属性。
                // If this layout property is set to a negative value, it means it should be ignored.
                // 如果此布局属性被设置为负值，则跳过。
                if (prop < 0)
                    continue;

                // If this layout component has higher priority than all previous ones, overwrite with this one's value.
                // 如果此布局组件的优先级高于之前的所有组件，则使用此组件的值覆盖。(上面已经排除了小于的情况, 这里是大于的情况)
                if (priority > maxPriority)
                {
                    min = prop;  //赋结果
                    maxPriority = priority;  //更新当前最大优先级
                    source = layoutComp; //赋生效的组件
                }
                // If the layout component has the same priority as a previously used, use the largest of the values with the same priority.
                // 如果布局组件与之前使用的具有相同的优先级，则使用具有相同优先级的值中最大的一个。（这里是等于的情况)
                else if (prop > min)
                {
                    min = prop; //赋结果
                    source = layoutComp; //赋生效的组件
                }
            }

            ListPool<Component>.Release(components);
            return min;
        }
    }
}
