using System.Runtime.CompilerServices;
namespace ForkForward.Mapper.Internal;
[DebuggerDisplay("{RequestedTypes.SourceType.Name}, {RequestedTypes.DestinationType.Name} : {RuntimeTypes.SourceType.Name}, {RuntimeTypes.DestinationType.Name}")]
public readonly record struct MapRequest(TypePair RequestedTypes, TypePair RuntimeTypes, MemberMap MemberMap)
{
    public MapRequest(TypePair types) : this(types, types, MemberMap.Instance) { }
    // This is the key of the cache of execution plans. A built-in mapper builds its plan for the member the request
    // comes from: the MaxDepth of its map, whether it must use the destination value, the member a mapping error
    // names. So requests from different members are different requests, also when their types are the same.
    // A Map call passes no member; CompileMappings and the mappers of dynamic objects and dictionaries pass
    // MemberMap.Instance, which belongs to no type map. Those are requests without a member, and they share one plan
    // per pair of types.
    // Equals and GetHashCode run on every Map call, nested ones included, and the lookup is as fast as it is only
    // while the JIT compiler inlines them into it. So every request takes one path: the types are compared and
    // hashed as they were when the member was not part of the key, and the member is tested in place. Measured on
    // .NET 10, the lookup takes twice as long with that test in a property of this struct, and also, for the kind
    // of request that was not being mapped when the code was optimised, when requests with a member are hashed
    // differently or kept in a cache of their own.
    public bool Equals(MapRequest other) => RequestedTypes.Equals(other.RequestedTypes) && RuntimeTypes.Equals(other.RuntimeTypes) &&
        (MemberMap == other.MemberMap || (MemberMap?.TypeMap == null && other.MemberMap?.TypeMap == null));
    public override int GetHashCode()
    {
        var hashCode = HashCode.Combine(RequestedTypes, RuntimeTypes);
        var memberMap = MemberMap;
        return memberMap?.TypeMap == null ? hashCode : hashCode ^ RuntimeHelpers.GetHashCode(memberMap);
    }
    internal bool HasMember => MemberMap?.TypeMap != null;
}
[DebuggerDisplay("{SourceType.Name}, {DestinationType.Name}")]
public readonly record struct TypePair(Type SourceType, Type DestinationType)
{
    public bool IsConstructedGenericType => SourceType.IsConstructedGenericType || DestinationType.IsConstructedGenericType;
    public bool ContainsGenericParameters => SourceType.ContainsGenericParameters || DestinationType.ContainsGenericParameters;
    public TypePair CloseGenericTypes(TypePair closedTypes)
    {
        var sourceArguments = closedTypes.SourceType.GenericTypeArguments;
        var destinationArguments = closedTypes.DestinationType.GenericTypeArguments;
        if(sourceArguments.Length == 0)
        {
            sourceArguments = destinationArguments;
        }
        else if(destinationArguments.Length == 0)
        {
            destinationArguments = sourceArguments;
        }
        var closedSourceType = SourceType.IsGenericTypeDefinition ? SourceType.MakeGenericType(sourceArguments) : SourceType;
        var closedDestinationType = DestinationType.IsGenericTypeDefinition ? DestinationType.MakeGenericType(destinationArguments) : DestinationType;
        return new(closedSourceType, closedDestinationType);
    }
    public TypePair Reverse() => new(DestinationType, SourceType);
    public Type ITypeConverter() => ContainsGenericParameters ? null : typeof(ITypeConverter<,>).MakeGenericType(SourceType, DestinationType);
    public TypePair GetTypeDefinitionIfGeneric() => new(GetTypeDefinitionIfGeneric(SourceType), GetTypeDefinitionIfGeneric(DestinationType));
    static Type GetTypeDefinitionIfGeneric(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition() : type;
}