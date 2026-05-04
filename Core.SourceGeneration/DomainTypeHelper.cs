using System.Collections.Generic;
using System.Linq;
using Abstractions.SourceGeneration;

namespace Core.SourceGeneration
{
    public static class DomainTypeHelper
    {
        private static readonly HashSet<string> EventInterfaces = new()
        {
            "Abstractions.IEvent",
            "IEvent"
        };
        
        private static readonly HashSet<string> CommandInterfaces = new()
        {
            "Abstractions.ICommand",
            "Abstractions.ICreationCommand",
            "ICreationCommand",
            "ICommand"
        };
        
        private static readonly HashSet<string> QueryInterfaces = new()
        {
            "Abstractions.IQuery",
            "IQuery"
        };

        // NEU
        private static readonly HashSet<string> QueryResponseInterfaces = new()
        {
            "Abstractions.IQueryResponse",
            "IQueryResponse"
        };
        
        public static Abstractions.SourceGeneration.DomainType ClassifyByInterfaces(IEnumerable<string> interfaces)
        {
            var interfaceList = interfaces.ToList();
            
            if (interfaceList.Any(i => EventInterfaces.Contains(i) || EventInterfaces.Any(ei => i.EndsWith(ei))))
                return Abstractions.SourceGeneration.DomainType.Event;
                
            if (interfaceList.Any(i => CommandInterfaces.Contains(i) || CommandInterfaces.Any(ci => i.EndsWith(ci))))
                return Abstractions.SourceGeneration.DomainType.Command;
                
            if (interfaceList.Any(i => QueryInterfaces.Contains(i) || QueryInterfaces.Any(qi => i.EndsWith(qi))))
                return DomainType.Query;

            if (interfaceList.Any(i => QueryResponseInterfaces.Contains(i) || QueryResponseInterfaces.Any(qri => i.EndsWith(qri))))
                return DomainType.QueryResponse;
            
            return Abstractions.SourceGeneration.DomainType.Object;
        }
    }
}