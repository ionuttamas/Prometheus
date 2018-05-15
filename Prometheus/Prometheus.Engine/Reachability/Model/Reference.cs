using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    public class Reference
    {
        /// <summary>
        /// In the case when we are interested in the data of a specific instance.
        /// If the assignment is a simple reference assignment, this property will be null.
        /// Currently, this only supports one level instance method calls, like "instance.Method()" not "instance.Member.Method()".
        /// E.g. in the case below, we bind the customer to the return values of the calling method, but bounded to the specific "customerRepository" instance.
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
        public SyntaxNode InstanceReference { get; set; }
        public SyntaxNode Node { get; set; }
        public SyntaxToken Token { get; set; }

        /// <summary>
        /// In the case of an assignment with a query applied on the reference like:
        ///  - "reference = customers[x]" or
        ///  - "reference = customers.First(x => predicate(x))" or
        ///  - "reference = customers.Where(x => predicate(x))"
        /// this contains the filters applied on the given instance ("customers" reference in this case).
        /// </summary>
        public IReferenceQuery Query { get; set; }

        public Reference() {
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