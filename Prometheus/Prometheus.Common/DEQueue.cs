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

        public void Append(T item)
        {
            container.Add(item);
        }

        public void Prepend(T item) {
            container.Insert(0, item);
        }

        public void DeleteFirst(T item) {
            container.RemoveAt(0);
        }

        public void DeleteLast(T item) {
            container.RemoveAt(container.Count-1);
        }

        public T PeekFirst() {
            return container.First();
        }

        public T PeekLast() {
            return container.Last();
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