using System;
using System.Threading;
using TestProject.Common;

namespace TestProject.Services
{
    public class OrderProcessor
    {
        private readonly AtomicQueue<Order> orderQueue;
        private readonly int processingTimeout;

        public OrderProcessor(AtomicQueue<Order> orderQueue, int processingTimeout) {
            this.orderQueue = orderQueue;
            this.processingTimeout = processingTimeout;
        }

        public void Start()
        {
            Thread processThread = new Thread(ProcessOrders);
            processThread.Start();
        }

        public void Foo()
        {
            Start();
        }

        private void ProcessOrders()
        {
            while (true)
            {
                if (orderQueue.Count() > 0)
                {
                    while (orderQueue.Count() > 0)
                    {
                        var order = orderQueue.Dequeue();
                        Console.WriteLine("Processing order: {0} -> {1}", order.Customer, order.Product);
                    }
                }
                else
                {
                    Thread.Sleep(processingTimeout);
                }
            }
        }
    }
}