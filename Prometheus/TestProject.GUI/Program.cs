using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestProject.Common;
using TestProject.Services;

namespace TestProject.GUI {
    class Program
    {
        private static AtomicStack<int> atomicStack;
        private static NonAtomicUsedStack<int> nonAtomicUsedStack;
        private static AtomicQueue<int> atomicQueue;
        private static NonAtomicQueue<int> nonAtomicQueue;
        private static DeadlockedQueue<int> deadlockedQueue;

        static void Main(string[] args)
        {
            var queue = new AtomicQueue<Order>();
            var orderService = new OrderService(queue);
            var orderProcessor = new OrderProcessor(queue, 10);
            var thread = new Thread(Do);

            atomicStack.Pop();
            nonAtomicUsedStack.Pop();
            nonAtomicUsedStack.List.AddLast(2);
            atomicQueue.Dequeue();
            nonAtomicQueue.Dequeue();
            deadlockedQueue.Dequeue();
            thread.Start();
            orderProcessor.Start();

            for (int i = 0; i < 100; i++)
            {
                orderService.Add(new Order());
            }
        }

        private static void Do() {
            atomicStack.Push(3);
            nonAtomicUsedStack.Push(4);
            atomicQueue.Enqueue(1);
            nonAtomicQueue.Enqueue(1);
            deadlockedQueue.Enqueue(1);
        }
    }
}
