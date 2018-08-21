using System;
using System.Collections.Generic;

namespace Prometheus.Engine.Types
{
    public class TypeContainer
    {
        /// <summary>
        /// The contract (interface or abstract class) of a given variable.
        /// </summary>
        public Type Contract { get; private set; }

        /// <summary>
        /// The possible implementations available for a given variable.
        /// </summary>
        public List<Type> Implementations { get; }

        /// <summary>
        /// Returns true if a variable is of concrete type (is not declared as a contract).
        /// </summary>
        public bool IsConcreteType => Contract == null;

        private TypeContainer()
        {
            Implementations = new List<Type>();
        }

        public static TypeContainer Empty => new TypeContainer();

        public TypeContainer WithContract(Type contract)
        {
            Contract = contract;
            return this;
        }

        public TypeContainer WithImplementation(Type implementedType) {
            Implementations.Add(implementedType);
            return this;
        }

        public TypeContainer WithImplementations(List<Type> implementations) {
            Implementations.AddRange(implementations);
            return this;
        }
    }
}