namespace ForkForward.Mapper.Configuration;
public interface IPropertyMapConfiguration
{
    void Configure(TypeMap typeMap);
    MemberInfo DestinationMember { get; }
    LambdaExpression SourceExpression { get; }
    LambdaExpression GetDestinationExpression();
    IPropertyMapConfiguration Reverse();
    bool Ignored => false;
}
public class MemberConfigurationExpression<TSource, TDestination, TMember>(MemberInfo destinationMember, Type sourceType) : IMemberConfigurationExpression<TSource, TDestination, TMember>, IPropertyMapConfiguration
{
    private MemberInfo[] _sourceMembers;
    private readonly Type _sourceType = sourceType;
    protected List<Action<PropertyMap>> PropertyMapActions { get; } = [];
    public MemberInfo DestinationMember { get; } = destinationMember;
    public void MapAtRuntime() => PropertyMapActions.Add(pm => pm.Inline = false);
    public void NullSubstitute(object nullSubstitute) => PropertyMapActions.Add(pm => pm.NullSubstitute = nullSubstitute);
    public void MapFrom<TValueResolver>() where TValueResolver : IValueResolver<TSource, TDestination, TMember> =>
        MapFromCore(new(typeof(TValueResolver), typeof(IValueResolver<TSource, TDestination, TMember>)));
    protected void MapFromCore(ClassValueResolver config) => SetResolver(config);
    protected void SetResolver(IValueResolver config) => PropertyMapActions.Add(pm => pm.SetResolver(config));
    public void MapFrom<TValueResolver, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember)
        where TValueResolver : IMemberValueResolver<TSource, TDestination, TSourceMember, TMember> =>
            MapFromCore<TValueResolver, TSourceMember>(sourceMember);
    public void MapFrom<TValueResolver, TSourceMember>(string sourceMemberName) where TValueResolver : IMemberValueResolver<TSource, TDestination, TSourceMember, TMember> =>
            MapFromCore<TValueResolver, TSourceMember>(null, sourceMemberName);
    private void MapFromCore<TValueResolver, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember, string sourceMemberName = null) where TValueResolver : IMemberValueResolver<TSource, TDestination, TSourceMember, TMember> =>
        MapFromCore(new(typeof(TValueResolver), typeof(IMemberValueResolver<TSource, TDestination, TSourceMember, TMember>), sourceMemberName)
        {
            SourceMemberLambda = sourceMember
        });
    public void MapFrom(IValueResolver<TSource, TDestination, TMember> valueResolver) =>
        MapFromCore(new(valueResolver, typeof(IValueResolver<TSource, TDestination, TMember>)));
    public void MapFrom<TSourceMember>(IMemberValueResolver<TSource, TDestination, TSourceMember, TMember> valueResolver, Expression<Func<TSource, TSourceMember>> sourceMember) =>
        MapFromCore(new(valueResolver, typeof(IMemberValueResolver<TSource, TDestination, TSourceMember, TMember>))
        {
            SourceMemberLambda = sourceMember
        });
    public void MapFrom<TResult>(Func<TSource, TDestination, TResult> mappingFunction) =>
        MapFromFunc((src, dest, destMember, ctxt) => mappingFunction(src, dest));
    public void MapFrom<TResult>(Func<TSource, TDestination, TMember, TResult> mappingFunction) =>
        MapFromFunc((src, dest, destMember, ctxt) => mappingFunction(src, dest, destMember));
    public void MapFrom<TResult>(Func<TSource, TDestination, TMember, ResolutionContext, TResult> mappingFunction) =>
        MapFromFunc((src, dest, destMember, ctxt) => mappingFunction(src, dest, destMember, ctxt));
    private void MapFromFunc<TResult>(Expression<Func<TSource, TDestination, TMember, ResolutionContext, TResult>> expr) => 
        SetResolver(new FuncResolver(expr));
    public void MapFrom<TSourceMember>(Expression<Func<TSource, TSourceMember>> mapExpression) => MapFromExpression(mapExpression);
    internal void MapFromExpression(LambdaExpression sourceExpression)
    {
        SourceExpression = sourceExpression;
        PropertyMapActions.Add(pm => pm.MapFrom(sourceExpression));
    }
    public void MapFrom(string sourceMembersPath)
    {
        _sourceMembers = ReflectionHelper.GetMemberPath(_sourceType, sourceMembersPath);
        PropertyMapActions.Add(pm => pm.MapFrom(sourceMembersPath, _sourceMembers));
    }
    public void Condition(Func<TSource, TDestination, TMember, TMember, ResolutionContext, bool> condition) =>
        ConditionCore((src, dest, srcMember, destMember, ctxt) => condition(src, dest, srcMember, destMember, ctxt));
    public void Condition(Func<TSource, TDestination, TMember, TMember, bool> condition) =>
        ConditionCore((src, dest, srcMember, destMember, ctxt) => condition(src, dest, srcMember, destMember));
    public void Condition(Func<TSource, TDestination, TMember, bool> condition) => 
        ConditionCore((src, dest, srcMember, destMember, ctxt) => condition(src, dest, srcMember));
    public void Condition(Func<TSource, TDestination, bool> condition) => ConditionCore((src, dest, srcMember, destMember, ctxt) => condition(src, dest));
    public void Condition(Func<TSource, bool> condition) => ConditionCore((src, dest, srcMember, destMember, ctxt) => condition(src));
    private void ConditionCore(Expression<Func<TSource, TDestination, TMember, TMember, ResolutionContext, bool>> expr) =>
        PropertyMapActions.Add(pm => pm.Condition = expr);
    public void Condition<TCondition>() where TCondition : IMemberCondition<TSource, TDestination, TMember> => ConditionCore(typeof(TCondition), nameof(TCondition));
    private protected void ConditionCore(Type conditionType, string parameterName)
    {
        ClassCondition.Check(conditionType, typeof(IMemberCondition<,,>), parameterName);
        PropertyMapActions.Add(pm => pm.Condition = ClassCondition.Create(pm.TypeMap, conditionType, typeof(IMemberCondition<,,>)));
    }
    public void PreCondition(Func<TSource, bool> condition) => PreConditionCore((src, dest, ctxt) => condition(src));
    public void PreCondition(Func<ResolutionContext, bool> condition) => PreConditionCore((src, dest, ctxt) => condition(ctxt));
    public void PreCondition(Func<TSource, ResolutionContext, bool> condition) => PreConditionCore((src, dest, ctxt) => condition(src, ctxt));
    public void PreCondition(Func<TSource, TDestination, ResolutionContext, bool> condition) => PreConditionCore((src, dest, ctxt) => condition(src, dest, ctxt));
    private void PreConditionCore(Expression<Func<TSource, TDestination, ResolutionContext, bool>> expr) =>
        PropertyMapActions.Add(pm => pm.PreCondition = expr);
    public void PreCondition<TPreCondition>() where TPreCondition : IMemberPreCondition<TSource, TDestination> => PreConditionCore(typeof(TPreCondition), nameof(TPreCondition));
    private protected void PreConditionCore(Type preConditionType, string parameterName)
    {
        ClassCondition.Check(preConditionType, typeof(IMemberPreCondition<,>), parameterName);
        PropertyMapActions.Add(pm => pm.PreCondition = ClassCondition.Create(pm.TypeMap, preConditionType, typeof(IMemberPreCondition<,>)));
    }
    public void AddTransform(Expression<Func<TMember, TMember>> transformer) =>
        PropertyMapActions.Add(pm => pm.AddValueTransformation(new ValueTransformerConfiguration(pm.DestinationType, transformer)));
    public void ExplicitExpansion(bool value) => PropertyMapActions.Add(pm => pm.ExplicitExpansion = value);
    public void Ignore() => Ignore(ignorePaths: true);
    public void Ignore(bool ignorePaths)
    {
        Ignored = true;
        PropertyMapActions.Add(pm =>
        {
            pm.Ignored = true;
            if (ignorePaths && pm.TypeMap.PathMaps.Count > 0)
            {
                pm.TypeMap.IgnorePaths(DestinationMember);
            }
        });
    }
    public void AllowNull() => SetAllowNull(true);
    public void DoNotAllowNull() => SetAllowNull(false);
    private void SetAllowNull(bool value) => PropertyMapActions.Add(pm => pm.AllowNull = value);
    public void UseDestinationValue() => SetUseDestinationValue(true);
    private void SetUseDestinationValue(bool value) => PropertyMapActions.Add(pm => pm.UseDestinationValue = value);
    public void SetMappingOrder(int mappingOrder) => PropertyMapActions.Add(pm => pm.MappingOrder = mappingOrder);
    public void ConvertUsing<TValueConverter, TSourceMember>() where TValueConverter : IValueConverter<TSourceMember, TMember> => 
        ConvertUsingCore<TValueConverter, TSourceMember>();
    public void ConvertUsing<TValueConverter, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember) where TValueConverter : IValueConverter<TSourceMember, TMember> => 
        ConvertUsingCore<TValueConverter, TSourceMember>(sourceMember);
    public void ConvertUsing<TValueConverter, TSourceMember>(string sourceMemberName) where TValueConverter : IValueConverter<TSourceMember, TMember> => 
        ConvertUsingCore<TValueConverter, TSourceMember>(null, sourceMemberName);
    public void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter) => ConvertUsingCore(valueConverter);
    public void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter, Expression<Func<TSource, TSourceMember>> sourceMember) =>
        ConvertUsingCore(valueConverter, sourceMember);
    public void ConvertUsing<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter, string sourceMemberName)  =>
        ConvertUsingCore(valueConverter, null, sourceMemberName);
    private void ConvertUsingCore<TValueConverter, TSourceMember>(Expression<Func<TSource, TSourceMember>> sourceMember = null, string sourceMemberName = null) =>
        ConvertUsingCore(new(typeof(TValueConverter), typeof(IValueConverter<TSourceMember, TMember>), sourceMemberName)
        {
            SourceMemberLambda = sourceMember,
        });
    protected void ConvertUsingCore(ValueConverter converter) => SetResolver(converter);
    private void ConvertUsingCore<TSourceMember>(IValueConverter<TSourceMember, TMember> valueConverter,
        Expression<Func<TSource, TSourceMember>> sourceMember = null, string sourceMemberName = null) =>
        ConvertUsingCore(new(valueConverter, typeof(IValueConverter<TSourceMember, TMember>), sourceMemberName)
        {
            SourceMemberLambda = sourceMember,
        });
    public void Configure(TypeMap typeMap)
    {
        var destMember = DestinationMember;
        if(destMember.DeclaringType.ContainsGenericParameters)
        {
            destMember = Array.Find(typeMap.DestinationTypeDetails.ReadAccessors, m => m.MetadataToken == destMember.MetadataToken);
        }
        var propertyMap = typeMap.FindOrCreatePropertyMapFor(destMember, typeof(TMember) == typeof(object) ? destMember.GetMemberType() : typeof(TMember));
        Apply(propertyMap);
    }
    private void Apply(PropertyMap propertyMap)
    {
        foreach(var action in PropertyMapActions)
        {
            action(propertyMap);
        }
    }
    public LambdaExpression SourceExpression { get; private set; }
    public bool Ignored { get; private set; }
    public LambdaExpression GetDestinationExpression() => DestinationMember.Lambda();
    public IPropertyMapConfiguration Reverse()
    {
        var destinationType = DestinationMember.DeclaringType;
        if (_sourceMembers != null)
        {
            if (_sourceMembers.Length > 1)
            {
                return null;
            }
            MemberConfigurationExpression<TDestination, TSource, object> reversedMemberConfiguration = new(_sourceMembers[0], destinationType);
            reversedMemberConfiguration.MapFrom(DestinationMember.Name);
            return reversedMemberConfiguration;
        }
        if (destinationType.IsGenericTypeDefinition) // .ForMember("InnerSource", o => o.MapFrom(s => s))
        {
            return null;
        }
        return PathConfigurationExpression<TDestination, TSource, object>.Create(SourceExpression, GetDestinationExpression());
    }
    public void DoNotUseDestinationValue() => SetUseDestinationValue(false);
}
public sealed class MemberConfigurationExpression(MemberInfo destinationMember, Type sourceType) : MemberConfigurationExpression<object, object, object>(destinationMember, sourceType), IMemberConfigurationExpression
{
    public void MapFrom(Type valueResolverType) => MapFromCore(new(valueResolverType, valueResolverType.GetGenericInterface(typeof(IValueResolver<,,>))));
    public void MapFrom(Type valueResolverType, string sourceMemberName) =>
            MapFromCore(new(valueResolverType, valueResolverType.GetGenericInterface(typeof(IMemberValueResolver<,,,>)), sourceMemberName));
    public void MapFrom<TSource, TDestination, TSourceMember, TDestMember>(IMemberValueResolver<TSource, TDestination, TSourceMember, TDestMember> resolver, string sourceMemberName) =>
        MapFromCore(new(resolver, typeof(IMemberValueResolver<TSource, TDestination, TSourceMember, TDestMember>), sourceMemberName));
    public void Condition(Type conditionType) => ConditionCore(conditionType, nameof(conditionType));
    public void PreCondition(Type preConditionType) => PreConditionCore(preConditionType, nameof(preConditionType));
    public void ConvertUsing(Type valueConverterType) => ConvertUsingCore(valueConverterType);
    public void ConvertUsing(Type valueConverterType, string sourceMemberName) => ConvertUsingCore(valueConverterType, sourceMemberName);
    public void ConvertUsing<TSourceMember, TDestinationMember>(IValueConverter<TSourceMember, TDestinationMember> valueConverter, string sourceMemberName) =>
        base.ConvertUsingCore(new(valueConverter, typeof(IValueConverter<TSourceMember, TDestinationMember>), sourceMemberName));
    private void ConvertUsingCore(Type valueConverterType, string sourceMemberName = null) =>
        base.ConvertUsingCore(new(valueConverterType, valueConverterType.GetGenericInterface(typeof(IValueConverter<,>)), sourceMemberName));
}
// A condition or a precondition written as a class. MemberMap.Condition and MemberMap.PreCondition stay lambdas:
// the one built here has the parameter types the class declares through the interface it implements, so the plan
// converts the arguments to them, and a body that creates the instance through the service constructor and calls
// ShouldMap. The parameter type of the source member is also how the plan knows which value the condition is given.
static class ClassCondition
{
    public static void Check(Type conditionType, Type openInterface, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(conditionType, parameterName);
        GetInterface(conditionType, openInterface, parameterName);
    }
    public static LambdaExpression Create(TypeMap typeMap, Type conditionType, Type openInterface)
    {
        if (conditionType.IsGenericTypeDefinition)
        {
            // Closed like an open generic value resolver, with the type arguments of the closed map. The open map
            // is configured too, and there is nothing to close the class with yet. It is never executed, but a map
            // can inherit from it: that map gets a condition that says so, where no condition would map the member.
            if (typeMap.Types.ContainsGenericParameters)
            {
                return Lambda(Throw(Constant(new AutoMapperConfigurationException(
                    $"{conditionType} is an open generic type. It is closed with the type arguments of the map it is configured on, and a map that inherits from {typeMap.SourceType} -> {typeMap.DestinationType} does not inherit it. Configure it on that map too, or use a class that is not generic.")),
                    typeof(bool)));
            }
            try
            {
                conditionType = typeMap.MakeGenericType(conditionType);
            }
            // A map without type arguments, with fewer of them than the class has type parameters, or with one that the class does not accept.
            catch (ArgumentException ex)
            {
                throw new AutoMapperConfigurationException(
                    $"Cannot close the open generic {(openInterface == typeof(IMemberPreCondition<,>) ? "precondition" : "condition")} class {conditionType} for the map from {typeMap.SourceType} to {typeMap.DestinationType}. " +
                    "It is closed with the type arguments of the source type, followed by those of the destination type.", ex);
            }
        }
        var interfaceType = GetInterface(conditionType, openInterface, nameof(conditionType));
        var shouldMap = interfaceType.GetMethod("ShouldMap");
        var parameters = Array.ConvertAll(shouldMap.GetParameters(), parameter => Parameter(parameter.ParameterType, parameter.Name));
        parameters[^1] = ContextParameter;
        return Lambda(Call(ToType(ServiceLocator(conditionType), interfaceType), shouldMap, parameters), parameters);
    }
    // A class that implements the interface more than once is refused: the interface is contravariant, so more than
    // one implementation can accept the same member, and nothing here could say which one was meant.
    static Type GetInterface(Type conditionType, Type openInterface, string parameterName)
    {
        var interfaceType = conditionType.IsGenericType(openInterface) ? conditionType : null;
        foreach (var implemented in conditionType.GetInterfaces())
        {
            if (!implemented.IsGenericType(openInterface))
            {
                continue;
            }
            if (interfaceType != null)
            {
                throw new ArgumentException($"{conditionType} implements {Name(openInterface)} more than once. Implement it once, or write one class for each set of type arguments.", parameterName);
            }
            interfaceType = implemented;
        }
        return interfaceType ?? throw new ArgumentException($"{conditionType} does not implement {Name(openInterface)}.", parameterName);
    }
    static string Name(Type openInterface) =>
        $"{openInterface.Name[..openInterface.Name.IndexOf('`')]}<{string.Join(", ", openInterface.GetGenericArguments().Select(argument => argument.Name))}>";
}