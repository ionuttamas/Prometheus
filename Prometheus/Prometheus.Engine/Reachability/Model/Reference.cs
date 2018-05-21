using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Prometheus.Engine.ReachabilityProver.Model
{
    public class Reference
    {
        public CallContext CallContext { get; set; }
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
            CallContext = new CallContext();
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

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            Reference instance = (Reference)obj;

            return instance.GetLocation() == GetLocation();
        }

        public override int GetHashCode() {
            return GetLocation().GetHashCode();
        }
    }

    public class CallContext
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

        /// <summary>
        /// The arguments table given the call context.
        /// E.g. for "reference = instance.Get(x, y)" and the method "Get(int a, int b)",
        /// the ArgumentsTable = {(a, x), (b, y)}.
        /// </summary>
        public Dictionary<ParameterSyntax, ArgumentSyntax> ArgumentsTable { get; set; }
        /// <summary>
        /// The invocation expression for the assignment.
        /// E.g. for "reference = instance.Get(x, y)" => "instance.Get(x, y)" will be the invocation expression.
        /// </summary>
        public InvocationExpressionSyntax InvocationExpression { get; set; }
    }
}