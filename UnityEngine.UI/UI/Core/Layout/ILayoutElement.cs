using UnityEngine;
using System.Collections;

namespace UnityEngine.UI
{
    // A component is treated as a layout element by the auto layout system if it implements ILayoutElement.
    // The layout system will invoke CalculateLayoutInputHorizontal before querying minWidth, preferredWidth, and flexibleWidth. It can potentially save performance if these properties are cached when CalculateLayoutInputHorizontal is invoked, so they don't need to be recalculated every time the properties are queried.
    // The layout system will invoke CalculateLayoutInputVertical before querying minHeight, preferredHeight, and flexibleHeight.It can potentially save performance if these properties are cached when CalculateLayoutInputVertical is invoked, so they don't need to be recalculated every time the properties are queried.
    // The minWidth, preferredWidth, and flexibleWidth properties should not rely on any properties of the RectTransform of the layout element, otherwise the behavior will be non-deterministic.
    // The minHeight, preferredHeight, and flexibleHeight properties may rely on horizontal aspects of the RectTransform, such as the width or the X component of the position.
    // Any properties of the RectTransforms on child layout elements may always be relied on.
    // 如果组件实现了ILayoutElement，则自动布局系统将其视为布局元素。
    // 布局系统将在查询 minWidth、preferredWidth 和 flexibleWidth 之前调用 CalculateLayoutInputHorizontal。
    // 如果在调用 CalculateLayoutInputHorizontal 时缓存这些属性，那么它可能会节省性能，因此不需要在每次查询属性时重新计算它们。
    // 布局系统将在查询 minHeight、preferredHeight 和 flexibleHeight 之前调用 CalculateLayoutInputVertical。
    // 如果在调用 CalculateLayoutInputVertical 时缓存了这些属性，那么它可能会节省性能，因此不需要在每次查询属性时重新计算它们。
    // minWidth, preferredWidth 和 flexibleWidth 属性不应该依赖于布局元素的 RectTransform 的任何属性，否则行为将是不确定的。
    // minHeight, preferredHeight 和 flexibleHeight 属性可能依赖于 RectTransform 的水平方向，比如宽度或位置的X分量。
    // 可能总是依赖任意子布局元素的 RectTransform 的属性。
    public interface ILayoutElement
    {
        // After this method is invoked, layout horizontal input properties should return up-to-date values.
        // Children will already have up-to-date layout horizontal inputs when this methods is called.
        // 调用此方法后，布局水平输入属性应该返回最新的值。
        // 调用此方法时，子节点将已经拥有最新的布局水平输入。
        void CalculateLayoutInputHorizontal();

        // After this method is invoked, layout vertical input properties should return up-to-date values.
        // Children will already have up-to-date layout vertical inputs when this methods is called.
        // 在调用这个方法之后，布局垂直输入属性应该返回最新的值。
        // 调用此方法时，子节点将已经拥有最新的布局垂直输入。
        void CalculateLayoutInputVertical();

        // The minimum width this layout element may be allocated.
        // 此布局元素可分配的最小宽度。
        float minWidth { get; }

        // The preferred width this layout element should be allocated if there is sufficient space.
        // PreferredWidth can be set to -1 to remove the size.
        // 如果有足够的空间，应该分配这个布局元素的偏好宽度。
        // PreferredWidth可以设置为-1来删除大小。
        float preferredWidth { get; }

        // The extra relative width this layout element should be allocated if there is additional available space.
        // Setting preferredWidth to -1 removed the preferredWidth.
        // 如果有额外的可用空间，应该分配这个布局元素的额外相对宽度。
        // 设置 preferredWidth 为-1将删除 preferredWidth。
        float flexibleWidth { get; }

        // The minimum height this layout element may be allocated.
        // 此布局元素可分配的最小高度。
        float minHeight { get; }

        // The preferred height this layout element should be allocated if there is sufficient space.
        // PreferredHeight can be set to -1 to remove the size.
        // 如果有足够的空间，布局元素的偏好高度应该被分配。
        // 设置 preferredHeight 为-1将删除 preferredHeight。
        float preferredHeight { get; }

        // The extra relative height this layout element should be allocated if there is additional available space.
        // 如果有额外的可用空间，应该分配这个布局元素的额外相对高度。
        float flexibleHeight { get; }

        // The layout priority of this component.
        // If multiple components on the same GameObject implement the ILayoutElement interface, the values provided by components that return a higher priority value are given priority. However, values less than zero are ignored. This way a component can override only select properties by leaving the remaning values to be -1 or other values less than zero.
        // 此组件的布局优先级。
        // 如果同一个游戏对象上的多个组件实现了ILayoutElement接口，那么返回更高优先级值的组件所提供的值将被赋予优先级。
        // 但是，小于零的值将被忽略。
        // 通过这种方式，组件可以选择性地重写部分属性，方法是：将其余值置为-1或其他小于零的值。
        int layoutPriority { get; }
    }

