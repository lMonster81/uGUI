namespace UnityEngine.EventSystems
{
    /// <summary>
    /// Event Data associated with Axis Events (Controller / Keyboard).
    /// 轴向的输入数据，可以由键盘或控制器触发
    /// </summary>
    public class AxisEventData : BaseEventData
    {
        /// <summary>
        /// Raw input vector associated with this event.
        /// 移动的量
        /// </summary>
        public Vector2 moveVector { get; set; }

        /// <summary>
        /// MoveDirection for this event.
        /// 移动的方向
        /// </summary>
        public MoveDirection moveDir { get; set; }

        public AxisEventData(EventSystem eventSystem)
            : base(eventSystem)
        {
            moveVector = Vector2.zero;
            moveDir = MoveDirection.None;
        }
    }
}
