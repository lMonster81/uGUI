namespace UnityEngine.UI
{
    internal class RectangularVertexClipper
    {
        readonly Vector3[] m_WorldCorners = new Vector3[4];
        readonly Vector3[] m_CanvasCorners = new Vector3[4];

        //获取 t在Canvas下的 rect (包括位置和大小)
        public Rect GetCanvasRect(RectTransform t, Canvas c)
        {
            if (c == null)
                return new Rect();

            t.GetWorldCorners(m_WorldCorners);  //取t的世界坐标
            var canvasTransform = c.GetComponent<Transform>();
            for (int i = 0; i < 4; ++i)
                m_CanvasCorners[i] = canvasTransform.InverseTransformPoint(m_WorldCorners[i]);  //将世界坐标转为相对Canvas的local坐标

            //返回t在Canvas下的 rect (包括位置和大小)
            return new Rect(m_CanvasCorners[0].x, m_CanvasCorners[0].y, m_CanvasCorners[2].x - m_CanvasCorners[0].x, m_CanvasCorners[2].y - m_CanvasCorners[0].y);
        }
    }
}
