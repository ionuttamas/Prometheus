using System.Collections.Generic;

namespace TestProject.Common
{
    public class AtomicQueue<T>
    {
        private readonly LinkedList<T> list;

        public AtomicQueue()
        {
            list = new LinkedList<T>();
        }

        public void Enqueue(T item)
        {
            lock (this) {
                list.AddFirst(item);
            }
        }

        public T Dequeue()
        {
            T item;

            lock (this)
            {
                item = list.Last.Value;
                list.RemoveLast();
            }

            return item;
        }

        public int Count()
        {
            Dequeue();
            return list.Count;
        }
    }
}
