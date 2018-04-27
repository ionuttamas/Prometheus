using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using TypeInfo = System.Reflection.TypeInfo;

namespace Prometheus.Engine.Types
{
    internal class TypeService : ITypeService
    {
        private readonly List<TypeInfo> solutionTypes;
        private readonly Dictionary<string, Type> coreTypes;

        public TypeService(Solution solution)
        {
            //todo: needs to get projects referenced assemblies
            solutionTypes = solution.Projects.Select(x => Assembly.Load(x.AssemblyName)).SelectMany(x => x.DefinedTypes)
                .ToList();
            solutionTypes.AddRange(Assembly.GetAssembly(typeof(int)).DefinedTypes);
            coreTypes = new Dictionary<string, Type>
            {
                {"byte", typeof(byte)},
                {"sbyte",typeof(sbyte)},
                {"short", typeof(short)},
                {"ushort", typeof(ushort)},
                {"int", typeof(int)},
                {"uint", typeof(uint)},
                {"long", typeof(long)},
                {"ulong", typeof(ulong)},
                {"float", typeof(float)},
                {"double", typeof(double)},
                {"decimal", typeof(decimal)},
                {"object", typeof(object)},
                {"bool", typeof(bool)},
                {"char", typeof(char)},
                {"byte?", typeof(byte?)},
                {"sbyte?",typeof(sbyte?)},
                {"short?", typeof(short?)},
                {"ushort?", typeof(ushort?)},
                {"int?", typeof(int?)},
                {"uint?", typeof(uint?)},
                {"long?", typeof(long?)},
                {"ulong?", typeof(ulong?)},
                {"float?", typeof(float?)},
                {"double?", typeof(double?)},
                {"decimal?", typeof(decimal?)},
                {"bool?", typeof(bool?)},
                {"char?", typeof(char?)},
                {"string", typeof(string)}
            };
        }

        public Type GetType(string name)
        {
            //todo: there can be multiple classes with the same name
            Type type = solutionTypes.FirstOrDefault(x => x.Name == name);
            return type ?? coreTypes[name];
        }
    }
}