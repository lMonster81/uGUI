using System;

namespace UnityEngine.UI
{
    // This element is capable of being masked out.
    // 这个元素可以被遮罩。（目前只有MaskableGraphic实现它）
    public interface IMaskable
    {
        // Recalculate masking for this element and all children elements.
        // Use this to update the internal state (recreate materials etc).
        // 重新计算此元素和所有子元素的遮罩。
        // 更新内部状态(重新创建材质等)。
        void RecalculateMasking();
    }
}
