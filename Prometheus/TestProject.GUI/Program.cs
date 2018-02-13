using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestProject.Common;
using TestProject.Services;

namespace TestProject.GUI {
    public class Program
    {
        private static AtomicStack<int> atomicStack;
        private static NonAtomicUsedStack<int> nonAtomicUsedStack;
        private static AtomicQueue<int> atomicQueue;
        private static NonAtomicQueue<int> nonAtomicQueue;
        private static DeadlockedQueue<int> deadlockedQueue;
        private static Customer sharedCustomer;

        static void Main(string[] args)
        {
            var queue = new AtomicQueue<Order>();
            var orderService = new OrderService(queue);
            var orderProcessor = new OrderProcessor(queue, 10);
            var thread = new Thread(Do);
            var transferService = new TransferService();

            transferService.Transfer(sharedCustomer, null, 200);
            transferService.SimpleIfTransfer(sharedCustomer, null, 200);
            transferService.SimpleIfSingleElseTransfer(sharedCustomer, null, 200);
            transferService.SimpleIfMultipleElseTransfer(sharedCustomer, null, 200);
            transferService.NestedIfElseTransfer(sharedCustomer, null, 200);
            transferService.NestedIfElse_With_IfElseTransfer(sharedCustomer, null, 200);
            transferService.NestedCall_SimpleIf_SimpleIfTransfer(sharedCustomer, null, 200);

            var transferService2 = new TransferService2();
            transferService2.SimpleIfTransfer(sharedCustomer, null, 100);
            transferService2.SimpleIfTransfer3(sharedCustomer, null, 100);

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
            var registrationService = new RegistrationService();
            registrationService.Register(sharedCustomer);
            registrationService.SimpleIfRegister(sharedCustomer);

            var transferService2 = new TransferService2();
            transferService2.SimpleIfTransfer2(sharedCustomer, null, 100);
            transferService2.SimpleIfTransfer3(sharedCustomer, null, 100);
        }
    }
}
