using System.Collections.Generic;

namespace TestProject.Common
{
    public class AtomicStack<T>
    {
        public LinkedList<T> List { get; set; }

        public AtomicStack() {
            List = new LinkedList<T>();
        }

        public void Push(T item) {
            lock (this) {
                List.AddLast(item);
            }
        }

        public T Pop() {
            T item;

            lock (this) {
                item = List.Last.Value;
                List.RemoveLast();
            }

            return item;
        }

        public int Count() {
            return List.Count;
        }
    }
}