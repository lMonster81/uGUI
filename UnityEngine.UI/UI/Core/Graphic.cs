using System;
#if UNITY_EDITOR
using System.Reflection;
#endif
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI.CoroutineTween;

namespace UnityEngine.UI
{
    // Base class for all UI components that should be derived from when creating new Graphic types.
    // 创建新的 Graphic 图形类型时应该派生的、所有UI组件的基类。
    // 当创建可视化UI组件时，您应该从这个类继承。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    /// <summary>
    ///   Base class for all visual UI Component.
    ///   When creating visual UI components you should inherit from this class.
    /// </summary>
    /// <example>
    /// Below is a simple example that draws a colored quad inside the Rect Transform area.
    /// <code>
    /// using UnityEngine;
    /// using UnityEngine.UI;
    ///
    /// [ExecuteInEditMode]
    /// public class SimpleImage : Graphic
    /// {
    ///     protected override void OnPopulateMesh(VertexHelper vh)
    ///     {
    ///         Vector2 corner1 = Vector2.zero;
    ///         Vector2 corner2 = Vector2.zero;
    ///
    ///         corner1.x = 0f;
    ///         corner1.y = 0f;
    ///         corner2.x = 1f;
    ///         corner2.y = 1f;
    ///
    ///         corner1.x -= rectTransform.pivot.x;
    ///         corner1.y -= rectTransform.pivot.y;
    ///         corner2.x -= rectTransform.pivot.x;
    ///         corner2.y -= rectTransform.pivot.y;
    ///
    ///         corner1.x *= rectTransform.rect.width;
    ///         corner1.y *= rectTransform.rect.height;
    ///         corner2.x *= rectTransform.rect.width;
    ///         corner2.y *= rectTransform.rect.height;
    ///
    ///         vh.Clear();
    ///
    ///         UIVertex vert = UIVertex.simpleVert;
    ///
    ///         vert.position = new Vector2(corner1.x, corner1.y);
    ///         vert.color = color;
    ///         vh.AddVert(vert);
    ///
    ///         vert.position = new Vector2(corner1.x, corner2.y);
    ///         vert.color = color;
    ///         vh.AddVert(vert);
    ///
    ///         vert.position = new Vector2(corner2.x, corner2.y);
    ///         vert.color = color;
    ///         vh.AddVert(vert);
    ///
    ///         vert.position = new Vector2(corner2.x, corner1.y);
    ///         vert.color = color;
    ///         vh.AddVert(vert);
    ///
    ///         vh.AddTriangle(0, 1, 2);
    ///         vh.AddTriangle(2, 3, 0);
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class Graphic  : UIBehaviour, ICanvasElement
    {
        static protected Material s_DefaultUI = null;       //默认UI材质，Canvas.GetDefaultCanvasMaterial()
        static protected Texture2D s_WhiteTexture = null;   //默认空白贴图，Texture2D.whiteTexture
        
        // Default material used to draw UI elements if no explicit material was specified.
        // 如果没有明确指定材质，则默认材质用于绘制UI元素
        static public Material defaultGraphicMaterial
        {
            get
            {
                if (s_DefaultUI == null)
                    s_DefaultUI = Canvas.GetDefaultCanvasMaterial();
                return s_DefaultUI;
            }
        }

        // Cached and saved values 疑问??? 特性作用？
        [FormerlySerializedAs("m_Mat")]
        [SerializeField] protected Material m_Material;         //当前材质

        [SerializeField] private Color m_Color = Color.white;   //当前颜色

        [NonSerialized] protected bool m_SkipLayoutUpdate;      //是否跳过Layout更新，置为true后本帧内有效，执行跳过后立刻置回false。
        [NonSerialized] protected bool m_SkipMaterialUpdate;    //是否跳过材质更新，置为true后本帧内有效，执行跳过后立刻置回false。

        /// <summary>
        /// Base color of the Graphic.
        /// </summary>
        /// <remarks>
        /// The builtin UI Components use this as their vertex color. Use this to fetch or change the Color of visual UI elements, such as an Image.
        /// </remarks>
        /// <example>
        /// <code>
        /// //Place this script on a GameObject with a Graphic component attached e.g. a visual UI element (Image).
        ///
        /// using UnityEngine;
        /// using UnityEngine.UI;
        ///
        /// public class Example : MonoBehaviour
        /// {
        ///     Graphic m_Graphic;
        ///     Color m_MyColor;
        ///
        ///     void Start()
        ///     {
        ///         //Fetch the Graphic from the GameObject
        ///         m_Graphic = GetComponent<Graphic>();
        ///         //Create a new Color that starts as red
        ///         m_MyColor = Color.red;
        ///         //Change the Graphic Color to the new Color
        ///         m_Graphic.color = m_MyColor;
        ///     }
        ///
        ///     // Update is called once per frame
        ///     void Update()
        ///     {
        ///         //When the mouse button is clicked, change the Graphic Color
        ///         if (Input.GetKey(KeyCode.Mouse0))
        ///         {
        ///             //Change the Color over time between blue and red while the mouse button is pressed
        ///             m_MyColor = Color.Lerp(Color.red, Color.blue, Mathf.PingPong(Time.time, 1));
        ///         }
        ///         //Change the Graphic Color to the new Color
        ///         m_Graphic.color = m_MyColor;
        ///     }
        /// }
        /// </code>
        /// </example>
        public virtual Color color { get { return m_Color; } set { if (SetPropertyUtility.SetColor(ref m_Color, value)) SetVerticesDirty(); } }

        [SerializeField] private bool m_RaycastTarget = true;   //是否作为射线检测目标

        // Should this graphic be considered a target for raycasting?
        // 本 graphic 是否应该被认为是射线检测的目标?
        public virtual bool raycastTarget { get { return m_RaycastTarget; } set { m_RaycastTarget = value; } }

        [NonSerialized] private RectTransform m_RectTransform;      //与自身同级的、依赖的RectTransform
        [NonSerialized] private CanvasRenderer m_CanvasRenderer;    //与自身同级的、依赖的CanvasRenderer
        [NonSerialized] private Canvas m_Canvas;                    //自身所属的、第一个active和enabled均为true的Canvas，可为null

        [NonSerialized] private bool m_VertsDirty;       //顶点脏标记，默认false, SetVerticesDirty()置为true, Rebuild()中UpdateGeometry()执行后置回false
        [NonSerialized] private bool m_MaterialDirty;    //材质脏标记，默认false, SetVerticesDirty()置为true, Rebuild()中UpdateMaterial()执行后置回false

        [NonSerialized] protected UnityAction m_OnDirtyLayoutCallback;      //SetLayoutDirty() 被调用时触发该回调
        [NonSerialized] protected UnityAction m_OnDirtyVertsCallback;       //SetVerticesDirty() 被调用时触发该回调
        [NonSerialized] protected UnityAction m_OnDirtyMaterialCallback;    //SetMaterialDirty() 被调用时触发该回调

        [NonSerialized] protected static Mesh s_Mesh;       //默认创建的、所有UI元素共享的 Mesh，HideFlags.HideAndDontSave。（新Scene中保留，Hierarchy上隐藏）
        [NonSerialized] private static readonly VertexHelper s_VertexHelper = new VertexHelper();   //顶点帮助工具类静态实例

        [NonSerialized] protected Mesh m_CachedMesh;        //疑问??? 没找到任何引用
        [NonSerialized] protected Vector2[] m_CachedUvs;    //疑问??? 没找到任何引用
        // Tween controls for the Graphic
        [NonSerialized] private readonly TweenRunner<ColorTween> m_ColorTweenRunner;    //颜色渐变动画运行器，用于执行颜色渐变/透明度渐变。

        protected bool useLegacyMeshGeneration { get; set; }        //是否使用旧的Mesh创建方式，默认为true。

        // Called by Unity prior to deserialization,
        // should not be called by users
        // 疑问??? 继承自Monobehaviour的类 构造函数?
        // 创建 m_ColorTweenRunner，默认 useLegacyMeshGeneration为true。
        protected Graphic()
        {
            if (m_ColorTweenRunner == null)
                m_ColorTweenRunner = new TweenRunner<ColorTween>();
            m_ColorTweenRunner.Init(this);
            useLegacyMeshGeneration = true;
        }

        // Set all properties of the Graphic dirty and needing rebuilt. Dirties Layout, Vertices, and Materials.
        // 设置 Graphic 的所有脏标记为脏（需要重建）。 Layout脏、Materials脏、Vertices脏。
        // 这里不用考虑顺序。因为只是做标记。真正重建的时候要保证顺序。
        public virtual void SetAllDirty()
        {
            // Optimization: Graphic layout doesn't need recalculation if the underlying Sprite is the same size with the same texture.
            // (e.g. Sprite sheet texture animation)
            // 优化:如果基础精灵具有相同的大小和纹理，那么 Graphic layout 便不需要重新计算。

            // LayoutUpdate 和 MaterialUpdate 可跳过。能跳过的应尽量跳过。
            if (m_SkipLayoutUpdate)
            {
                m_SkipLayoutUpdate = false;
            }
            else
            {
                SetLayoutDirty();
            }

            if (m_SkipMaterialUpdate)
            {
                m_SkipMaterialUpdate = false;
            }
            else
            {
                SetMaterialDirty();
            }

            SetVerticesDirty();
        }

        // Mark the layout as dirty and needing rebuilt.
        // Send a OnDirtyLayoutCallback notification if any elements are registered. See RegisterDirtyLayoutCallback
        // 1、标记为需要重新布局。
        // 2、触发一个 布局脏事件。
        public virtual void SetLayoutDirty()
        {
            if (!IsActive())
                return;

            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);

            if (m_OnDirtyLayoutCallback != null)
                m_OnDirtyLayoutCallback();
        }

