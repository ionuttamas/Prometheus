using System.Collections.Generic;

namespace TestProject.Common
{
    public class DeadlockedQueue<T> {
        private readonly LinkedList<T> list;
        private readonly object firstLocker = new object();
        private readonly object secondLocker = new object();

        public DeadlockedQueue() {
            list = new LinkedList<T>();
        }

        public void Enqueue(T item) {
            lock (firstLocker)
            {
                lock (secondLocker)
                {
                    list.AddFirst(item);
                }
            }
        }

        public T Dequeue() {
            T item;

            lock (secondLocker)
            {
                lock (firstLocker)
                {
                    item = list.Last.Value;
                    list.RemoveLast();
                }
            }

            return item;
        }

        public int Count() {
            return list.Count;
        }
    }
}