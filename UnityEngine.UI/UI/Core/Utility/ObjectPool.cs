using System.Collections.Generic;
using UnityEngine.Events;

namespace UnityEngine.UI
{
    internal class ObjectPool<T> where T : new()
    {
        private readonly Stack<T> m_Stack = new Stack<T>();
        private readonly UnityAction<T> m_ActionOnGet;
        private readonly UnityAction<T> m_ActionOnRelease;

        public int countAll { get; private set; }           //创建出的元素总数
        public int countActive { get { return countAll - countInactive; } } //池外元素数量
        public int countInactive { get { return m_Stack.Count; } }  //池中元素数量

        public ObjectPool(UnityAction<T> actionOnGet, UnityAction<T> actionOnRelease)
        {
            m_ActionOnGet = actionOnGet;
            m_ActionOnRelease = actionOnRelease;
        }

        //从池中取出元素
        public T Get()
        {
            T element;
            if (m_Stack.Count == 0)
            {
                element = new T();          //创建元素
                countAll++;                 //创建出的元素总数+1
            }
            else
            {
                element = m_Stack.Pop();    //从池中取出
            }
            if (m_ActionOnGet != null)
                m_ActionOnGet(element);     //执行取出回调
            return element;
        }

        //元素放回池中
        public void Release(T element)
        {
            if (m_Stack.Count > 0 && ReferenceEquals(m_Stack.Peek(), element))
                Debug.LogError("Internal error. Trying to destroy object that is already released to pool.");   //尝试放回的元素已在池中
            if (m_ActionOnRelease != null)
                m_ActionOnRelease(element); //执行放回回调
            m_Stack.Push(element);          //放回池中
        }
    }
}
