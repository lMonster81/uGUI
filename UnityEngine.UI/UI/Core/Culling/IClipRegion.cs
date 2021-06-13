namespace UnityEngine.UI
{
    // Interface that can be used to recieve clipping callbacks as part of the canvas update loop.
    // 可用于接受裁剪回调（裁剪是画布更新循环的一部分）的接口。
    // 实现该接口的元素（目前只有RectMask2D），将对其“实现了 IClippable 接口”的子物体进行裁剪。
    public interface IClipper
    {
        // Function to to cull / clip children elements.
        // Called after layout and before Graphic update of the Canvas update loop.
        // 在 CanvasUpdate 循环中, 方法在 LayoutUpdate 与 GraphicUpdate 之间被调用。
        void PerformClipping();
    }

    // Interface for elements that can be clipped if they are under an IClipper
    // 实现该接口的元素（目前只有MaskableGraphic），如果作为“实现了 IClipper 接口”的元素（目前只有RectMask2D）的子物体，则可被裁剪。
    public interface IClippable
    {
        // 实现了 IClippable 接口的组件所在的 GameObject
        GameObject gameObject { get; }

        // Will be called when the state of a parent IClippable changed.
        // “实现了 IClipper 接口”的元素（目前只有RectMask2D）状态改变时，调用其“实现了 IClippable 接口”的子物体的该方法。
        // 状态改变包括：OnEnable、OnDisable、OnValidate（编辑器下）。
        void RecalculateClipping();

        // The RectTransform of the clippable.
        // 实现了 IClippable 接口的组件关联的 RectTransform。
        RectTransform rectTransform { get; }

        // Clip and cull the IClippable given a specific clipping rect
        // 裁剪和剔除 IClippable 给定的裁剪矩形
        // 参数"clipRect"：The Rectangle in which to clip against. 裁剪本物体的矩形 （来自RectMask2D）。
        // 参数"validRect"：Is the Rect valid. If not then the rect has 0 size. 矩形是否有效。若无效，则矩形的大小视为0（不裁剪）。
        void Cull(Rect clipRect, bool validRect);

        // Set the clip rect for the IClippable.
        // 设置裁剪矩形。
        // 参数"value"：The Rectangle for the clipping.  裁剪本物体的矩形 （来自RectMask2D）。
        // 参数"validRect"：Is the rect valid.  矩形是否有效。
        void SetClipRect(Rect value, bool validRect);
    }
}
