using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    [AddComponentMenu("Layout/Layout Element", 140)]
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]

    // Add this component to a GameObject to make it into a layout element or override values on an existing layout element.
    // 将该组件添加到游戏对象中，使其成为一个布局元素或覆盖现有布局元素上的值。
    public class LayoutElement : UIBehaviour, ILayoutElement, ILayoutIgnorer
    {
        [SerializeField] private bool m_IgnoreLayout = false;   //是否忽略布局
        [SerializeField] private float m_MinWidth = -1;         //最小宽度
        [SerializeField] private float m_MinHeight = -1;        //最小高度
        [SerializeField] private float m_PreferredWidth = -1;   //偏好宽度
        [SerializeField] private float m_PreferredHeight = -1;  //偏好高度
        [SerializeField] private float m_FlexibleWidth = -1;    //灵活宽度
        [SerializeField] private float m_FlexibleHeight = -1;   //灵活高度
        [SerializeField] private int m_LayoutPriority = 1;      //布局优先级

        // 实现 ILayoutIgnorer 的接口
        // Should this RectTransform be ignored by the layout system?
        // Setting this property to true will make a parent layout group component not consider this RectTransform part of the group.
        // The RectTransform can then be manually positioned despite being a child GameObject of a layout group.
        // 这个RectTransform应该被布局系统忽略?
        // 将此属性设置为true,父布局组组件将不把这个 RectTransform 作为布局组的一部分。
        // RectTransform 将可以被手动定位，尽管它是布局组的子游戏对象。
        public virtual bool ignoreLayout { get { return m_IgnoreLayout; } set { if (SetPropertyUtility.SetStruct(ref m_IgnoreLayout, value)) SetDirty(); } }

        // 实现 ILayoutElement 的接口
        public virtual void CalculateLayoutInputHorizontal() { }

        // 实现 ILayoutElement 的接口
        public virtual void CalculateLayoutInputVertical() { }

        // 实现 ILayoutElement 的接口
        // The minimum width this layout element may be allocated.
        // 修改(set)时，若有变化则在修改后调用 SetDirty(); 若无变化直接忽略。
        public virtual float minWidth { get { return m_MinWidth; } set { if (SetPropertyUtility.SetStruct(ref m_MinWidth, value)) SetDirty(); } }

        // 实现 ILayoutElement 的接口
        // The minimum height this layout element may be allocated.
        // 修改(set)时，若有变化则在修改后调用 SetDirty(); 若无变化直接忽略。
        public virtual float minHeight { get { return m_MinHeight; } set { if (SetPropertyUtility.SetStruct(ref m_MinHeight, value)) SetDirty(); } }

        // 实现 ILayoutElement 的接口
        // The preferred width this layout element should be allocated if there is sufficient space. The preferredWidth can be set to -1 to remove the size.
        // 修改(set)时，若有变化则在修改后调用 SetDirty(); 若无变化直接忽略。
        public virtual float preferredWidth { get { return m_PreferredWidth; } set { if (SetPropertyUtility.SetStruct(ref m_PreferredWidth, value)) SetDirty(); } }

        // 实现 ILayoutElement 的接口
        // The preferred height this layout element should be allocated if there is sufficient space.
        // 修改(set)时，若有变化则在修改后调用 SetDirty(); 若无变化直接忽略。
        public virtual float preferredHeight { get { return m_PreferredHeight; } set { if (SetPropertyUtility.SetStruct(ref m_PreferredHeight, value)) SetDirty(); } }

        // 实现 ILayoutElement 的接口
        // The extra relative width this layout element should be allocated if there is additional available space.
        // 修改(set)时，若有变化则在修改后调用 SetDirty(); 若无变化直接忽略。
        public virtual float flexibleWidth { get { return m_FlexibleWidth; } set { if (SetPropertyUtility.SetStruct(ref m_FlexibleWidth, value)) SetDirty(); } }

        // 实现 ILayoutElement 的接口
        // The extra relative height this layout element should be allocated if there is additional available space.
        // 修改(set)时，若有变化则在修改后调用 SetDirty(); 若无变化直接忽略。
        public virtual float flexibleHeight { get { return m_FlexibleHeight; } set { if (SetPropertyUtility.SetStruct(ref m_FlexibleHeight, value)) SetDirty(); } }

        // 实现 ILayoutElement 的接口
        // The Priority of layout this element has.
        // 修改(set)时，若有变化则在修改后调用 SetDirty(); 若无变化直接忽略。
        public virtual int layoutPriority { get { return m_LayoutPriority; } set { if (SetPropertyUtility.SetStruct(ref m_LayoutPriority, value)) SetDirty(); } }


        protected LayoutElement()
        { }

        // 重写 UIBehaviour 的方法
        protected override void OnEnable()
        {
            base.OnEnable();
            SetDirty();
        }

        // 重写 UIBehaviour 的方法
        protected override void OnTransformParentChanged()
        {
            SetDirty();
        }

        // 重写 UIBehaviour 的方法
        protected override void OnDisable()
        {
            SetDirty();
            base.OnDisable();
        }

        // 重写 UIBehaviour 的方法
        protected override void OnDidApplyAnimationProperties()
        {
            SetDirty();
        }

        // 重写 UIBehaviour 的方法
        protected override void OnBeforeTransformParentChanged()
        {
            SetDirty();
        }

        // Mark the LayoutElement as dirty.
        // This will make the auto layout system process this element on the next layout pass.
        // This method should be called by the LayoutElement whenever a change is made that potentially affects the layout.
        // 标记布局元素为脏。
        // 这将使自动布局系统在下一次布局中处理这个元素。
        // 当发生可能影响布局的更改时，LayoutElement应该调用此方法。
        protected void SetDirty()
        {
            if (!IsActive())
                return;
            LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
        }

#if UNITY_EDITOR
        // 重写 UIBehaviour 的方法
        protected override void OnValidate()
        {
            SetDirty();
        }

#endif
    }
}
