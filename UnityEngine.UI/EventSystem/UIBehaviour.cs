namespace UnityEngine.EventSystems
{
    /// <summary>
    /// Base behaviour that has protected implementations of Unity lifecycle functions.
    /// </summary>
    public abstract class UIBehaviour : MonoBehaviour
    {
        protected virtual void Awake()
        {}

        protected virtual void OnEnable()
        {}

        protected virtual void Start()
        {}

        protected virtual void OnDisable()
        {}

        protected virtual void OnDestroy()
        {}

        /// <summary>
        /// Returns true if the GameObject and the Component are active.
        /// </summary>
        public virtual bool IsActive()
        {
            return isActiveAndEnabled;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {}

        protected virtual void Reset()
        {}
#endif
        /// <summary>
        /// This callback is called if an associated RectTransform has its dimensions changed. The call is also made to all child rect transforms, even if the child transform itself doesn't change - as it could have, depending on its anchoring.
        /// 当关联的RectTransform的大小发生了变化时，方法被调用
        /// 调用还会对所有子RectTransform生效，即使子RectTransform本身未发生变化。
        /// （子RectTransform的大小可能随父RectTransform的大小变化而变化，由锚点决定）
        /// </summary>
        protected virtual void OnRectTransformDimensionsChange()
        {}

        /// <summary>
        /// 当关联的Transform的父物体变化前，方法被调用。
        /// </summary>
        protected virtual void OnBeforeTransformParentChanged()
        {}

        /// <summary>
        /// 来自Monobehaviour
        /// 当关联的Transform的父物体变化时，方法被调用。
        /// </summary>
        protected virtual void OnTransformParentChanged()
        {}

        /// <summary>
        /// 应用动画属性时，方法被调用。
        /// </summary>
        protected virtual void OnDidApplyAnimationProperties()
        {}

        /// <summary>
        /// 当关联的CanvasGroup变化时，方法被调用。
        /// </summary>
        protected virtual void OnCanvasGroupChanged()
        {}

        /// <summary>
        /// Called when the state of the parent Canvas is changed.
        /// 当关联的父Canvas变化时，方法被调用。
        /// </summary>
        protected virtual void OnCanvasHierarchyChanged()
        {}

        /// <summary>
        /// Returns true if the native representation of the behaviour has been destroyed.
        /// </summary>
        /// <remarks>
        /// When a parent canvas is either enabled, disabled or a nested canvas's OverrideSorting is changed this function is called. You can for example use this to modify objects below a canvas that may depend on a parent canvas - for example, if a canvas is disabled you may want to halt some processing of a UI element.
        /// </remarks>
        public bool IsDestroyed()
        {
            // Workaround for Unity native side of the object
            // having been destroyed but accessing via interface
            // won't call the overloaded ==
            return this == null;
        }
    }
}
