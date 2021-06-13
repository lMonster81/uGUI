namespace UnityEngine.EventSystems
{
    // Base behaviour that has protected implementations of Unity lifecycle functions.
    // 拥有一些 protected 的、Unity生命周期方法的 UI 基本行为类。
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

        // Returns true if the GameObject and the Component are active.
        // 若游戏对象和组件处于激活状态，则返回true。
        public virtual bool IsActive()
        {
            return isActiveAndEnabled;
        }

#if UNITY_EDITOR
        //编辑器下，脚本被加载、或 Inspector 中的任何值被修改时，方法被调用
        protected virtual void OnValidate()
        {}

        //编辑器下，脚本被加载、或 Inspector 上的Reset被点击时，方法被调用
        protected virtual void Reset()
        {}
#endif

        // This callback is called if an associated RectTransform has its dimensions changed. The call is also made to all child rect transforms, even if the child transform itselfdoesn't change - as it could have, depending on its anchoring.
        // 当关联的RectTransform的大小发生了变化时，方法被调用
        // 调用还会对所有子RectTransform生效，即使子RectTransform本身未发生变化。
        // （原因：子RectTransform的大小可能随父RectTransform的大小变化而变化，由锚点决定）
        protected virtual void OnRectTransformDimensionsChange()
        {}
        
        // 来自 Monobehaviour
        // 当关联的 Transform 的父物体变化前，方法被调用。
        // 指 Hieraychy 上的父子层级关系变化。
        // 可由 拖拽调整父子关系 或 调用 transform.SetParent()触发。
        // 若A是B的子物体，现调整为A是C的子物体。则OnBeforeTransformParentChanged()被调用，回调内A的父物体是B（即调整前的父物体）。（NRatel亲测）
        protected virtual void OnBeforeTransformParentChanged()
        {}
        
        // 来自 Monobehaviour
        // 当关联的 Transform 的父物体变化后，方法被调用。
        // 指 Hieraychy 上的父子层级关系变化。
        // 可由 拖拽调整父子关系 或 调用 transform.SetParent()触发。
        // 若A是B的子物体，现调整为A是C的子物体。则 OnTransformParentChanged() 被调用，回调内A的父物体是C（即调整后的父物体）。（NRatel亲测）
        protected virtual void OnTransformParentChanged()
        {}

        // <summary>
        // 当动画属性变化时，方法被调用。
        // </summary>
        protected virtual void OnDidApplyAnimationProperties()
        {}
        
        // 当关联的 CanvasGroup 变化时，方法被调用。
        // 指当 CanvasGroup 被启用或禁用（包括增加或移除）、或 CanvasGroup 的参数改变时被调用。（NRatel亲测）
        protected virtual void OnCanvasGroupChanged()
        {}

        // Called when the state of the parent Canvas is changed.
        // Returns true if the native representation of the behaviour has been destroyed.
        // When a parent canvas is either enabled, disabled or a nested canvas's OverrideSorting is changed this function is called. You can for example use this to modify objects below a canvas that may depend on a parent canvas - for example, if a canvas is disabled you may want to halt some processing of a UI element.
        // 当关联的 Canvas 在 Hierarchy 上变化时，方法被调用。
        // 指当父 Canvas 被启用或禁用（包括增加或移除）或者嵌套 Canvas 的 OverrideSorting 发生更改时，系统会调用此函数。（NRatel亲测）
        // 注意：从 父Canvas 中拖出，或 拖入某Canvas,不会被调用。（NRatel亲测）
        // 例如，您可以使用此函数来修改Canvas下的对象（这可能取决于父Canvas）。
        // 例如，如果 Canvas 被禁用，您可能需要停止对 UI 元素的某些处理。
        // </summary>
        protected virtual void OnCanvasHierarchyChanged()
        {}

        // Returns true if the native representation of the behaviour has been destroyed.
        // 若本身表现已被销毁，则返回true。
        // UIBehaviour 帮其子类实现了一个默认的、ICanvasElement 接口定义的 IsDestroyed() 方法。（若子类实现 ICanvasElement 接口，无须再自己实现这个接口方法）
        public bool IsDestroyed()
        {
            // Workaround for Unity native side of the object
            // having been destroyed but accessing via interface
            // won't call the overloaded ==
            // “Unity本地对象已经被销毁，但通过接口访问不会调用重载的==”的变通方法。
            return this == null;
        }
    }
}
