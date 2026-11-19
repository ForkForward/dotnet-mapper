namespace ForkForward.Mapper.QueryableExtensions.Impl;
using static ReflectionHelper;
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EnumerableProjectionMapper : IProjectionMapper
{
    private static readonly MethodInfo SelectMethod = typeof(Enumerable).StaticGenericMethod("Select", parametersCount: 2);
    private static readonly MethodInfo ToArrayMethod = typeof(Enumerable).GetStaticMethod("ToArray");
    private static readonly MethodInfo ToListMethod = typeof(Enumerable).GetStaticMethod("ToList");
    public bool IsMatch(TypePair context) => context.IsCollection();
    public Expression Project(IGlobalConfiguration configuration, in ProjectionRequest request, Expression resolvedSource, LetPropertyMaps letPropertyMaps)
    {
        var destinationType = request.DestinationType;
        var destinationListType = GetElementType(destinationType);
        var sourceListType = GetElementType(request.SourceType);
        var sourceExpression = resolvedSource;
        if (sourceListType != destinationListType)
        {
            var itemRequest = request.InnerRequest(sourceListType, destinationListType);
            var transformedExpressions = configuration.ProjectionBuilder.CreateProjection(itemRequest, letPropertyMaps.New());
            if(transformedExpressions.Empty)
            {
                return null;
            }
            sourceExpression = transformedExpressions.Chain(sourceExpression, Select);
        }
        if (!destinationType.IsAssignableFrom(sourceExpression.Type))
        {
            var convertFunction = destinationType.IsArray ? ToArrayMethod : ToListMethod;
            convertFunction = convertFunction.MakeGenericMethod(destinationListType);
            if (destinationType.IsAssignableFrom(convertFunction.ReturnType))
            {
                sourceExpression = Call(convertFunction, sourceExpression);
            }
            else
            {
                var ctorInfo = destinationType.GetConstructor([sourceExpression.Type]) ?? SetConstructor(destinationType, destinationListType, sourceExpression.Type);
                if (ctorInfo is null)
                {
                    throw CannotCreate(request, sourceExpression.Type, letPropertyMaps?.CurrentMember);
                }
                sourceExpression = New(ctorInfo, sourceExpression);
            }
        }
        return sourceExpression;
    }
    // ISet<T> and IReadOnlySet<T> get what Map creates for them, a HashSet<T>.
    private static ConstructorInfo SetConstructor(Type destinationType, Type elementType, Type sourceType)
    {
        var setType = typeof(HashSet<>).MakeGenericType(elementType);
        return destinationType.IsAssignableFrom(setType) ? setType.GetConstructor([sourceType]) : null;
    }
    private static AutoMapperMappingException CannotCreate(in ProjectionRequest request, Type constructorArgument, MemberMap memberMap)
    {
        var message = $"ProjectTo cannot create {request.DestinationType} from {request.SourceType}: it is not an array, neither a List<T> nor a HashSet<T> can be assigned to it, " +
            $"and it has no public constructor that takes {constructorArgument}. Use a collection type that can be created in one of these ways for the destination member, or create the collection in MapFrom.";
        return memberMap == null ? new(message, null, new TypePair(request.SourceType, request.DestinationType)) : new(message, null, memberMap);
    }
    private static Expression Select(Expression source, LambdaExpression lambda) =>
        Call(SelectMethod.MakeGenericMethod(lambda.Parameters[0].Type, lambda.ReturnType), source, lambda);
}