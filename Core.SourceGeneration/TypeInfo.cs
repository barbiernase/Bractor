using System.Collections.Generic;

namespace Core.SourceGeneration
{
    public abstract class TypeInfo
    {
        public abstract string Name { get; }
        public abstract string FullName { get; }
        public abstract IEnumerable<string> Interfaces { get; }
        public abstract IEnumerable<ConstructorInfo> Constructors { get; }
        public abstract bool IsGenericType { get; }
        public abstract IEnumerable<TypeInfo> TypeArguments { get; }
    }
    
    public class ConstructorInfo
    {
        public IEnumerable<ParameterInfo> Parameters { get; set; }
        public bool IsPublic { get; set; }
    }
    
    public class ParameterInfo
    {
        public string Name { get; set; }
        public TypeInfo Type { get; set; }
    }
}