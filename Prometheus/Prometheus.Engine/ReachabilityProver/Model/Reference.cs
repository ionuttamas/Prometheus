using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    public class Reference
    {
        /// <summary>
        /// In the case when we are interested in the data of a specific instance (or references in the case of call chain).
        /// If the assignment is a simple reference assignment, this property will be null.
        /// E.g. in the case below, we bind the customer to the return values of the calling method, but bounded to the specific "customerRepository" instance.
        /// If the GetCustomer method return values would have come from other assignments, we add those instances to the stack.
        ///
        /// class TransferService
        /// {
        ///     private readonly CustomerRepository customerRepository;
        ///
        ///     public void Transfer(int fromId, int toId, decimal amount)
        ///     {
        ///         Customer customer = customerRepository.GetCustomer(fromId);
        ///
        ///         return instance;
        ///     }
        /// }
        /// </summary>
        public Stack<Reference> CallInstances { get; set; }
        public SyntaxNode Node { get; set; }
        public SyntaxToken Token { get; set; }

        public Reference() {
            CallInstances = new Stack<Reference>();
        }

        public Reference(SyntaxNode node) : this()
        {
            Node = node;
        }

        public Reference(SyntaxToken token) : this() {
            Token = token;
        }

        public Location GetLocation()
        {
            return Node != null ? Node.GetLocation() : Token.GetLocation();
        }

        public override string ToString()
        {
            return Node != null ? Node.ToString() : Token.ToString();
        }
    }
}