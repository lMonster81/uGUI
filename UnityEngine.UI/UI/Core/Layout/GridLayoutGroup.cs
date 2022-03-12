using UnityEngine;
using System.Collections.Generic;

namespace UnityEngine.UI
{
    [AddComponentMenu("Layout/Grid Layout Group", 152)]

    // The GridLayoutGroup component is used to layout child layout elements in a uniform grid where all cells have the same size. The size and the spacing between cells is controlled by the GridLayoutGroup itself. The children have no influence on their sizes.
    // GridLayoutGroup 组件用于在所有单元格具有相同大小的统一网格中布局子布局元素。
    // 单元格之间的大小和间距由GridLayoutGroup本身控制。不由它们自身决定。
    public class GridLayoutGroup : LayoutGroup
    {
        // Which corner is the starting corner for the grid.
        // 从哪个角开始
        public enum Corner
        {
            UpperLeft = 0,  //左上
            UpperRight = 1, //右上
            LowerLeft = 2,  //左下
            LowerRight = 3  //右下
        }

        // The grid axis we are looking at.
        // As the storage is a [][] we make access easier by passing a axis.
        // 沿着此轴排布。
        // 由于存储是 [][]，我们通过一个轴使访问更容易。  
        public enum Axis
        {
            Horizontal = 0, //水平
            Vertical = 1 //竖直
        }

        // Constraint type on either the number of columns or rows.
        // 列数或行数上的约束类型。 
        public enum Constraint
        {
            // Don't constrain the number of rows or columns.
            // 不限定行或列数。（灵活自适应）
            Flexible = 0,

            // Constrain the number of columns to a specified number.
            // 限定列数。
            FixedColumnCount = 1,

            // Constraint the number of rows to a specified number.
            // 限定行数。
            FixedRowCount = 2
        }

        [SerializeField] protected Corner m_StartCorner = Corner.UpperLeft;

        // Which corner should the first cell be placed in?
        // 第一个单元格应该放在哪个角落  
        public Corner startCorner { get { return m_StartCorner; } set { SetProperty(ref m_StartCorner, value); } }

        [SerializeField] protected Axis m_StartAxis = Axis.Horizontal;

        // Which axis should cells be placed along first
        // When startAxis is set to horizontal, an entire row will be filled out before proceeding to the next row. When set to vertical, an entire column will be filled out before proceeding to the next column.
        // 单元格应该先沿哪条轴排列
        // 当startAxis设置为水平时，将在继续下一行之前填充一整行。
        // 当设置为垂直时，将在继续下一列之前填写整个列。  
        public Axis startAxis { get { return m_StartAxis; } set { SetProperty(ref m_StartAxis, value); } }

        [SerializeField] protected Vector2 m_CellSize = new Vector2(100, 100);

        // The size to use for each cell in the grid.
        // 单元格大小。
        public Vector2 cellSize { get { return m_CellSize; } set { SetProperty(ref m_CellSize, value); } }

        [SerializeField] protected Vector2 m_Spacing = Vector2.zero;

        // The spacing to use between layout elements in the grid on both axises.
        // 布局元素之间的间距。  
        public Vector2 spacing { get { return m_Spacing; } set { SetProperty(ref m_Spacing, value); } }

        [SerializeField] protected Constraint m_Constraint = Constraint.Flexible;

        // Which constraint to use for the GridLayoutGroup.
        // Specifying a constraint can make the GridLayoutGroup work better in conjunction with a [[ContentSizeFitter]] component. When GridLayoutGroup is used on a RectTransform with a manually specified size, there's no need to specify a constraint.
        // GridLayoutGroup使用哪个约束。
        // 指定一个约束可以使GridLayoutGroup与[[ContentSizeFitter]]组件一起更好地工作。
        // 当GridLayoutGroup在RectTransform上使用手动指定的大小时，不需要指定约束。  
        public Constraint constraint { get { return m_Constraint; } set { SetProperty(ref m_Constraint, value); } }

        [SerializeField] protected int m_ConstraintCount = 2;

        // How many cells there should be along the constrained axis.
        // 约束轴上应该有多少个单元格。
        public int constraintCount { get { return m_ConstraintCount; } set { SetProperty(ref m_ConstraintCount, Mathf.Max(1, value)); } }

        protected GridLayoutGroup()
        {}

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            constraintCount = constraintCount;
        }

#endif

        // Also see ILayoutElement
        // Called by the layout system to calculate the horizontal layout size.
        // 实现 ILayoutElement 的方法
        // 由布局系统调用来计算水平布局大小。  
        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal(); //统计有效子元素

