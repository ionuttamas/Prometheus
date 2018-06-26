using System;
using System.Collections.Generic;
using System.Linq;
using Prometheus.Engine.Verifier;

namespace Prometheus.Engine.Model
{
    public class DotNetModelStateConfigurator : IModelStateConfigurator
    {
        public ModelStateConfiguration GetConfiguration()
        {
            var modelConfiguration = ModelStateConfiguration.Empty;

            BootstrapList(modelConfiguration);
            BootstrapLinkedList(modelConfiguration);
            BootstrapSortedList(modelConfiguration);
            BootstrapSortedSet(modelConfiguration);
            BootstrapStack(modelConfiguration);
            BootstrapQueue(modelConfiguration);
            BootstrapHashSet(modelConfiguration);
            BootstrapDictionary(modelConfiguration);

            return modelConfiguration;
        }

        private void BootstrapList(ModelStateConfiguration modelConfiguration)
        {
            modelConfiguration
                .ChangesState<List<object>>(x => x.RemoveAt(Args.Any<int>()))
                .ChangesState<List<object>>(x => x.Remove(Args.Any<object>()))
                .ChangesState<List<object>>(x => x.RemoveAll(Args.Any<Predicate<object>>()))
                .ChangesState<List<object>>(x => x.RemoveRange(Args.Any<int>(), Args.Any<int>()))
                .ChangesState<List<object>>(x => x.Clear())
                .ChangesState<List<object>>(x => x.Insert(Args.Any<int>(), Args.Any<object>()))
                .ChangesState<List<object>>(x => x.Reverse())
                .ChangesState<List<object>>(x => x.InsertRange(Args.Any<int>(), Args.Any<IEnumerable<object>>()))
                .ChangesState<List<object>>(x => x.Sort())
                .ChangesState<List<object>>(x => x.AddRange(Args.Any<IEnumerable<object>>()));

            modelConfiguration
                .MutuallyExclusive<List<object>>(x => x.Contains(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<List<object>>(x => x.Contains(Args.Any<object>()), x => x.Any());
        }

        private void BootstrapLinkedList(ModelStateConfiguration modelConfiguration) {
            modelConfiguration
                .ChangesState<LinkedList<object>>(x => x.AddAfter(Args.Any<LinkedListNode<object>>(), Args.Any<object>()))
                .ChangesState<LinkedList<object>>(x => x.AddBefore(Args.Any<LinkedListNode<object>>(), Args.Any<object>()))
                .ChangesState<LinkedList<object>>(x => x.AddFirst(Args.Any<LinkedListNode<object>>()))
                .ChangesState<LinkedList<object>>(x => x.AddLast(Args.Any<LinkedListNode<object>>()))
                .ChangesState<LinkedList<object>>(x => x.Remove(Args.Any<object>()))
                .ChangesState<LinkedList<object>>(x => x.RemoveFirst())
                .ChangesState<LinkedList<object>>(x => x.RemoveLast())
                .ChangesState<LinkedList<object>>(x => x.Clear());

            modelConfiguration
                .MutuallyExclusive<LinkedList<object>>(x => x.Contains(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<LinkedList<object>>(x => x.Contains(Args.Any<object>()), x => x.Any());
        }

        private void BootstrapStack(ModelStateConfiguration modelConfiguration)
        {
            modelConfiguration
                .ChangesState<Stack<object>>(x => x.Pop())
                .ChangesState<Stack<object>>(x => x.Push(Args.Any<object>()))
                .ChangesState<Stack<object>>(x => x.Clear());

            modelConfiguration
                .MutuallyExclusive<Stack<object>>(x => x.Contains(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<Stack<object>>(x => x.Contains(Args.Any<object>()), x => x.Any());
        }

        private void BootstrapQueue(ModelStateConfiguration modelConfiguration) {
            modelConfiguration
                .ChangesState<Queue<object>>(x => x.Dequeue())
                .ChangesState<Queue<object>>(x => x.Enqueue(Args.Any<object>()))
                .ChangesState<Queue<object>>(x => x.Clear());

            modelConfiguration
                .MutuallyExclusive<Queue<object>>(x => x.Contains(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<Queue<object>>(x => x.Contains(Args.Any<object>()), x => x.Any());
        }

        private void BootstrapHashSet(ModelStateConfiguration modelConfiguration)
        {
            modelConfiguration
                .ChangesState<HashSet<object>>(x => x.ExceptWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<HashSet<object>>(x => x.IntersectWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<HashSet<object>>(x => x.SymmetricExceptWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<HashSet<object>>(x => x.UnionWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<HashSet<object>>(x => x.Add(Args.Any<object>()))
                .ChangesState<HashSet<object>>(x => x.RemoveWhere(Args.Any<Predicate<object>>()))
                .ChangesState<HashSet<object>>(x => x.Remove(Args.Any<object>()))
                .ChangesState<HashSet<object>>(x => x.Clear());

            modelConfiguration
                .MutuallyExclusive<HashSet<object>>(x => x.Contains(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<HashSet<object>>(x => x.Contains(Args.Any<object>()), x => x.Any());
        }

        private void BootstrapSortedSet(ModelStateConfiguration modelConfiguration) {
            modelConfiguration
                .ChangesState<SortedSet<object>>(x => x.ExceptWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<SortedSet<object>>(x => x.IntersectWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<SortedSet<object>>(x => x.SymmetricExceptWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<SortedSet<object>>(x => x.UnionWith(Args.Any<IEnumerable<object>>()))
                .ChangesState<SortedSet<object>>(x => x.Add(Args.Any<object>()))
                .ChangesState<SortedSet<object>>(x => x.RemoveWhere(Args.Any<Predicate<object>>()))
                .ChangesState<SortedSet<object>>(x => x.Remove(Args.Any<object>()))
                .ChangesState<SortedSet<object>>(x => x.Clear());

            modelConfiguration
                .MutuallyExclusive<SortedSet<object>>(x => x.Contains(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<SortedSet<object>>(x => x.Contains(Args.Any<object>()), x => x.Any());
        }

        private void BootstrapDictionary(ModelStateConfiguration modelConfiguration) {
            modelConfiguration
                .ChangesState<Dictionary<object, object>>(x => x.Add(Args.Any<object>(), Args.Any<object>()))
                .ChangesState<Dictionary<object, object>>(x => x.Remove(Args.Any<object>()))
                .ChangesState<Dictionary<object, object>>(x => x.Clear());

            modelConfiguration
                .MutuallyExclusive<Dictionary<object, object>>(x => x.ContainsKey(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<Dictionary<object, object>>(x => x.ContainsValue(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<Dictionary<object, object>>(x => x.ContainsKey(Args.Any<object>()), x => x.Any())
                .MutuallyExclusive<Dictionary<object, object>>(x => x.ContainsValue(Args.Any<object>()), x => x.Any());
        }

        private void BootstrapSortedList(ModelStateConfiguration modelConfiguration) {
            modelConfiguration
                .ChangesState<SortedList<object, object>>(x => x.Add(Args.Any<object>(), Args.Any<object>()))
                .ChangesState<SortedList<object, object>>(x => x.Remove(Args.Any<object>()))
                .ChangesState<SortedList<object, object>>(x => x.RemoveAt(Args.Any<int>()))
                .ChangesState<SortedList<object, object>>(x => x.Clear());

            modelConfiguration
                .MutuallyExclusive<SortedList<object, object>>(x => x.ContainsKey(Args.Any<object>()), x => x.IndexOfKey(Args.Any<object>()) > 0)
                .MutuallyExclusive<SortedList<object, object>>(x => x.ContainsValue(Args.Any<object>()), x => x.IndexOfValue(Args.Any<object>()) > 0)
                .MutuallyExclusive<SortedList<object, object>>(x => x.ContainsKey(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<SortedList<object, object>>(x => x.ContainsValue(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<SortedList<object, object>>(x => x.ContainsValue(Args.Any<object>()), x => x.Count > 0)
                .MutuallyExclusive<SortedList<object, object>>(x => x.ContainsKey(Args.Any<object>()), x => x.Any())
                .MutuallyExclusive<SortedList<object, object>>(x => x.ContainsValue(Args.Any<object>()), x => x.Any());
        }
    }
}