    // Base interface to implement by components that control the layout of RectTransforms.
    // 由控制 RectTransforms 布局的组件实现的基本接口。
    // If a component is driving its own RectTransform it should implement the interface [[ILayoutSelfController]].
    // If a component is driving the RectTransforms of its children, it should implement [[ILayoutGroup]].
    // The layout system will first invoke SetLayoutHorizontal and then SetLayoutVertical.
    // In the SetLayoutHorizontal call it is valid to call LayoutUtility.GetMinWidth, LayoutUtility.GetPreferredWidth, and LayoutUtility.GetFlexibleWidth on the RectTransform of itself or any of its children.
    // In the SetLayoutVertical call it is valid to call LayoutUtility.GetMinHeight, LayoutUtility.GetPreferredHeight, and LayoutUtility.GetFlexibleHeight on the RectTransform of itself or any of its children.
    // The component may use this information to determine the width and height to use for its own RectTransform or the RectTransforms of its children.
    // 如果一个组件正在驱动它自己的RectTransform，它应该实现接口[[ILayoutSelfController]]。
    // 如果一个组件正在驱动其子组件的RectTransforms，它应该实现[[ILayoutGroup]]。
    // 布局系统将首先调用 SetLayoutHorizontal，然后调用 SetLayoutVertical。
    // 在 SetLayoutHorizontal 调用中，可以对自身或其任何子对象的 RectTransform 调用 LayoutUtility.GetMinWidth, LayoutUtility.GetPreferredWidth, and LayoutUtility.GetFlexibleWidth。
    // 在 SetLayoutVertical 调用中，可以对自身或其任何子对象的 RectTransform 调用 LayoutUtility.GetMinHeight, LayoutUtility.GetPreferredHeight, and LayoutUtility.GetFlexibleHeight。
    // 组件可以使用此信息来确定其自己的 RectTransform 或其子组件的 RectTransform 的宽度和高度。
    public interface ILayoutController
    {
        // Callback invoked by the auto layout system which handles horizontal aspects of the layout.
        // 自动布局系统调用的回调函数，该系统处理布局的水平方向。
        void SetLayoutHorizontal();

        // Callback invoked by the auto layout system which handles vertical aspects of the layout.
        // 自动布局系统调用的回调函数，该系统处理布局的垂直方向。
        void SetLayoutVertical();
    }

    // ILayoutGroup is an ILayoutController that should drive the RectTransforms of its children.
    // ILayoutGroup derives from ILayoutController and requires the same members to be implemented.
    // ILayoutGroup 是一个 ILayoutController，它应该驱动其子 RectTransforms。
    // ILayoutGroup 派生自 ILayoutController，需要实现相同的成员。
    public interface ILayoutGroup : ILayoutController
    {
    }

    // ILayoutSelfController is an ILayoutController that should drive its own RectTransform.
    // The iLayoutSelfController derives from the base controller [[ILayoutController]] and controls the layout of a RectTransform.
    // Use the ILayoutSelfController to manipulate a GameObject’s own RectTransform component, which you attach in the Inspector.Use ILayoutGroup to manipulate RectTransforms belonging to the children of the GameObject.
    // Call ILayoutController.SetLayoutHorizontal to handle horizontal parts of the layout, and call ILayoutController.SetLayoutVertical to handle vertical parts.
    // You can change the height, width, position and rotation of the RectTransform.
    // ILayoutSelfController 是一个 ILayoutController，它应该驱动自己的 RectTransform。
    // ILayoutSelfController 派生自 ILayoutController，控制 RectTransform 的布局。
    public interface ILayoutSelfController : ILayoutController
    {
    }

    // A RectTransform will be ignored by the layout system if it has a component which implements ILayoutIgnorer.
    // A components that implements ILayoutIgnorer can be used to make a parent layout group component not consider this RectTransform part of the group. The RectTransform can then be manually positioned despite being a child GameObject of a layout group.
    // 如果 RectTransform 有一个实现 ILayoutIgnorer 的组件，那么它将被布局系统忽略。
    public interface ILayoutIgnorer
    {
        // Should this RectTransform be ignored bvy the layout system?
        // Setting this property to true will make a parent layout group component not consider this RectTransform part of the group. The RectTransform can then be manually positioned despite being a child GameObject of a layout group.
        // 这个RectTransform应该被布局系统忽略?
        // 将此属性设置为true,父布局组组件将不把这个 RectTransform 作为布局组的一部分。
        // RectTransform 将可以被手动定位，尽管它是布局组的子游戏对象。
        bool ignoreLayout { get; }
    }
}
