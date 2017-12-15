using System.Collections.Generic;

namespace TestProject.Common
{
    public class NonAtomicQueue<T>
    {
        private readonly LinkedList<T> list;
        private readonly object firstLocker = new object();
        private readonly object secondLocker = new object();

        public NonAtomicQueue()
        {
            list = new LinkedList<T>();
        }

        public void Enqueue(T item)
        {
            lock (firstLocker)
            {
                list.AddFirst(item);
            }
        }

        public T Dequeue()
        {
            T item;

            lock (secondLocker)
            {
                item = list.Last.Value;
                list.RemoveLast();
            }

            return item;
        }

        public int Count()
        {
            return list.Count;
        }
    }
}