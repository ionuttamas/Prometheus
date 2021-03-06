using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Common
{
    /// <summary>
    /// Double-ended queue implementation.
    /// </summary>
    public class DEQueue<T>
    {
        private readonly List<T> container;

        public DEQueue()
        {
            container = new List<T>();
        }

        public DEQueue(T item) {
            container = new List<T>{item};
        }

        public DEQueue(IEnumerable<T> container) {
            this.container = container.ToList();
        }

        public int Count => container.Count;

        public bool IsEmpty => container.Count == 0;

        public T this[int index] => container[index];

        public void Append(T item)
        {
            container.Add(item);
        }

        public void Prepend(T item) {
            container.Insert(0, item);
        }

        public void DeleteFirst() {
            container.RemoveAt(0);
        }

        public void DeleteLast() {
            container.RemoveAt(container.Count-1);
        }

        public T PeekFirst() {
            return container.FirstOrDefault();
        }

        public T PeekLast() {
            return container.LastOrDefault();
        }

        public void Clear()
        {
            container.Clear();
        }

        public List<T> ToList()
        {
            return container;
        }
    }
}