            int minColumns = 0;   //最小列数
            int preferredColumns = 0; //偏好列数
            if (m_Constraint == Constraint.FixedColumnCount)
            {
                //指定列时：最小列数 = 偏好列数 = 指定列数
                minColumns = preferredColumns = m_ConstraintCount;  
            }
            else if (m_Constraint == Constraint.FixedRowCount)
            {
                //指定行时：最小列数 = 偏好列数 = (元素总数/指定行数)向上取整
                minColumns = preferredColumns = Mathf.CeilToInt(rectChildren.Count / (float)m_ConstraintCount - 0.001f); 
            }
            else
            {
                //自适应时，最小列数 = 1； 偏好列数 = (元素总数取平方根)向上取整（即：尽量正方）
                minColumns = 1;
                preferredColumns = Mathf.CeilToInt(Mathf.Sqrt(rectChildren.Count));
            }

            SetLayoutInputForAxis(
                padding.horizontal + (cellSize.x + spacing.x) * minColumns - spacing.x,         //totalMin, 
                padding.horizontal + (cellSize.x + spacing.x) * preferredColumns - spacing.x,   //totalPreferred,
                -1, //totalFlexible
                0); //水平方向
        }

        // Also see ILayoutElement
        // Called by the layout system to calculate the vertical layout size.
        // 实现 ILayoutElement 的方法
        // 由布局系统调用来计算竖直布局大小。  
        public override void CalculateLayoutInputVertical()
        {
            int minRows = 0;   //最小行数
            if (m_Constraint == Constraint.FixedColumnCount)
            {
                //指定列时：最小行数 = (元素总数/指定列数)并向上取整
                minRows = Mathf.CeilToInt(rectChildren.Count / (float)m_ConstraintCount - 0.001f);
            }
            else if (m_Constraint == Constraint.FixedRowCount)
            {
                //指定行时：最小行数 = 指定行数
                minRows = m_ConstraintCount;
            }
            else
            {
                //自适应时：
                //水平方向格子数 = 1~水平方向最多能放置的格子数
                //最下行数 = (总元素数/水平方向格子数)向上取整
                float width = rectTransform.rect.width;
                int cellCountX = Mathf.Max(1, Mathf.FloorToInt((width - padding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x)));
                minRows = Mathf.CeilToInt(rectChildren.Count / (float)cellCountX);
            }

            float minSpace = padding.vertical + (cellSize.y + spacing.y) * minRows - spacing.y;  //按照最小行得到的最小y轴高度
            SetLayoutInputForAxis(minSpace, minSpace, -1, 1);
        }

        // Also see ILayoutElement
        // Called by the layout system
        // 实现 ILayoutElement 的方法
        // 由布局系统自动调用
        public override void SetLayoutHorizontal()
        {
            SetCellsAlongAxis(0);
        }

        // Also see ILayoutElement
        // Called by the layout system
        // 实现 ILayoutElement 的方法
        // 由布局系统自动调用
        public override void SetLayoutVertical()
        {
            SetCellsAlongAxis(1);
        }

        private void SetCellsAlongAxis(int axis)
        {
            // Normally a Layout Controller should only set horizontal values when invoked for the horizontal axis and only vertical values when invoked for the vertical axis.
            // However, in this case we set both the horizontal and vertical position when invoked for the vertical axis.
            // Since we only set the horizontal position and not the size, it shouldn't affect children's layout, and thus shouldn't break the rule that all horizontal layout must be calculated before all vertical layout.
            // 通常布局控制器在调用水平轴时只能设置水平值，在调用竖直轴时只能设置竖直值。  
            // 然而，在本例中，当调用竖直轴时，我们同时设置了水平和竖直位置。  
            // 因为我们只设置了水平位置而没有设置大小，所以它不影响子元素的布局，也未打破“所有水平布局必须在所有竖直布局之前计算”的规则。

            if (axis == 0)
            {
                // Only set the sizes when invoked for horizontal axis, not the positions.
                // 当调用水平轴时，只设置大小，不设置位置。  
                for (int i = 0; i < rectChildren.Count; i++)
                {
                    RectTransform rect = rectChildren[i];

                    // 控制子物体锚点、位置、大小
                    m_Tracker.Add(this, rect,
                        DrivenTransformProperties.Anchors |
                        DrivenTransformProperties.AnchoredPosition |
                        DrivenTransformProperties.SizeDelta);

                    // 强制设置锚点为左上
                    rect.anchorMin = Vector2.up;
                    rect.anchorMax = Vector2.up;

                    // 强制设置大小为指定大小
                    rect.sizeDelta = cellSize;
                }
                return;
            }

//一、按水平向，计算行列数
            float width = rectTransform.rect.size.x;
            float height = rectTransform.rect.size.y;

            int cellCountX = 1;  //默认最小1
            int cellCountY = 1;  //默认最小1
            if (m_Constraint == Constraint.FixedColumnCount)
            {
                //指定列时：
                //列数 = 指定列数
                cellCountX = m_ConstraintCount;
                if (rectChildren.Count > cellCountX)   //多于一列时
                    //行数 = 整除（总数/列数） 有余数+1，没余数则不+
                    cellCountY = rectChildren.Count / cellCountX + (rectChildren.Count % cellCountX > 0 ? 1 : 0);
            }
            else if (m_Constraint == Constraint.FixedRowCount)
            {
                //指定行时：
                //行数 = 指定行数
                cellCountY = m_ConstraintCount;
                if (rectChildren.Count > cellCountY)   //多于一行时
                    //列数 = 整除（总数/行数） 有余数+1，没余数则不+
                    cellCountX = rectChildren.Count / cellCountY + (rectChildren.Count % cellCountY > 0 ? 1 : 0);
            }
            else
            {
                // 自适应时：
                if (cellSize.x + spacing.x <= 0)
                    cellCountX = int.MaxValue;   //处理参数不合法的情况
                else
                    //列数 = 能放下的最大列数
                    cellCountX = Mathf.Max(1, Mathf.FloorToInt((width - padding.horizontal + spacing.x + 0.001f) / (cellSize.x + spacing.x)));

                if (cellSize.y + spacing.y <= 0)
                    cellCountY = int.MaxValue;   //处理参数不合法的情况
                else
                    //行数 = 能放下的最大行数
                    cellCountY = Mathf.Max(1, Mathf.FloorToInt((height - padding.vertical + spacing.y + 0.001f) / (cellSize.y + spacing.y)));
            }


            //UpperLeft = 0,  //左上
            //UpperRight = 1, //右上
            //LowerLeft = 2,  //左下
            //LowerRight = 3  //右下
            int cornerX = (int)startCorner % 2;  //0：左， 1右
            int cornerY = (int)startCorner / 2;  //0：上， 1下

//二、沿自定的轴转置，确定真实行列数
            int cellsPerMainAxis;  //延伸轴上的格子数
            int actualCellCountX;  //水平方向实际格子数（实际列数）
            int actualCellCountY;  //竖直方向实际格子数（实际行数）

            if (startAxis == Axis.Horizontal)
            {
                cellsPerMainAxis = cellCountX;
                actualCellCountX = Mathf.Clamp(cellCountX, 1, rectChildren.Count);  //注意，这里Mathf.Clamp是因为上面自适应中非法时，将行列数设为了Int最大值。
                actualCellCountY = Mathf.Clamp(cellCountY, 1, Mathf.CeilToInt(rectChildren.Count / (float)cellsPerMainAxis));
            }
            else
            {
                cellsPerMainAxis = cellCountY;
                actualCellCountY = Mathf.Clamp(cellCountY, 1, rectChildren.Count);
                actualCellCountX = Mathf.Clamp(cellCountX, 1, Mathf.CeilToInt(rectChildren.Count / (float)cellsPerMainAxis));
            }

//三、计算实际需要的空间大小（不含padding） 及 在这个空间上第一个元素所在的位置
            Vector2 requiredSpace = new Vector2(
                actualCellCountX * cellSize.x + (actualCellCountX - 1) * spacing.x,
                actualCellCountY * cellSize.y + (actualCellCountY - 1) * spacing.y
            );
            Vector2 startOffset = new Vector2(
                GetStartOffset(0, requiredSpace.x),
                GetStartOffset(1, requiredSpace.y)
            );

//四、根据起始角依次确定所有子元素位置索引、设置具体位置坐标
            for (int i = 0; i < rectChildren.Count; i++)
            {
                int positionX;   //X位置索引（注意不是坐标）
                int positionY;   //Y位置索引（注意不是坐标）
                if (startAxis == Axis.Horizontal)
                {
                    positionX = i % cellsPerMainAxis;
                    positionY = i / cellsPerMainAxis;
                }
                else
                {
                    positionX = i / cellsPerMainAxis;
                    positionY = i % cellsPerMainAxis;
                }

                //根据起始角进行转置
                if (cornerX == 1)  //如果是从右往左
                    positionX = actualCellCountX - 1 - positionX;
                if (cornerY == 1) //如果是从下往上
                    positionY = actualCellCountY - 1 - positionY;

                //设置具体坐标
                SetChildAlongAxis(rectChildren[i], 0, startOffset.x + (cellSize[0] + spacing[0]) * positionX, cellSize[0]);
                SetChildAlongAxis(rectChildren[i], 1, startOffset.y + (cellSize[1] + spacing[1]) * positionY, cellSize[1]);
            }
        }
    }
}
