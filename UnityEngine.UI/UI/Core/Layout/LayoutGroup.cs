 using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace UnityEngine.UI
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]

    // Abstract base class to use for layout groups.
    // 为一组子元素布局的抽象基类
    public abstract class LayoutGroup : UIBehaviour, ILayoutElement, ILayoutGroup
    {
        [SerializeField] protected RectOffset m_Padding = new RectOffset();

        // The padding to add around the child layout elements.
        // 子布局元素边距。  
        public RectOffset padding { get { return m_Padding; } set { SetProperty(ref m_Padding, value); } }

        [SerializeField] protected TextAnchor m_ChildAlignment = TextAnchor.UpperLeft;

        // The alignment to use for the child layout elements in the layout group.
        // some child layout elements specify no flexible width and height, the children may not take up all the available space inside the layout group.
        // The alignment setting specifies how to align them within the layout group when this is the case.
        // 布局组中，子元素的对齐方式
        // 一些子布局元素没有指定灵活的宽度和高度，子布局元素可能不会占用布局组内的所有可用空间。
        // 这种情况下，“子元素对齐方式”将指定如何在布局组中对齐子元素。
        public TextAnchor childAlignment { get { return m_ChildAlignment; } set { SetProperty(ref m_ChildAlignment, value); } }

        [System.NonSerialized] private RectTransform m_Rect;
        protected RectTransform rectTransform
        {
            get
            {
                if (m_Rect == null)
                    m_Rect = GetComponent<RectTransform>();
                return m_Rect;
            }
        }

        protected DrivenRectTransformTracker m_Tracker;     // 用来控制 Transform ：https://docs.unity3d.com/cn/2018.4/ScriptReference/DrivenRectTransformTracker.html
        private Vector2 m_TotalMinSize = Vector2.zero;      //该LayoutGroup的MinSize
        private Vector2 m_TotalPreferredSize = Vector2.zero;    //该LayoutGroup的PreferredSize
        private Vector2 m_TotalFlexibleSize = Vector2.zero;  //该LayoutGroup的FlexibleSize

        [System.NonSerialized] private List<RectTransform> m_RectChildren = new List<RectTransform>(); //子布局元素
        protected List<RectTransform> rectChildren { get { return m_RectChildren; } }

        // ILayoutElement Interface
        // 实现 ILayoutElement 的方法
        // 虚方法，只是为子类提供一个通用的、计算有效的 m_RectChildren 的方法。
        public virtual void CalculateLayoutInputHorizontal()
        {
            m_RectChildren.Clear(); //计算前先清理上次计算的子 RectTransform 列表
            var toIgnoreList = ListPool<Component>.Get(); 
            for (int i = 0; i < rectTransform.childCount; i++)  //遍历子物体
            {
                var rect = rectTransform.GetChild(i) as RectTransform;
                if (rect == null || !rect.gameObject.activeInHierarchy)
                    continue;

                rect.GetComponents(typeof(ILayoutIgnorer), toIgnoreList);   //取实现了 ILayoutIgnorer 接口的子物体列表

                if (toIgnoreList.Count == 0)
                {
                    m_RectChildren.Add(rect);   //将没有实现 ILayoutIgnorer 接口的子物体加入 m_RectChildren。
                    continue;
                }

                for (int j = 0; j < toIgnoreList.Count; j++)
                {
                    var ignorer = (ILayoutIgnorer)toIgnoreList[j];
                    if (!ignorer.ignoreLayout)
                    {
                        m_RectChildren.Add(rect); //将实现了 ILayoutIgnorer 接口，但 ignoreLayout 为false 的子物体加入 m_RectChildren。
                        break;
                    }
                }
            }
            ListPool<Component>.Release(toIgnoreList);
            m_Tracker.Clear();
        }

        // 实现 ILayoutElement 的方法
        // 抽象方法
        public abstract void CalculateLayoutInputVertical();

        // See LayoutElement.minWidth
        // 实现 ILayoutElement 的方法
        public virtual float minWidth { get { return GetTotalMinSize(0); } }

        // See LayoutElement.preferredWidth
        // 实现 ILayoutElement 的方法
        public virtual float preferredWidth { get { return GetTotalPreferredSize(0); } }

        // See LayoutElement.flexibleWidth
        // 实现 ILayoutElement 的方法
        public virtual float flexibleWidth { get { return GetTotalFlexibleSize(0); } }

        // See LayoutElement.minHeight
        // 实现 ILayoutElement 的方法
        public virtual float minHeight { get { return GetTotalMinSize(1); } }

        // See LayoutElement.preferredHeight
        // 实现 ILayoutElement 的方法
        public virtual float preferredHeight { get { return GetTotalPreferredSize(1); } }

        // See LayoutElement.flexibleHeight
        // 实现 ILayoutElement 的方法
        public virtual float flexibleHeight { get { return GetTotalFlexibleSize(1); } }

        // See LayoutElement.layoutPriority
        // 实现 ILayoutElement 的方法
        public virtual int layoutPriority { get { return 0; } }

        // ILayoutController Interface
        // 实现 ILayoutElement 的方法
        public abstract void SetLayoutHorizontal();
        // 实现 ILayoutElement 的方法
        public abstract void SetLayoutVertical();

        protected LayoutGroup()
        {
            if (m_Padding == null)
                m_Padding = new RectOffset();   //默认边距
        }

        // 重写 UIBehaviour 的方法
        protected override void OnEnable()
        {
            base.OnEnable();
            SetDirty();     //组件启用时标记为需要重新布局
        }

        // 重写 UIBehaviour 的方法
        // 疑问？？？
        // 为什么OnEnable中用SetDirty(); 而这里直接调用 LayoutRebuilder.MarkLayoutForRebuild(rectTransform)
        // 估计是因为 OnDisable 时铁定要立即标记为需要重新布局
        protected override void OnDisable()
        {
            m_Tracker.Clear(); //清除DrivenRectTransformTracker对子元素的控制
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);    //组件禁用时标记为需要重新布局
            base.OnDisable();
        }
        
        // 重写 UIBehaviour 的方法
        // Callback for when properties have been changed by animation.
        // 当动画属性变化时，方法被调用。
        protected override void OnDidApplyAnimationProperties()
        {
            SetDirty();
        }

        // The min size for the layout group on the given axis.
        // 给定轴上，LayoutGroup 的最小尺寸
        protected float GetTotalMinSize(int axis)
        {
            return m_TotalMinSize[axis];
        }

        // 给定轴上，LayoutGroup 的偏好尺寸
        protected float GetTotalPreferredSize(int axis)
        {
            return m_TotalPreferredSize[axis];
        }

        // The flexible size for the layout group on the given axis.
        // 给定轴上，LayoutGroup 的灵活尺寸
        protected float GetTotalFlexibleSize(int axis)
        {
            return m_TotalFlexibleSize[axis];
        }

        // Returns the calculated position of the first child layout element along the given axis.
        // 返回沿给定轴的第一个子布局元素的位置。  
        // 参数 "axis"：The axis index. 0 is horizontal and 1 is vertical. //轴索引，0是水平的，1是垂直的。
        // 参数 "requiredSpaceWithoutPadding"：The total space required on the given axis for all the layout elements including spacing and excluding padding. //给定轴上所有布局元素所需的总空间，包括间距和边距。  
        // 返回值：The position of the first child along the given axis. //沿给定轴的第一个子节点的位置
        //---------------------------------------------------------
        // 影响顺序：子元素对齐设置 =》 布局起点 =》 整体位置对齐
        // 注意影响因素：左边距、上边距、剩余尺寸（总需要-实际）。
        protected float GetStartOffset(int axis, float requiredSpaceWithoutPadding)
        {
            float requiredSpace = requiredSpaceWithoutPadding + (axis == 0 ? padding.horizontal : padding.vertical);  //该轴上子元素需要的总尺寸 + 边距
            float availableSpace = rectTransform.rect.size[axis];   //该轴上 LayoutGroup 的实际有效尺寸
            float surplusSpace = availableSpace - requiredSpace;  //剩余尺寸（可以是负的）
            float alignmentOnAxis = GetAlignmentOnAxis(axis);   //获取小数形式的子元素对齐方式

            //水平方向从左开始，竖直方向从上开始。
            // 要计入剩余尺寸。以水平方向为例，
            // 若对齐方式为居左，则 alignmentOnAxis 为 0， 结果为 padding.left + 0，可以达到居左效果；
            // 若对齐方式为居中，则 alignmentOnAxis 为 0.5， 结果为 padding.left + 0.5*剩余距离，可以达到居中效果；
            // 若对齐方式为居右，则 alignmentOnAxis 为 1， 结果为 padding.left + 1*剩余距离，可以达到居右效果。
            return (axis == 0 ? padding.left : padding.top) + surplusSpace * alignmentOnAxis; 
        }

        // Returns the alignment on the specified axis as a fraction where 0 is left/top, 0.5 is middle, and 1 is right/bottom.
        // 以小数形式返回指定轴上的对齐方式，其中0为左/上，0.5为中，1为右/下。（水平方向：0左，0.5中，1右）（竖直方向：0上，0.5中，1下）
        // 参数 "axis"：The axis to get alignment along. 0 is horizontal and 1 is vertical.    //轴索引，0是水平的，1是垂直的。
        // 返回值：The alignment as a fraction where 0 is left/top, 0.5 is middle, and 1 is right/bottom. //小数形式的对齐方式
        protected float GetAlignmentOnAxis(int axis)
        {
            if (axis == 0)
                return ((int)childAlignment % 3) * 0.5f;  // TextAnchor 水平方向 0~8 转为 0左，0.5中，1右。
            else
                return ((int)childAlignment / 3) * 0.5f;  // TextAnchor 竖直方向 0~8 转为 0上，0.5中，1下。
        }

        // Used to set the calculated layout properties for the given axis.
        // 用于为指定轴设置布局属性
        // 参数"totalMin"：The min size for the layout group.    //LayoutGroup 的最小尺寸
        // 参数"totalPreferred"：The preferred size for the layout group. //LayoutGroup 的偏好尺寸
        // 参数"totalFlexible"：The flexible size for the layout group.  //LayoutGroup 的灵活尺寸
        // 参数"axis"：The axis to set sizes for. 0 is horizontal and 1 is vertical. //要设置尺寸的轴，0是水平的，1是垂直的。
        protected void SetLayoutInputForAxis(float totalMin, float totalPreferred, float totalFlexible, int axis)
        {
            m_TotalMinSize[axis] = totalMin;
            m_TotalPreferredSize[axis] = totalPreferred;
            m_TotalFlexibleSize[axis] = totalFlexible;
        }

        // Set the position and size of a child layout element along the given axis.
        // 沿给定轴设置子布局元素的位置和大小。  
        // 参数"rect"：The RectTransform of the child layout element. //子布局元素 RectTransform
        // 参数"axis"：The axis to set the position and size along. 0 is horizontal and 1 is vertical. //要设置尺寸的轴，0是水平的，1是垂直的。
        // 参数"pos"：The position from the left side or top. // 从左/上开始的位置
        protected void SetChildAlongAxis(RectTransform rect, int axis, float pos)
        {
            if (rect == null)
                return;

            SetChildAlongAxisWithScale(rect, axis, pos, 1.0f);
        }

        // Set the position and size of a child layout element along the given axis.
        // 沿给定轴设置子布局元素的位置和大小。  
        // 参数"rect"：The RectTransform of the child layout element. //子布局元素 RectTransform
        // 参数"axis"：The axis to set the position and size along. 0 is horizontal and 1 is vertical. //要设置尺寸的轴，0是水平的，1是垂直的。
        // 参数"pos"：The position from the left side or top. // 从左/上开始的位置
        // 参数"scaleFactor"：缩放因子
        protected void SetChildAlongAxisWithScale(RectTransform rect, int axis, float pos, float scaleFactor)
        {
            if (rect == null)
                return;

            // 驱动子物体的锚点和位置
            m_Tracker.Add(this, rect,
                DrivenTransformProperties.Anchors |
                (axis == 0 ? DrivenTransformProperties.AnchoredPositionX : DrivenTransformProperties.AnchoredPositionY));

            // Inlined rect.SetInsetAndSizeFromParentEdge(...) and refactored code in order to multiply desired size by scaleFactor.
            // sizeDelta must stay the same but the size used in the calculation of the position must be scaled by the scaleFactor.
            // 内联 rect.SetInsetAndSizeFromParentEdge(…) 并且 重构代码，以便将所需的大小乘以scaleFactor。  
            // sizelta 必须保持不变，但在计算位置时使用的大小必须由scaleFactor缩放。

            // 强制设置锚点为左上
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;

            // 设置子物体位置
            // x轴：初始位置+宽度*中心点偏移*缩放系数 (x轴是向正方向)(从左上到右下)
            // y轴：-初始位置-宽度*(1-中心点偏移)*缩放系数 (y轴是向负方向)(从左上到右下)
            Vector2 anchoredPosition = rect.anchoredPosition;
            anchoredPosition[axis] = (axis == 0) ? (pos + rect.sizeDelta[axis] * rect.pivot[axis] * scaleFactor) : (-pos - rect.sizeDelta[axis] * (1f - rect.pivot[axis]) * scaleFactor);
            rect.anchoredPosition = anchoredPosition;
        }

        // Set the position and size of a child layout element along the given axis.
        // 沿给定轴设置子布局元素的位置和大小。  
        // 参数"rect"：The RectTransform of the child layout element. //子布局元素 RectTransform
        // 参数"axis"：The axis to set the position and size along. 0 is horizontal and 1 is vertical. //要设置尺寸的轴，0是水平的，1是垂直的。
        // 参数"pos"：The position from the left side or top. // 从左/上开始的位置
        // 参数"size"：大小
        protected void SetChildAlongAxis(RectTransform rect, int axis, float pos, float size)
        {
            if (rect == null)
                return;

            SetChildAlongAxisWithScale(rect, axis, pos, size, 1.0f);
        }

        // Set the position and size of a child layout element along the given axis.
        // 沿给定轴设置子布局元素的位置和大小。  
        // 参数"rect"：The RectTransform of the child layout element. //子布局元素 RectTransform
        // 参数"axis"：The axis to set the position and size along. 0 is horizontal and 1 is vertical. //要设置尺寸的轴，0是水平的，1是垂直的。
        // 参数"pos"：The position from the left side or top. // 从左/上开始的位置
        // 参数"size"：大小
        protected void SetChildAlongAxisWithScale(RectTransform rect, int axis, float pos, float size, float scaleFactor)
        {
            if (rect == null)
                return;

            m_Tracker.Add(this, rect,
                DrivenTransformProperties.Anchors |
                (axis == 0 ?
                    (DrivenTransformProperties.AnchoredPositionX | DrivenTransformProperties.SizeDeltaX) :
                    (DrivenTransformProperties.AnchoredPositionY | DrivenTransformProperties.SizeDeltaY)
                )
            );

            // Inlined rect.SetInsetAndSizeFromParentEdge(...) and refactored code in order to multiply desired size by scaleFactor.
            // sizeDelta must stay the same but the size used in the calculation of the position must be scaled by the scaleFactor.

            // 强制设置锚点为左上
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.up;

            // 设置大小为传入的size
            Vector2 sizeDelta = rect.sizeDelta;
            sizeDelta[axis] = size;
            rect.sizeDelta = sizeDelta;

            // 算法同上
            Vector2 anchoredPosition = rect.anchoredPosition;
            anchoredPosition[axis] = (axis == 0) ? (pos + size * rect.pivot[axis] * scaleFactor) : (-pos - size * (1f - rect.pivot[axis]) * scaleFactor);
            rect.anchoredPosition = anchoredPosition;
        }

        //是否最顶层的 LayoutGroup
        private bool isRootLayoutGroup
        {
            get
            {
                Transform parent = transform.parent;
                if (parent == null)
                    return true;
                return transform.parent.GetComponent(typeof(ILayoutGroup)) == null;
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (isRootLayoutGroup)
                SetDirty();
        }

        protected virtual void OnTransformChildrenChanged()
        {
            SetDirty();
        }

        // 帮助方法，用于在给定属性发生更改时设置该属性。
        // currentValue：A reference to the member value. //成员值的引用
        // newValue：The new value.  //新值
        protected void SetProperty<T>(ref T currentValue, T newValue)
        {
            if ((currentValue == null && newValue == null) || (currentValue != null && currentValue.Equals(newValue)))  //过滤无效和未变
                return;
            currentValue = newValue;
            SetDirty();
        }

        // Mark the LayoutGroup as dirty.
        // 标记 LayoutGroup 为脏
        // 若布局重建正在进行，则延迟一帧
        protected void SetDirty()
        {
            if (!IsActive())
                return;

            if (!CanvasUpdateRegistry.IsRebuildingLayout())
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
            else
                StartCoroutine(DelayedSetDirty(rectTransform));
        }

        IEnumerator DelayedSetDirty(RectTransform rectTransform)
        {
            yield return null;
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

    #if UNITY_EDITOR
        protected override void OnValidate()
        {
            SetDirty();
        }

    #endif
    }
}