        // Mark the vertices as dirty and needing rebuilt.
        // Send a OnDirtyVertsCallback notification if any elements are registered. See RegisterDirtyVerticesCallback
        // 1、标记 顶点脏标记 为脏。
        // 2、在 CanvasUpdateRegistry 中注册。（使图形更新生效）。
        // 3、触发一个 顶点脏事件。
        public virtual void SetVerticesDirty()
        {
            if (!IsActive())
                return;

            m_VertsDirty = true;
            CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);

            if (m_OnDirtyVertsCallback != null)
                m_OnDirtyVertsCallback();
        }

        // 1、标记 材质脏标记 为脏。
        // 2、在 CanvasUpdateRegistry 中注册。（使图形更新生效）。
        // 3、触发一个 材质脏事件。
        public virtual void SetMaterialDirty()
        {
            if (!IsActive())
                return;

            m_MaterialDirty = true;
            CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);

            if (m_OnDirtyMaterialCallback != null)
                m_OnDirtyMaterialCallback();
        }

        //重写 UIBehaviour 的方法
        //RectTransform大小发生变化时（具体看UIBehaviour里的注释），
        //1、标记顶点脏（需要重新创建自身Mesh）。
        //2、标记布局脏（会导致布局改变）。
        protected override void OnRectTransformDimensionsChange()
        {
            if (gameObject.activeInHierarchy)
            {
                // prevent double dirtying...
                if (CanvasUpdateRegistry.IsRebuildingLayout())
                    SetVerticesDirty();
                else
                {
                    SetVerticesDirty();
                    SetLayoutDirty();
                }
            }
        }

        //重写 UIBehaviour 的方法
        //父物体改变前（具体看UIBehaviour里的注释），
        //1、清除GraphicRegistry中的注册（使原位置射线检测失效）。
        //2、标记重新布局（会导致布局改变）（不会触发布局为脏回调）。
        protected override void OnBeforeTransformParentChanged()
        {
            GraphicRegistry.UnregisterGraphicForCanvas(canvas, this);
            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        //重写 UIBehaviour 的方法
        //父物体改变后（具体看UIBehaviour里的注释），
        //1、清除缓存的 m_Canvas 并重新查找和缓存（因为位置变了，所属Canvas可能变化）。
        //2、在GraphicRegistry中注册。（使新位置射线检测生效）。
        //3、标记为全脏。
        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();

            m_Canvas = null;

            if (!IsActive())
                return;

            CacheCanvas();
            GraphicRegistry.RegisterGraphicForCanvas(canvas, this);
            SetAllDirty();
        }

        // Absolute depth of the graphic, used by rendering and events -- lowest to highest.
        // The depth is relative to the first root canvas.
        // This value is used to determine draw and event ordering.
        // Graphic 的绝对深度，渲染和事件使用它——从低到高。
        // 深度总是相对于第一个根 Canvas。
        // 这个值用于确定绘制和事件排序。
        /// <example>
        /// Canvas
        ///  Graphic - 1
        ///  Graphic - 2
        ///  Nested Canvas
        ///     Graphic - 3
        ///     Graphic - 4
        ///  Graphic - 5
        /// </example>
        public int depth { get { return canvasRenderer.absoluteDepth; } }

        // The RectTransform component used by the Graphic.Cached for speed.
        // Graphic 关联的 RectTransform 组件。为了速度而缓存。
        public RectTransform rectTransform
        {
            get
            {
                // The RectTransform is a required component that must not be destroyed. Based on this assumption, a
                // null-reference check is sufficient.
                if (ReferenceEquals(m_RectTransform, null))
                {
                    m_RectTransform = GetComponent<RectTransform>();
                }
                return m_RectTransform;
            }
        }

        // A reference to the Canvas this Graphic is rendering to.
        // In the situation where the Graphic is used in a hierarchy with multiple Canvases, the Canvas closest to the root will be used.
        // Graphic 所在的 Canvas。在有多个Canvas嵌套时，使用最接近的Canvas。
        public Canvas canvas
        {
            get
            {
                if (m_Canvas == null)
                    CacheCanvas();
                return m_Canvas;
            }
        }

        private void CacheCanvas()
        {
            var list = ListPool<Canvas>.Get();
            gameObject.GetComponentsInParent(false, list);
            if (list.Count > 0)
            {
                // Find the first active and enabled canvas.
                for (int i = 0; i < list.Count; ++i)
                {
                    if (list[i].isActiveAndEnabled)
                    {
                        m_Canvas = list[i];
                        break;
                    }
                }
            }
            else
            {
                m_Canvas = null;
            }

            ListPool<Canvas>.Release(list);
        }

        // A reference to the CanvasRenderer populated by this Graphic.
        // Graphic 所在的 CanvasRenderer（同级，每个Graphic一一对应一个CanvasRenderer）。
        public CanvasRenderer canvasRenderer
        {
            get
            {
                // The CanvasRenderer is a required component that must not be destroyed. Based on this assumption, a
                // null-reference check is sufficient.
                // CanvasRenderer一个必须的、不能被销毁的组件。基于这个假设，空引用检查就足够了
                if (ReferenceEquals(m_CanvasRenderer, null))
                {
                    m_CanvasRenderer = GetComponent<CanvasRenderer>();
                }
                return m_CanvasRenderer;
            }
        }
        
        // Returns the default material for the graphic.
        // 本Graphic默认采用的材质，默认为defaultGraphicMaterial（可重写）
        public virtual Material defaultMaterial
        {
            get { return defaultGraphicMaterial; }
        }
        
        // The Material set by the user
        // 当前材质set/get。set时触发SetMaterialDirty(); get时若为空则取defaultMaterial（可重写）
        public virtual Material material
        {
            get
            {
                return (m_Material != null) ? m_Material : defaultMaterial;
            }
            set
            {
                if (m_Material == value)
                    return;

                m_Material = value;
                SetMaterialDirty();
            }
        }

        // The material that will be sent for Rendering (Read only).
        // This is the material that actually gets sent to the CanvasRenderer. By default it's the same as [[Graphic.material]]. When extending Graphic you can override this to send a different material to the CanvasRenderer than the one set by Graphic.material. This is useful if you want to modify the user set material in a non destructive manner.
        // 实际被发送到CanvasRenderer的、被 IMaterialModifier 修改后的材质。（只读、可重写）
        // 这是实际发送到CanvasRenderer的材质。默认情况下，它与 Graphic.material 相同。
        // 当扩展Graphic时，你可以覆盖它，发送一个不同于 Graphic.material 设置的材质到 CanvasRenderer。
        // 如果你想以非破坏性的方式修改用户设置的材质，这是很有用的。
        public virtual Material materialForRendering
        {
            get
            {
                var components = ListPool<Component>.Get(); 
                GetComponents(typeof(IMaterialModifier), components);   //取自身所有实现了 IMaterialModifier 接口的组件。

                var currentMat = material;
                for (var i = 0; i < components.Count; i++)  //遍历处理，（即：有多个时只有一个会生效，其他的被覆盖。
                    currentMat = (components[i] as IMaterialModifier).GetModifiedMaterial(currentMat);
                ListPool<Component>.Release(components);
                return currentMat;
            }
        }

        // The graphic's texture. (Read Only).
        // This is the Texture that gets passed to the CanvasRenderer, Material and then Shader _MainTex.
        // When implementing your own Graphic you can override this to control which texture goes through the UI Rendering pipeline.
        // Bear in mind that Unity tries to batch UI elements together to improve performance, so its ideal to work with atlas to reduce the number of draw calls.
        // 图形的纹理。
        // 这是被传递到 CanvasRenderer, Material 和 Shader _MainTex 的纹理。
        // 当你实现自己的图形时，你可以覆盖它，来控制怎样的纹理进入UI渲染管线。
        // 请记住，Unity试图批量处理UI元素以提高性能，所以它的理想工作方式是与图集协作，以减少 draw call 的数量。
        public virtual Texture mainTexture
        {
            get
            {
                return s_WhiteTexture;
            }
        }

        // Mark the Graphic and the canvas as having been changed.
        // 标记 Graphic 和 Canvas 已改变。
        //1、查找和缓存 m_Canvas。
        //2、在 GraphicRegistry 中注册。（使射线检测生效）
        //3、在 GraphicRebuildTracker 追踪。（仅编辑器下）
        //4、为 s_WhiteTexture 设置初始值 Texture2D.whiteTexture。
        //5、标记为全脏。
        protected override void OnEnable()
        {
            base.OnEnable();   //疑问??? 父方法是空的为何要调？
            CacheCanvas();
            GraphicRegistry.RegisterGraphicForCanvas(canvas, this);

#if UNITY_EDITOR
            GraphicRebuildTracker.TrackGraphic(this);
#endif
            if (s_WhiteTexture == null)
                s_WhiteTexture = Texture2D.whiteTexture;

            SetAllDirty();
        }

        // Clear references.
        // 清除引用
        //1、清除 GraphicRebuildTracker 追踪。（仅编辑器下）
        //2、清除 GraphicRegistry 中的注册。
        //3、清除 CanvasUpdateRegistry 中的注册。
        //4、清理 canvasRenderer。
        //5、标记重新布局（会导致布局改变）（不会触发布局为脏回调）。
        protected override void OnDisable()
        {
#if UNITY_EDITOR
            GraphicRebuildTracker.UnTrackGraphic(this);
#endif
            GraphicRegistry.UnregisterGraphicForCanvas(canvas, this);
            CanvasUpdateRegistry.UnRegisterCanvasElementForRebuild(this);

            if (canvasRenderer != null)
                canvasRenderer.Clear();

            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);

            base.OnDisable();
        }

        //1、销毁缓存的 Mesh???
        protected override void OnDestroy()
        {
            if (m_CachedMesh)
                Destroy(m_CachedMesh);
            m_CachedMesh = null;

            base.OnDestroy();
        }

        //重写 UIBehaviour 的方法
        //当关联的 Canvas 在 Hierarchy 上变化时（具体看UIBehaviour里的注释），
        //1、清除缓存的 m_Canvas 并重新查找和缓存。
        //2、若新的 Canvas 与原来的不同，更新GraphicRegistry中注册（更新射线的检测）。
        protected override void OnCanvasHierarchyChanged()
        {
            // Use m_Cavas so we dont auto call CacheCanvas
            Canvas currentCanvas = m_Canvas;

            // Clear the cached canvas. Will be fetched below if active.
            m_Canvas = null;

            if (!IsActive())
                return;  

            CacheCanvas();

            if (currentCanvas != m_Canvas)
            {
                GraphicRegistry.UnregisterGraphicForCanvas(currentCanvas, this);

                // Only register if we are active and enabled as OnCanvasHierarchyChanged can get called
                // during object destruction and we dont want to register ourself and then become null.
                if (IsActive())
                    GraphicRegistry.RegisterGraphicForCanvas(canvas, this);
            }
        }

        // This method must be called when <c>CanvasRenderer.cull</c> is modified.
        // This can be used to perform operations that were previously skipped because the <c>Graphic</c> was culled.
        // 当 CanvasRenderer.cull 被修改时，方法被调用。 CanvasRenderer.cull：表示是否忽略该渲染器发射的几何形状。
        // 这可以用于执行因 Graphic 被剔除而在之前跳过的操作。
        public virtual void OnCullingChanged()
        {
            if (!canvasRenderer.cull && (m_VertsDirty || m_MaterialDirty))
            {
                // When we were culled, we potentially skipped calls to <c>Rebuild</c>.
                // 当我们处理剔除时，可能会跳过对 Rebuild 的调用。
                //（指：如果 Graphic 被剔除，则不需要调用 SetVerticesDirty() 和 SetMaterialDirty()。 ）
                // 因此，当剔除状态变化时（变为不再剔除），要重新执行跳过的步骤。
                CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this);
            }
        }

        //实现 ICanvasElement 的接口
        // Rebuilds the graphic geometry and its material on the PreRender cycle.
        // 在 CanvasUpdate 的 PreRender 阶段重建图形几何 和 材质
        //1、顶点脏则更新图形几何
        //2、材质脏则更新材质
        public virtual void Rebuild(CanvasUpdate update)
        {
            if (canvasRenderer == null || canvasRenderer.cull)
                return;

            switch (update)
            {
                case CanvasUpdate.PreRender:
                    if (m_VertsDirty)
                    {
                        UpdateGeometry();
                        m_VertsDirty = false;
                    }
                    if (m_MaterialDirty)
                    {
                        UpdateMaterial();
                        m_MaterialDirty = false;
                    }
                    break;
            }
        }

        //实现 ICanvasElement 的接口
        public virtual void LayoutComplete()
        {}

        //实现 ICanvasElement 的接口
        public virtual void GraphicUpdateComplete()
        {}

        // Call to update the Material of the graphic onto the CanvasRenderer.
        // 将 Graphic 的 Material 更新至 CanvasRenderer 上。
        //1、设置 canvasRenderer 的材质数量为1。
        //2、设置 canvasRenderer 的材质为 materialForRendering （经过修改的最终材质）。
        //3、设置 canvasRenderer 的 Texture 为 Graphic 的 mainTexture。
        protected virtual void UpdateMaterial()
        {
            if (!IsActive())
                return;

            canvasRenderer.materialCount = 1;
            canvasRenderer.SetMaterial(materialForRendering, 0);
            canvasRenderer.SetTexture(mainTexture);
        }

        // Call to update the geometry of the Graphic onto the CanvasRenderer.
        // 将 Graphic 的 Mesh 更新至 CanvasRenderer 上。
        protected virtual void UpdateGeometry()
        {
            if (useLegacyMeshGeneration)
            {
                DoLegacyMeshGeneration();
            }
            else
            {
                DoMeshGeneration();
            }
        }

        //新的 Mesh 创建方法
        //1、rectTransform 存在 且宽高为正时才创建，否则调用 s_VertexHelper 的清理方法。
        //2、调用 OnPopulateMesh(VertexHelper vh) 执行创建。
        //3、取“实现了接口 IMeshModifier”的组件，对 Mesh 修改。
        //4、设置 workerMesh 到 canvasRenderer 上。
        // 新旧创建方法的唯一区别是：新的创建方法引入了 VertexHelper。
        private void DoMeshGeneration()
        {
            if (rectTransform != null && rectTransform.rect.width >= 0 && rectTransform.rect.height >= 0)
                OnPopulateMesh(s_VertexHelper);
            else
                s_VertexHelper.Clear(); // clear the vertex helper so invalid graphics dont draw.

            var components = ListPool<Component>.Get();
            GetComponents(typeof(IMeshModifier), components);

            for (var i = 0; i < components.Count; i++)
                ((IMeshModifier)components[i]).ModifyMesh(s_VertexHelper);

            ListPool<Component>.Release(components);

            s_VertexHelper.FillMesh(workerMesh);
            canvasRenderer.SetMesh(workerMesh);
        }

        //旧的 Mesh 创建方法（）
        //1、rectTransform 存在 且宽高为正时才创建，否则调用 Mesh 的清理方法。
        //2、调用 OnPopulateMesh(Mesh m) 执行创建。
        //3、取“实现了接口 IMeshModifier”的组件，对 Mesh 修改。
        //4、设置 workerMesh 到 canvasRenderer 上。
        private void DoLegacyMeshGeneration()
        {
            if (rectTransform != null && rectTransform.rect.width >= 0 && rectTransform.rect.height >= 0)
            {
#pragma warning disable 618         //疑问??? 618是什么警告？
                OnPopulateMesh(workerMesh);
#pragma warning restore 618
            }
            else
            {
                workerMesh.Clear();
            }

            var components = ListPool<Component>.Get();
            GetComponents(typeof(IMeshModifier), components);

            for (var i = 0; i < components.Count; i++)
            {
#pragma warning disable 618
                ((IMeshModifier)components[i]).ModifyMesh(workerMesh);
#pragma warning restore 618
            }

            ListPool<Component>.Release(components);
            canvasRenderer.SetMesh(workerMesh);
        }

        //工作Mesh，所有UI元素共享的 Mesh，HideFlags.HideAndDontSave。（新Scene中保留，Hierarchy上隐藏）
        protected static Mesh workerMesh
        {
            get
            {
                if (s_Mesh == null)
                {
                    s_Mesh = new Mesh();
                    s_Mesh.name = "Shared UI Mesh";
                    s_Mesh.hideFlags = HideFlags.HideAndDontSave;
                }
                return s_Mesh;
            }
        }

        //废弃方法。
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Use OnPopulateMesh instead.", true)]
        protected virtual void OnFillVBO(System.Collections.Generic.List<UIVertex> vbo) {}

        //废弃方法，作用同 OnPopulateMesh(VertexHelper vh)
        [Obsolete("Use OnPopulateMesh(VertexHelper vh) instead.", false)]
        protected virtual void OnPopulateMesh(Mesh m)
        {
            OnPopulateMesh(s_VertexHelper);
            s_VertexHelper.FillMesh(m);
        }

        // Callback function when a UI element needs to generate vertices. Fills the vertex buffer data.
        // 当UI元素需要创建顶点时的回调函数。填充 vertex buffer data。
        // 参数"vh"： 顶点帮助工具类
        // 备注：例如，由 Text、Image、RawImage 用其来生成特定于它们自己的顶点。
        //这里只是创建了一个默认的矩形（两个三角形）
        protected virtual void OnPopulateMesh(VertexHelper vh)
        {
            var r = GetPixelAdjustedRect();
            var v = new Vector4(r.x, r.y, r.x + r.width, r.y + r.height);

            Color32 color32 = color;
            vh.Clear(); //vh 所有 Graphic 共用，故每次使用都要先Clear。
            vh.AddVert(new Vector3(v.x, v.y), color32, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(v.x, v.w), color32, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(v.z, v.w), color32, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(v.z, v.y), color32, new Vector2(1f, 0f));

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only callback that is issued by Unity if a rebuild of the Graphic is required.
        /// Currently sent when an asset is reimported.
        /// </summary>
        public virtual void OnRebuildRequested()
        {
            // when rebuild is requested we need to rebuild all the graphics /
            // and associated components... The correct way to do this is by
            // calling OnValidate... Because MB's don't have a common base class
            // we do this via reflection. It's nasty and ugly... Editor only.
            //当重建被请求时，我们需要重建所有的graphics和相关组件…
            //做这件事的正确方法是调用OnValidate……
            //但因为MonoBehaviour没有公共基类， 所以通过反射来实现。又脏又丑……仅编辑器下。
            var mbs = gameObject.GetComponents<MonoBehaviour>();
            foreach (var mb in mbs)
            {
                if (mb == null)
                    continue;
                var methodInfo = mb.GetType().GetMethod("OnValidate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (methodInfo != null)
                    methodInfo.Invoke(mb, null);
            }
        }

        protected override void Reset()
        {
            SetAllDirty();
        }

#endif

        //重写 UIBehaviour 的方法
        //当动画属性变化时，方法被调用。
        //1、标记为全脏。
        protected override void OnDidApplyAnimationProperties()
        {
            SetAllDirty();
        }


        // Make the Graphic have the native size of its content.
        // 使 Graphic 具有其内容本身的大小。
        // 为子类设定的模板方法
        public virtual void SetNativeSize() {}


        // When a GraphicRaycaster is raycasting into the scene it does two things. First it filters the elements using their RectTransform rect. Then it uses this Raycast function to determine the elements hit by the raycast.
        // 当GraphicRaycaster向场景进行光线投射时，它会做两件事。它使用 RectTransform 的 rect 来过滤元素。使用光线投射函数来确定光线投射的元素。
        // 参数 "sp": Screen point being tested。被射线检测的屏幕坐标
        // 参数 "eventCamera": Camera that is being used for the testing. 射线检测的事件相机
        // 方法的目的：确定本 Graphic 的物体 是否被射线检测到!!!？
        // 1、若未激活/启动，则直接返回false。
        // 2、从当前 Graphic 开始向其父物体递归遍历检测，直到parent为null或被提前打断。
        // 3、若当前物体存在一个“使用独立绘制顺序” 的 Canvas 组件，则使本次执行完后结束递归遍历（提前打断）。（NRatel原因/结论：检测以相同 SortOrder 为基准，对嵌套的 Canvas 分割断层。
        // 4、若当前物体不存在一个“实现了接口 ICanvasRaycastFilter” 的组件，则跳过当前，继续检测其父物体。（NRatel原因/结论：不实现该接口的组件无法被射线检测到。
        // 5、若当前物体存在一个“ignoreParentGroups == true” 的 CanvasGroup 组件，则在递归遍历过程中只检测当前 CanvasGroup 这一层。
        public virtual bool Raycast(Vector2 sp, Camera eventCamera)
        {
            if (!isActiveAndEnabled)
                return false;

            var t = transform;
            var components = ListPool<Component>.Get();

            bool ignoreParentGroups = false;        //是否忽略了父CanvasGroup（这是一个保证仅一次的标志）
            bool continueTraversal = true;          //是否继续遍历，若置为false，本次执行完后结束遍历

            while (t != null)
            {
                t.GetComponents(components);         //取所有组件
                for (var i = 0; i < components.Count; i++)
                {
                    var canvas = components[i] as Canvas;  
                    if (canvas != null && canvas.overrideSorting)
                        continueTraversal = false;  //若当前物体存在一个“使用独立绘制顺序” 的 Canvas 组件，则使本次执行完后结束递归遍历（提前打断）。

                    var filter = components[i] as ICanvasRaycastFilter;
                    if (filter == null)
                        continue;                   //下面的步骤均依赖 ICanvasRaycastFilter。

                    var raycastValid = true;        //射线检测是否有效？

                    var group = components[i] as CanvasGroup;
                    if (group != null)
                    {
                        //if (ignoreParentGroups == false && group.ignoreParentGroups)
                        //{
                        //    ignoreParentGroups = true;
                        //    raycastValid = filter.IsRaycastLocationValid(sp, eventCamera);
                        //}
                        //else if (!ignoreParentGroups)
                        //    raycastValid = filter.IsRaycastLocationValid(sp, eventCamera);

                        //将这块代码等价替换，易于理解（NRatel）
                        //若当前物体存在一个“ignoreParentGroups == true” 的 CanvasGroup 组件，则在递归遍历过程中只检测当前 CanvasGroup 这一层。
                        if (!ignoreParentGroups)
                        {
                            if (group.ignoreParentGroups)
                            {
                                ignoreParentGroups = true;
                            }
                            raycastValid = filter.IsRaycastLocationValid(sp, eventCamera); 
                        }
                    }
                    else
                    {
                        raycastValid = filter.IsRaycastLocationValid(sp, eventCamera);
                    }

                    if (!raycastValid)
                    {
                        ListPool<Component>.Release(components);
                        return false;               //可被检测（有ICanvasRaycastFilter），但未被检测到
                    }
                }
                t = continueTraversal ? t.parent : null;
            }
            ListPool<Component>.Release(components);
            return true;
        }

#if UNITY_EDITOR
        //编辑器下，脚本被加载、或 Inspector 中的任何值被修改时，方法被调用
        //1、设为全脏
        protected override void OnValidate()
        {
            base.OnValidate();
            SetAllDirty();
        }

#endif

        // Adjusts the given pixel to be pixel perfect.
        // 调整给定像素为完美像素。
        // 受 Canvas 的配置 canvas.pixelPerfect 影响。https://docs.unity3d.com/cn/2020.1/ScriptReference/Canvas-pixelPerfect.html
        // 仅在 renderMode 为屏幕空间时有效。
        // 参数"point"：Local space point. 本地空间坐标。
        public Vector2 PixelAdjustPoint(Vector2 point)
        {
            if (!canvas || canvas.renderMode == RenderMode.WorldSpace || canvas.scaleFactor == 0.0f || !canvas.pixelPerfect)
                return point;
            else
            {
                //Unity C#源码中也看不到具体实现
                return RectTransformUtility.PixelAdjustPoint(point, transform, canvas);
            }
        }

        // Returns a pixel perfect Rect closest to the Graphic RectTransform.
        // 返回最接近图形矩形变换的完美像素矩形。
        // 受 Canvas 的配置 canvas.pixelPerfect 影响。https://docs.unity3d.com/cn/2020.1/ScriptReference/Canvas-pixelPerfect.html
        // 仅在 renderMode 为屏幕空间时有效。
        public Rect GetPixelAdjustedRect()
        {
            if (!canvas || canvas.renderMode == RenderMode.WorldSpace || canvas.scaleFactor == 0.0f || !canvas.pixelPerfect)
                return rectTransform.rect;
            else
                //Unity C#源码中也看不到具体实现
                //https://github.com/Unity-Technologies/UnityCsReference/blob/61f92bd79ae862c4465d35270f9d1d57befd1761/Modules/UI/ScriptBindings/RectTransformUtil.bindings.cs
                return RectTransformUtility.PixelAdjustRect(rectTransform, canvas); 
        }

        //重载方法，同其他
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha)
        {
            CrossFadeColor(targetColor, duration, ignoreTimeScale, useAlpha, true);
        }

        // Tweens the CanvasRenderer color associated with this Graphic.
        // 用内置m_ColorTweenRunner执行颜色渐变（最终调用canvasRenderer.SetColor）
        // 参数"targetColor"：Target color. 目标颜色
        // 参数"duration"：Tween duration. 持续时间
        // 参数"ignoreTimeScale"：Should ignore Time.scale? 是否忽略时间缩放
        // 参数"useAlpha"：Should also Tween the alpha channel? 是否变换透明度？
        // 参数"useRGB"：Should the color or the alpha be used to tween? 是否变化RGB？
        // 其实是提供3种变换方式（1、全部；2、仅RGB；3、仅透明度）
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha, bool useRGB)
        {
            if (canvasRenderer == null || (!useRGB && !useAlpha))
                return;

            Color currentColor = canvasRenderer.GetColor();
            if (currentColor.Equals(targetColor))
            {
                m_ColorTweenRunner.StopTween();
                return;
            }

            ColorTween.ColorTweenMode mode = (useRGB && useAlpha ?
                ColorTween.ColorTweenMode.All :
                (useRGB ? ColorTween.ColorTweenMode.RGB : ColorTween.ColorTweenMode.Alpha));

            var colorTween = new ColorTween {duration = duration, startColor = canvasRenderer.GetColor(), targetColor = targetColor};
            colorTween.AddOnChangedCallback(canvasRenderer.SetColor);
            colorTween.ignoreTimeScale = ignoreTimeScale;
            colorTween.tweenMode = mode;
            m_ColorTweenRunner.StartTween(colorTween);
        }

        //创建一个黑色的、带透明度的Color（仅透明度有效）
        static private Color CreateColorFromAlpha(float alpha)
        {
            var alphaColor = Color.black;
            alphaColor.a = alpha;
            return alphaColor;
        }

        //重载方法，同其他
        public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale)
        {
            CrossFadeColor(CreateColorFromAlpha(alpha), duration, ignoreTimeScale, true, false);
        }

        // Add a listener to receive notification when the graphics layout is dirtied.
        // 添加一个listener，用于在图形布局为脏时接收通知。
        public void RegisterDirtyLayoutCallback(UnityAction action)
        {
            m_OnDirtyLayoutCallback += action;
        }

        // Remove a listener from receiving notifications when the graphics layout are dirtied
        // 移除图形布局为脏的 listener。
        public void UnregisterDirtyLayoutCallback(UnityAction action)
        {
            m_OnDirtyLayoutCallback -= action;
        }

        // Add a listener to receive notification when the graphics vertices are dirtied.
        // 添加一个listener，用于在图形顶点为脏时接收通知。
        public void RegisterDirtyVerticesCallback(UnityAction action)
        {
            m_OnDirtyVertsCallback += action;
        }

        // Remove a listener from receiving notifications when the graphics vertices are dirtied
        // 移除图形顶点为脏的 listener。
        public void UnregisterDirtyVerticesCallback(UnityAction action)
        {
            m_OnDirtyVertsCallback -= action;
        }

        // Add a listener to receive notification when the graphics material is dirtied.
        // 添加一个listener，用于在图形材质为脏时接收通知。
        public void RegisterDirtyMaterialCallback(UnityAction action)
        {
            m_OnDirtyMaterialCallback += action;
        }

        // Remove a listener from receiving notifications when the graphics material are dirtied
        // 移除图形材质为脏的 listener。
        public void UnregisterDirtyMaterialCallback(UnityAction action)
        {
            m_OnDirtyMaterialCallback -= action;
        }
    }
}
