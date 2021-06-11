using System;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace UnityEngine.UI
{
    /// <summary>
    /// A Graphic that is capable of being masked out.
    /// </summary>
    public abstract class MaskableGraphic : Graphic, IClippable, IMaskable, IMaterialModifier
    {
        [NonSerialized]
        protected bool m_ShouldRecalculateStencil = true;   //是否重新计算模板测试值（脏标记）

        [NonSerialized]
        protected Material m_MaskMaterial;  //遮罩材质

        [NonSerialized]
        private RectMask2D m_ParentMask;    //父 RectMask2D， 由 RectMask2D 在 RecalculateClipping 时为其所有“实现了接口 IClippable”的子物体设置 

        // m_Maskable is whether this graphic is allowed to be masked or not. It has the matching public property maskable.
        // The default for m_Maskable is true, so graphics under a mask are masked out of the box.
        // The maskable property can be turned off from script by the user if masking is not desired.
        // m_Maskable 表示这个图形是否允许被 Mask。有与之对应的 public属性 maskable。
        // m_Maskable 的默认值是true，所以 Mask 下的图形是默认生效的。
        // 如果不想被 Mask，可以用脚本关闭 maskable 属性。
        [NonSerialized]
        private bool m_Maskable = true;

        // m_IncludeForMasking 已废弃。
        [NonSerialized]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Not used anymore.", true)]
        protected bool m_IncludeForMasking = false;

        [Serializable]
        public class CullStateChangedEvent : UnityEvent<bool> {}
        
        [SerializeField]
        private CullStateChangedEvent m_OnCullStateChanged = new CullStateChangedEvent();

        // Callback issued when culling changes.
        // Called when the culling state of this MaskableGraphic either becomes culled or visible. You can use this to control other elements of your UI as culling happens.
        // 剔除改变时的回调。
        // 当 MaskableGraphic 的剔除状态变成 被剔除（culled）或 可见时（visible）调用。
        // 当 剔除发生时，你可以使用它来控制UI的其他元素。
        public CullStateChangedEvent onCullStateChanged
        {
            get { return m_OnCullStateChanged; }
            set { m_OnCullStateChanged = value; }
        }

        // Does this graphic allow masking.
        // 这个图形是否允许被 Mask
        public bool maskable
        {
            get { return m_Maskable; }
            set
            {
                if (value == m_Maskable)
                    return;
                m_Maskable = value;
                m_ShouldRecalculateStencil = true;
                SetMaterialDirty();
            }
        }

        [NonSerialized]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Not used anymore", true)]
        protected bool m_ShouldRecalculate = true;

        [NonSerialized]
        protected int m_StencilValue;   //模板测试值

        /// <summary>
        /// See IMaterialModifier.GetModifiedMaterial
        /// </summary>
        public virtual Material GetModifiedMaterial(Material baseMaterial)
        {
            var toUse = baseMaterial;

            if (m_ShouldRecalculateStencil)
            {
                var rootCanvas = MaskUtilities.FindRootSortOverrideCanvas(transform);
                m_StencilValue = maskable ? MaskUtilities.GetStencilDepth(transform, rootCanvas) : 0;
                m_ShouldRecalculateStencil = false;
            }

            // if we have a enabled Mask component then it will
            // generate the mask material. This is an optimisation
            // it adds some coupling between components though :(
            Mask maskComponent = GetComponent<Mask>();
            if (m_StencilValue > 0 && (maskComponent == null || !maskComponent.IsActive()))
            {
                var maskMat = StencilMaterial.Add(toUse, (1 << m_StencilValue) - 1, StencilOp.Keep, CompareFunction.Equal, ColorWriteMask.All, (1 << m_StencilValue) - 1, 0);
                StencilMaterial.Remove(m_MaskMaterial);
                m_MaskMaterial = maskMat;
                toUse = m_MaskMaterial;
            }
            return toUse;
        }

        /// <summary>
        /// See IClippable.Cull
        /// </summary>
        public virtual void Cull(Rect clipRect, bool validRect)
        {
            var cull = !validRect || !clipRect.Overlaps(rootCanvasRect, true);
            UpdateCull(cull);
        }

        private void UpdateCull(bool cull)
        {
            if (canvasRenderer.cull != cull)
            {
                canvasRenderer.cull = cull;
                UISystemProfilerApi.AddMarker("MaskableGraphic.cullingChanged", this);
                m_OnCullStateChanged.Invoke(cull);
                OnCullingChanged();
            }
        }

        /// <summary>
        /// See IClippable.SetClipRect
        /// </summary>
        public virtual void SetClipRect(Rect clipRect, bool validRect)
        {
            if (validRect)
                canvasRenderer.EnableRectClipping(clipRect);
            else
                canvasRenderer.DisableRectClipping();
        }

        //1、执行父类 OnEnable
        //2、m_ShouldRecalculateStencil 设为true（需要计算模板值）
        protected override void OnEnable()
        {
            base.OnEnable();
            m_ShouldRecalculateStencil = true;
            UpdateClipParent();
            SetMaterialDirty();

            if (GetComponent<Mask>() != null)
            {
                MaskUtilities.NotifyStencilStateChanged(this);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            m_ShouldRecalculateStencil = true;
            SetMaterialDirty();
            UpdateClipParent();
            StencilMaterial.Remove(m_MaskMaterial);
            m_MaskMaterial = null;

            if (GetComponent<Mask>() != null)
            {
                MaskUtilities.NotifyStencilStateChanged(this);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_ShouldRecalculateStencil = true;
            UpdateClipParent();
            SetMaterialDirty();
        }

#endif

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();

            if (!isActiveAndEnabled)
                return;

            m_ShouldRecalculateStencil = true;
            UpdateClipParent();
            SetMaterialDirty();
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Not used anymore.", true)]
        public virtual void ParentMaskStateChanged() {}

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();

            if (!isActiveAndEnabled)
                return;

            m_ShouldRecalculateStencil = true;
            UpdateClipParent();
            SetMaterialDirty();
        }

        readonly Vector3[] m_Corners = new Vector3[4];
        private Rect rootCanvasRect
        {
            get
            {
                rectTransform.GetWorldCorners(m_Corners);

                if (canvas)
                {
                    Matrix4x4 mat = canvas.rootCanvas.transform.worldToLocalMatrix;
                    for (int i = 0; i < 4; ++i)
                        m_Corners[i] = mat.MultiplyPoint(m_Corners[i]);
                }

                // bounding box is now based on the min and max of all corners (case 1013182)

                Vector2 min = m_Corners[0];
                Vector2 max = m_Corners[0];
                for (int i = 1; i < 4; i++)
                {
                    min.x = Mathf.Min(m_Corners[i].x, min.x);
                    min.y = Mathf.Min(m_Corners[i].y, min.y);
                    max.x = Mathf.Max(m_Corners[i].x, max.x);
                    max.y = Mathf.Max(m_Corners[i].y, max.y);
                }

                return new Rect(min, max - min);
            }
        }

        // 更新 m_ParentMask
        private void UpdateClipParent()
        {
            // 仅当需要被 Mask 且 Active 时，取当前可令自身 IClippable 生效的父 RectMask2D 为 m_ParentMask。 否则为null。
            var newParent = (maskable && IsActive()) ? MaskUtilities.GetRectMaskForClippable(this) : null;

            // if the new parent is different OR is now inactive
            if (m_ParentMask != null && (newParent != m_ParentMask || !newParent.IsActive()))
            {
                m_ParentMask.RemoveClippable(this);
                UpdateCull(false);
            }

            // don't re-add it if the newparent is inactive
            if (newParent != null && newParent.IsActive())
                newParent.AddClippable(this);

            m_ParentMask = newParent;
        }

        /// <summary>
        /// See IClippable.RecalculateClipping
        /// </summary>
        public virtual void RecalculateClipping()
        {
            UpdateClipParent();
        }

        /// <summary>
        /// See IMaskable.RecalculateMasking
        /// </summary>
        public virtual void RecalculateMasking()
        {
            // Remove the material reference as either the graphic of the mask has been enable/ disabled.
            // This will cause the material to be repopulated from the original if need be. (case 994413)
            StencilMaterial.Remove(m_MaskMaterial);
            m_MaskMaterial = null;
            m_ShouldRecalculateStencil = true;
            SetMaterialDirty();
        }
    }
}
