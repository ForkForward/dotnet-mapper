namespace ForkForward.Mapper;
using Features;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

/// <summary>
/// Main configuration object holding all mapping configuration for a source and destination type
/// </summary>
[DebuggerDisplay("{SourceType.Name} -> {DestinationType.Name}")]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TypeMap
{
    static readonly LambdaExpression EmptyLambda = Lambda(ExpressionBuilder.Empty);
    static readonly MethodInfo CreateProxyMethod = typeof(ObjectFactory).GetStaticMethod(nameof(ObjectFactory.CreateInterfaceProxy));
    TypeMapDetails _details;
    List<PropertyMap> _propertyMaps;
    bool _sealed;
    public TypeMap(Type sourceType, Type destinationType, ProfileMap profile, TypeMapConfiguration typeMapConfiguration, List<MemberInfo> sourceMembers = null)
    {
        Types = new(sourceType, destinationType);
        Profile = profile;
        if (typeMapConfiguration?.HasTypeConverter is true)
        {
            return;
        }
        SourceTypeDetails = profile.CreateTypeDetails(sourceType);
        DestinationTypeDetails = profile.CreateTypeDetails(destinationType);
        sourceMembers ??= [];
        var isReverseMap = typeMapConfiguration?.IsReverseMap is true;
        foreach (var destinationProperty in DestinationTypeDetails.WriteAccessors)
        {
            var destinationName = destinationProperty.Name;
            var memberConfig = typeMapConfiguration?.GetDestinationMemberConfiguration(destinationProperty);
            if (memberConfig?.Ignored is true || profile.GlobalIgnores.Contains(destinationName))
            {
                continue;
            }
            sourceMembers.Clear();
            var propertyType = destinationProperty.GetMemberType();
            if (profile.MapDestinationPropertyToSource(SourceTypeDetails, destinationType, propertyType, destinationName, sourceMembers, isReverseMap, out var throughChain))
            {
                AddPropertyMap(destinationProperty, propertyType, sourceMembers, throughChain);
            }
        }
    }
    public string CheckRecord() => ConstructorMap?.Ctor is ConstructorInfo ctor && ctor.IsFamily && ctor.Has<CompilerGeneratedAttribute>() ?
        " When mapping to records, consider using only public constructors. See https://docs.automapper.org/en/latest/Construction.html." : null;
    public Features<IRuntimeFeature> Features => Details.Features;
    private TypeMapDetails Details => _details ??= new();
    public bool HasDetails => _details != null;
    public void CheckProjection()
    {
        if (Projection)
        {
            throw new AutoMapperConfigurationException("CreateProjection works with ProjectTo, not with Map.", MissingMapException(Types));
        }
    }
    public static Exception MissingMapException(TypePair types) => MissingMapException(types.SourceType, types.DestinationType);
    public static Exception MissingMapException(Type sourceType, Type destinationType)
    {
        InvalidOperationException exception = new($"Missing map from {sourceType} to {destinationType}. Create using CreateMap<{sourceType.Name}, {destinationType.Name}>.");
        // It is thrown on purpose and says what is missing, but its type is the framework's: the mark is how
        // ExpressionBuilder.IsUnexpected knows it, so that it is passed on as it is, wherever it is thrown.
        exception.Data[MissingMapMark] = true;
        return exception;
    }
    const string MissingMapMark = "ForkForward.Mapper.MissingMap";
    internal static bool IsMissingMapException(Exception exception) => exception is InvalidOperationException && exception.Data.Contains(MissingMapMark);
    public bool Projection { get; set; }
    public LambdaExpression MapExpression { get; private set; }
    public Expression Invoke(Expression source, Expression destination) =>
        Expression.Invoke(MapExpression, ToType(source, SourceType), ToType(destination, DestinationType), ContextParameter);
    internal bool CanConstructorMap() => Profile.ConstructorMappingEnabled && !DestinationType.IsAbstract &&
        !CustomConstruction && !HasTypeConverter && DestinationConstructors.Length > 0;
    public TypePair Types;
    public ConstructorMap ConstructorMap { get; set; }
    public TypeDetails SourceTypeDetails { get; private set; }
    public TypeDetails DestinationTypeDetails { get; private set; }
    public Type SourceType => Types.SourceType;
    public Type DestinationType => Types.DestinationType;
    public ProfileMap Profile { get; }
    public LambdaExpression CustomMapExpression => TypeConverter?.ProjectToExpression;
    public LambdaExpression CustomCtorFunction { get => _details?.CustomCtorFunction; set => Details.CustomCtorFunction = value; }
    public LambdaExpression CustomCtorExpression => CustomCtorFunction is { Parameters: [_] } ? CustomCtorFunction : null;
    public bool IncludeAllDerivedTypes { get => (_details?.IncludeAllDerivedTypes).GetValueOrDefault(); set => Details.IncludeAllDerivedTypes = value; }
    /// <summary>
    /// Whether ProjectTo uses the derived maps of this map. Null when the map has no setting of its own and follows the configuration.
    /// </summary>
    public bool? PolymorphicProjection { get => _details?.PolymorphicProjection; set => Details.PolymorphicProjection = value; }
    public MemberList ConfiguredMemberList
    {
        get => (_details?.ConfiguredMemberList).GetValueOrDefault();
        set 
        {
            if (_details == null && value == default)
            {
                return;
            }
            Details.ConfiguredMemberList = value;
        }
    }
    public IReadOnlyCollection<TypePair> IncludedDerivedTypes => (_details?.IncludedDerivedTypes).NullCheck();
    public IReadOnlyCollection<TypePair> IncludedBaseTypes => (_details?.IncludedBaseTypes).NullCheck();
    public IReadOnlyCollection<LambdaExpression> BeforeMapActions => (_details?.BeforeMapActions).NullCheck();
    public IReadOnlyCollection<LambdaExpression> AfterMapActions => (_details?.AfterMapActions).NullCheck();
    public IReadOnlyCollection<ValueTransformerConfiguration> ValueTransformers => (_details?.ValueTransformerConfigs).NullCheck();
    // The destination objects are cached by the source object, by reference. A value type has no reference to find it by
    // (boxed, every use is another object), and a destination that is a value type is copied wherever it is used, so
    // there is nothing to preserve: such a map does not use the cache and is mapped as without PreserveReferences.
    public bool PreserveReferences { get => (_details?.PreserveReferences).GetValueOrDefault(); set => Details.PreserveReferences = value && !SourceType.IsValueType && !DestinationType.IsValueType; }
    public int MaxDepth { get => (_details?.MaxDepth).GetValueOrDefault(); set => Details.MaxDepth = value; }
    // In a projection MaxDepth is not a depth: it counts how many times the map has been used in the query. A derived
    // map does not inherit it there; it would leave members empty in projections that are complete today.
    internal int ProjectionMaxDepth => _details is { MaxDepthInherited: false } ? _details.MaxDepth : 0;
    public bool DisableConstructorValidation { get => (_details?.DisableConstructorValidation).GetValueOrDefault(); set => Details.DisableConstructorValidation = value; }
    public IReadOnlyCollection<PropertyMap> PropertyMaps => _propertyMaps.NullCheck();
    public IReadOnlyCollection<PathMap> PathMaps => (_details?.PathMaps).NullCheck();
    public IEnumerable<MemberMap> MemberMaps
    {
        get
        {
            var maps = PropertyMaps.Concat((IReadOnlyCollection<MemberMap>)PathMaps);
            if (ConstructorMapping)
            {
                maps = maps.Concat(ConstructorMap.CtorParams);
            }
            return maps;
        }
    }
    public bool PassesCtorValidation =>
        DisableConstructorValidation
        || CustomConstruction
        || ConstructorMapping
        || DestinationType.IsAbstract
        || DestinationType.IsValueType
        || TypeDetails.GetConstructors(DestinationType, Profile).Any(c => c.AllParametersOptional());
    public MemberInfo[] DestinationSetters => DestinationTypeDetails.WriteAccessors;
    public ConstructorParameters[] DestinationConstructors => DestinationTypeDetails.Constructors;
    public bool ConstructorMapping => ConstructorMap is { CanResolve: true };
    public bool CustomConstruction => CustomCtorFunction != null;
    public bool HasTypeConverter => TypeConverter != null;
    public Execution.TypeConverter TypeConverter { get; set; }
    public bool ShouldCheckForValid => ConfiguredMemberList != MemberList.None && !HasTypeConverter;
    public LambdaExpression[] IncludedMembers { get => _details?.IncludedMembers ?? []; set => Details.IncludedMembers = value; }
    public string[] IncludedMembersNames { get => _details?.IncludedMembersNames ?? []; set => Details.IncludedMembersNames = value; }
    public IReadOnlyCollection<IncludedMember> IncludedMembersTypeMaps => (_details?.IncludedMembersTypeMaps).NullCheck();
    public Type MakeGenericType(Type type) => type.IsGenericTypeDefinition ?
        type.MakeGenericType(SourceType.GenericTypeArguments.Concat(DestinationType.GenericTypeArguments).Take(type.GenericParametersCount()).ToArray()) :
        type;
    public bool HasIncludedMembers => IncludedMembers.Length > 0 || IncludedMembersNames.Length > 0;
    public IEnumerable<LambdaExpression> GetAllIncludedMembers() => IncludedMembersNames.Length == 0 || SourceType.ContainsGenericParameters ?
        IncludedMembers : IncludedMembers.Concat(IncludedMembersNames.Select(name => MemberAccessLambda(SourceType, name, this)));
    public bool ConstructorParameterMatches(string destinationPropertyName) => ConstructorMapping && ConstructorMap[destinationPropertyName] != null;
    private void AddPropertyMap(MemberInfo destProperty, Type destinationPropertyType, List<MemberInfo> sourceMembers, bool throughChain = false)
    {
        PropertyMap propertyMap = new(destProperty, destinationPropertyType, this);
        propertyMap.MapByConvention([..sourceMembers]);
        if (throughChain)
        {
            propertyMap.SourceMembersThroughChain = true;
        }
        AddPropertyMap(propertyMap);
    }
    private void AddPropertyMap(PropertyMap propertyMap)
    {
        _propertyMaps ??= [];
        _propertyMaps.Add(propertyMap);
    }
    public string[] GetUnmappedPropertyNames()
    {
        IEnumerable<string> properties;
        if (ConfiguredMemberList == MemberList.Destination)
        {
            properties = Profile.CreateTypeDetails(DestinationType).WriteAccessors
                .Select(p => p.Name)
                .Where(p => !ConstructorParameterMatches(p))
                .Except(MappedMembers().Select(m => m.DestinationName))
                .Except(PathMaps.Select(p => p.MemberPath.First.Name));
        }
        else
        {
            var ignoredSourceMembers = _details?.SourceMemberConfigs?
                .Where(smc => smc.Ignored)
                .Select(pm => pm.SourceMember.Name);
            properties = Profile.CreateTypeDetails(SourceType).ReadAccessors
                .Select(p => p.Name)
                .Except(MappedMembers().Select(m => m.GetSourceMemberName()))
                .Except(IncludedMembersNames)
                .Except(IncludedMembers.Select(m => m.GetMember()?.Name))
                .Except(ignoredSourceMembers ?? []);
        }
        return properties.Where(memberName => !Profile.GlobalIgnores.Any(memberName.StartsWith)).ToArray();
        IEnumerable<MemberMap> MappedMembers() => MemberMaps.Where(pm => pm.IsMapped);
    }
    public PropertyMap FindOrCreatePropertyMapFor(MemberInfo destinationProperty, Type destinationPropertyType)
    {
        var propertyMap = GetPropertyMap(destinationProperty.Name);
        if (propertyMap == null)
        {
            propertyMap = new(destinationProperty, destinationPropertyType, this);
            AddPropertyMap(propertyMap);
        }
        return propertyMap;
    }
    private void CheckDifferent(TypePair types)
    {
        if (types == Types)
        {
            throw new InvalidOperationException($"You cannot include a type map into itself.{Environment.NewLine}Source type: {types.SourceType.FullName}{Environment.NewLine}Destination type: {types.DestinationType.FullName}");
        }
    }
    internal void IgnorePaths(MemberInfo destinationMember)
    {
        foreach (var pathMap in PathMaps)
        {
            if (pathMap.MemberPath.First == destinationMember)
            {
                pathMap.Ignored = true;
            }
        }
    }
    public bool HasDerivedTypesToInclude => IncludedDerivedTypes.Count > 0;
    public void Seal(IGlobalConfiguration configuration)
    {
        if (_sealed)
        {
            // A map that could not be sealed has no plan and will not get one: building it again would fail the same way.
            // Whoever reaches the map later, with other requested types, as a member of another map or for its plan, is
            // given the error of the first attempt, and not the plan that is missing.
            _details?.SealError?.Throw();
            return;
        }
        _sealed = true;
        try
        {
            // A map that still has an open generic destination factory here was not closed from an open map: with its own
            // types, which have no open side to take type arguments from, this says that the factory cannot be closed.
            CloseDestinationFactory(Types);
            _details?.Seal(configuration, this);
            MapExpression = Projection ? EmptyLambda : CreateMapperLambda(configuration);
        }
        // The filter keeps the error and says no. Plans are built one inside another, and the first of them can be built
        // deep in a source graph: a catch block that throws again at each level would run on top of the stack that has
        // not been unwound yet (see ExpressionBuilder.MaxExceptionWrappers). The error is passed on as it is.
        catch (Exception exception) when (KeepSealError(exception))
        {
        }
        SourceTypeDetails = null;
        DestinationTypeDetails = null;
    }
    bool KeepSealError(Exception exception)
    {
        Details.SealError = ExceptionDispatchInfo.Capture(exception);
        return false;
    }
    internal bool IsSealed => _sealed;
    // The maps Seal seals before this one: the maps it inherits from and the maps of its included members.
    internal TypeMap[] GetMapsToSealFirst()
    {
        var inheritedTypeMaps = _details?.InheritedTypeMaps;
        var includedMembersTypeMaps = _details?.IncludedMembersTypeMaps;
        return inheritedTypeMaps == null && includedMembersTypeMaps == null ? [] :
            [..inheritedTypeMaps.NullCheck(), ..includedMembersTypeMaps.NullCheck().Select(includedMember => includedMember.TypeMap)];
    }
    public List<PropertyMap> OrderedPropertyMaps()
    {
        if (HasMappingOrder())
        {
            _propertyMaps.Sort((left, right) => Comparer<int?>.Default.Compare(left.MappingOrder, right.MappingOrder));
        }
        return _propertyMaps;
        bool HasMappingOrder()
        {
            if (_propertyMaps == null)
            {
                return false;
            }
            foreach (var propertyMap in _propertyMaps)
            {
                if (propertyMap.MappingOrder != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
    public void IncludeDerivedTypes(TypePair derivedTypes)
    {
        CheckDifferent(derivedTypes);
        Details.IncludeDerivedTypes(derivedTypes);
    }
    public void IncludeBaseTypes(TypePair baseTypes)
    {
        CheckDifferent(baseTypes);
        Details.IncludeBaseTypes(baseTypes);
    }
    public void AddBeforeMapAction(LambdaExpression beforeMap) => Details.AddBeforeMapAction(beforeMap);
    public void AddAfterMapAction(LambdaExpression afterMap) => Details.AddAfterMapAction(afterMap);
    public void AddValueTransformation(ValueTransformerConfiguration config) => Details.AddValueTransformation(config);
    public void ConstructUsingServiceLocator() => CustomCtorFunction = Lambda(ServiceLocator(DestinationType));
    internal LambdaExpression CreateMapperLambda(IGlobalConfiguration configuration)
    {
        if (Types.ContainsGenericParameters)
        {
            return null;
        }
        TypeMapPlanBuilder builder = default;
        try
        {
            builder = new(configuration, this);
            return builder.CreateMapperLambda();
        }
        catch (Exception exception) when (IsUnexpected(exception))
        {
            throw PlanBuildingError(exception, this, builder.MemberMap);
        }
    }
    private PropertyMap GetPropertyMap(string name)
    {
        if (_propertyMaps == null)
        {
            return null;
        }
        foreach (var propertyMap in _propertyMaps)
        {
            if (propertyMap.DestinationName == name)
            {
                return propertyMap;
            }
        }
        return null;
    }
    private PropertyMap GetPropertyMap(PropertyMap propertyMap) => GetPropertyMap(propertyMap.DestinationName);
    public void AsProxy() => CustomCtorFunction = Lambda(Call(CreateProxyMethod, Constant(DestinationType)));
    internal void CopyInheritedMapsTo(TypeMap typeMap)
    {
        if (_details?.InheritedTypeMaps == null)
        {
            return;
        }
        _details.CopyInheritedMapsTo(typeMap);
    }
    public void CloseGenerics(TypeMapConfiguration openMapConfig, TypePair closedTypes)
    {
        TypeConverter?.CloseGenerics(openMapConfig, closedTypes);
        CloseDestinationFactory(openMapConfig.Types);
    }
    // An open generic destination factory that ForAllMaps gave to this map: the expression it was given to has the closed
    // types of the map and left the factory to be closed here, with the types of the open map (see OpenDestinationFactory).
    void CloseDestinationFactory(TypePair openTypes)
    {
        if (_details?.CustomCtorFunction is { Body: ConstantExpression { Value: OpenDestinationFactory closeDestinationFactory } })
        {
            _details.CustomCtorFunction = closeDestinationFactory(openTypes);
        }
    }
    public bool AddMemberMap(IncludedMember includedMember) => Details.AddMemberMap(includedMember);
    public PathMap FindOrCreatePathMapFor(LambdaExpression destinationExpression, MemberPath path, TypeMap typeMap) =>
        Details.FindOrCreatePathMapFor(destinationExpression, path, typeMap);
    public void AddInheritedMap(TypeMap inheritedTypeMap) => Details.AddInheritedMap(inheritedTypeMap);
    public SourceMemberConfig FindOrCreateSourceMemberConfigFor(MemberInfo sourceMember) => Details.FindOrCreateSourceMemberConfigFor(sourceMember);
    class TypeMapDetails
    {
        Features<IRuntimeFeature> _features;
        public bool PreserveReferences;
        public LambdaExpression[] IncludedMembers;
        public string[] IncludedMembersNames;
        public bool DisableConstructorValidation;
        public int MaxDepth;
        public bool MaxDepthInherited;
        public bool IncludeAllDerivedTypes;
        public bool? PolymorphicProjection;
        public LambdaExpression CustomCtorFunction;
        public ExceptionDispatchInfo SealError;
        public MemberList ConfiguredMemberList;
        public List<LambdaExpression> AfterMapActions { get; private set; }
        public List<LambdaExpression> BeforeMapActions { get; private set; }
        public List<TypePair> IncludedDerivedTypes { get; private set; }
        public List<TypePair> IncludedBaseTypes { get; private set; }
        public List<TypeMap> InheritedTypeMaps { get; private set; }
        public List<IncludedMember> IncludedMembersTypeMaps { get; private set; }
        public List<PathMap> PathMaps { get; private set; }
        public List<SourceMemberConfig> SourceMemberConfigs { get; private set; }
        public List<ValueTransformerConfiguration> ValueTransformerConfigs { get; private set; }
        public Features<IRuntimeFeature> Features => _features ??= new();
        public void Seal(IGlobalConfiguration configuration, TypeMap thisMap)
        {
            // The lists are read by index, not with an enumerator. Sealing a map can configure another one before its turn
            // (MapperConfiguration.ConfigureMapsToSeal), and configuring a base map adds it to the inherited maps of its
            // derived maps (ProfileMap.ApplyDerivedMaps), also of one that is being sealed here. What is added is sealed and
            // applied like the maps that were there from the start.
            if (InheritedTypeMaps != null)
            {
                for (var index = 0; index < InheritedTypeMaps.Count; index++)
                {
                    var inheritedTypeMap = InheritedTypeMaps[index];
                    inheritedTypeMap.Seal(configuration);
                    var includedMaps = inheritedTypeMap._details?.IncludedMembersTypeMaps;
                    if (includedMaps != null)
                    {
                        IncludedMembersTypeMaps ??= [];
                        IncludedMembersTypeMaps.TryAdd(includedMaps);
                    }
                }
            }
            if (IncludedMembersTypeMaps != null)
            {
                for (var index = 0; index < IncludedMembersTypeMaps.Count; index++)
                {
                    var includedMemberTypeMap = IncludedMembersTypeMaps[index];
                    includedMemberTypeMap.TypeMap.Seal(configuration);
                    ApplyIncludedMemberTypeMap(includedMemberTypeMap, thisMap);
                }
            }
            if (InheritedTypeMaps != null)
            {
                for (var index = 0; index < InheritedTypeMaps.Count; index++)
                {
                    var inheritedTypeMap = InheritedTypeMaps[index];
                    // Sealed in the first loop, unless it was added after that one.
                    inheritedTypeMap.Seal(configuration);
                    ApplyInheritedTypeMap(inheritedTypeMap, thisMap);
                }
                if (MaxDepth == 0)
                {
                    InheritMaxDepth();
                }
            }
            _features?.Seal(configuration);
        }
        // A map that sets no MaxDepth takes the one of the map it inherits from.
        void InheritMaxDepth()
        {
            var maxDepth = InheritedMaxDepth(InheritedTypeMaps);
            if (maxDepth > 0)
            {
                MaxDepth = maxDepth;
                MaxDepthInherited = true;
            }
        }
        // When several of the inherited maps have a MaxDepth, the order they were included or registered in must not
        // decide. A more derived map beats a less derived one. Of the maps that are left, which are not related to each
        // other, one that set its MaxDepth itself beats one that inherited it, and after that the smallest MaxDepth wins.
        static int InheritedMaxDepth(List<TypeMap> inheritedTypeMaps)
        {
            var maxDepth = 0;
            var maxDepthInherited = false;
            foreach (var inheritedTypeMap in inheritedTypeMaps)
            {
                var candidate = MaxDepthOf(inheritedTypeMap, out var inherited);
                if (candidate == 0 || HasMoreDerivedWithMaxDepth(inheritedTypeMaps, inheritedTypeMap))
                {
                    continue;
                }
                if (maxDepth == 0 || (inherited == maxDepthInherited ? candidate < maxDepth : maxDepthInherited))
                {
                    maxDepth = candidate;
                    maxDepthInherited = inherited;
                }
            }
            return maxDepth;
        }
        // An inherited map may not have its own inherited MaxDepth yet: a map whose sealing is in progress further up
        // the stack is not sealed again, and which map that is depends on the order the maps were registered in.
        // So an inherited map without a MaxDepth is looked through, to the maps it inherits from.
        static int MaxDepthOf(TypeMap typeMap, out bool inherited)
        {
            var details = typeMap._details;
            if (details is { MaxDepth: > 0 })
            {
                inherited = details.MaxDepthInherited;
                return details.MaxDepth;
            }
            inherited = true;
            return details?.InheritedTypeMaps == null ? 0 : InheritedMaxDepth(details.InheritedTypeMaps);
        }
        static bool HasMoreDerivedWithMaxDepth(List<TypeMap> inheritedTypeMaps, TypeMap typeMap)
        {
            foreach (var other in inheritedTypeMaps)
            {
                if (other != typeMap && IsDerivedFrom(other, typeMap) && !IsDerivedFrom(typeMap, other) && MaxDepthOf(other, out _) > 0)
                {
                    return true;
                }
            }
            return false;
        }
        static bool IsDerivedFrom(TypeMap derivedMap, TypeMap baseMap) =>
            IsDerivedFrom(derivedMap.SourceType, baseMap.SourceType) && IsDerivedFrom(derivedMap.DestinationType, baseMap.DestinationType);
        // Type.IsAssignableFrom says no to a generic type definition, which is what an open generic base map has for its types.
        static bool IsDerivedFrom(Type derivedType, Type baseType)
        {
            if (baseType.IsAssignableFrom(derivedType))
            {
                return true;
            }
            if (!baseType.IsGenericTypeDefinition)
            {
                return false;
            }
            if (baseType.IsInterface)
            {
                return derivedType.GetGenericInterface(baseType) != null;
            }
            for (var type = derivedType; type != null; type = type.BaseType)
            {
                if (type.IsGenericType(baseType))
                {
                    return true;
                }
            }
            return false;
        }
        public void IncludeDerivedTypes(TypePair derivedTypes)
        {
            IncludedDerivedTypes ??= [];
            IncludedDerivedTypes.TryAdd(derivedTypes);
        }
        public void AddBeforeMapAction(LambdaExpression beforeMap)
        {
            BeforeMapActions ??= [];
            BeforeMapActions.TryAdd(beforeMap);
        }
        public void AddAfterMapAction(LambdaExpression afterMap)
        {
            AfterMapActions ??= [];
            AfterMapActions.TryAdd(afterMap);
        }
        public void AddValueTransformation(ValueTransformerConfiguration valueTransformerConfiguration)
        {
            ValueTransformerConfigs ??= [];
            ValueTransformerConfigs.Add(valueTransformerConfiguration);
        }
        public PathMap FindOrCreatePathMapFor(LambdaExpression destinationExpression, MemberPath path, TypeMap typeMap)
        {
            PathMaps ??= [];
            var pathMap = GetPathMap(path);
            if (pathMap == null)
            {
                pathMap = new(destinationExpression, path, typeMap);
                AddPathMap(pathMap);
            }
            return pathMap;
        }
        private PathMap GetPathMap(MemberPath memberPath)
        {
            if (PathMaps == null)
            {
                return null;
            }
            foreach (var pathMap in PathMaps)
            {
                if (pathMap.MemberPath == memberPath)
                {
                    return pathMap;
                }
            }
            return null;
        }
        private void AddPathMap(PathMap pathMap) => PathMaps.Add(pathMap);
        public void IncludeBaseTypes(TypePair baseTypes)
        {
            IncludedBaseTypes ??= [];
            IncludedBaseTypes.TryAdd(baseTypes);
        }
        internal void CopyInheritedMapsTo(TypeMap typeMap)
        {
            typeMap.Details.InheritedTypeMaps ??= [];
            typeMap._details.InheritedTypeMaps.TryAdd(InheritedTypeMaps);
        }
        public bool AddMemberMap(IncludedMember includedMember)
        {
            IncludedMembersTypeMaps ??= [];
            return IncludedMembersTypeMaps.TryAdd(includedMember);
        }
        public SourceMemberConfig FindOrCreateSourceMemberConfigFor(MemberInfo sourceMember)
        {
            SourceMemberConfigs ??= [];
            var config = GetSourceMemberConfig(sourceMember);
            if (config == null)
            {
                config = new(sourceMember);
                SourceMemberConfigs.Add(config);
            }
            return config;
        }
        private SourceMemberConfig GetSourceMemberConfig(MemberInfo sourceMember)
        {
            foreach (var sourceConfig in SourceMemberConfigs)
            {
                if (sourceConfig.SourceMember == sourceMember)
                {
                    return sourceConfig;
                }
            }
            return null;
        }
        public void AddInheritedMap(TypeMap inheritedTypeMap)
        {
            InheritedTypeMaps ??= [];
            InheritedTypeMaps.TryAdd(inheritedTypeMap);
        }
        private void ApplyIncludedMemberTypeMap(IncludedMember includedMember, TypeMap thisMap)
        {
            var typeMap = includedMember.TypeMap;
            var includedMemberMaps = typeMap.PropertyMaps.
                Where(m => m.CanResolveValue && thisMap.GetPropertyMap(m) == null)
                .Select(p => new PropertyMap(p, thisMap, includedMember))
                .ToArray();
            var notOverridenPathMaps = NotOverridenPathMaps(typeMap);
            var appliedConstructorMap = thisMap.ConstructorMap?.ApplyMap(typeMap, includedMember);
            if (includedMemberMaps.Length == 0 && notOverridenPathMaps.Length == 0 && appliedConstructorMap is not true)
            {
                return;
            }
            foreach (var includedMemberMap in includedMemberMaps)
            {
                thisMap.AddPropertyMap(includedMemberMap);
                foreach (var transformer in typeMap.ValueTransformers)
                {
                    includedMemberMap.AddValueTransformation(transformer);
                }
            }
            var details = typeMap._details;
            if (details != null)
            {
                ApplyInheritedMapActions(details.BeforeMapActions?.Select(includedMember.Chain), details.AfterMapActions?.Select(includedMember.Chain));
            }
            foreach (var notOverridenPathMap in notOverridenPathMaps)
            {
                AddPathMap(new(notOverridenPathMap, thisMap, includedMember));
            }
        }
        private void ApplyInheritedTypeMap(TypeMap inheritedTypeMap, TypeMap thisMap)
        {
            if (inheritedTypeMap._propertyMaps != null)
            {
                ApplyInheritedPropertyMaps(inheritedTypeMap, thisMap);
            }
            thisMap.ConstructorMap?.ApplyMap(inheritedTypeMap);
            var inheritedDetails = inheritedTypeMap._details;
            if (inheritedDetails == null)
            {
                return;
            }
            ApplyInheritedMapActions(inheritedDetails.BeforeMapActions, inheritedDetails.AfterMapActions);
            if (inheritedDetails.SourceMemberConfigs != null)
            {
                ApplyInheritedSourceMembers(inheritedTypeMap._details);
            }
            var notOverridenPathMaps = NotOverridenPathMaps(inheritedTypeMap);
            foreach (var notOverridenPathMap in notOverridenPathMaps)
            {
                AddPathMap(notOverridenPathMap);
            }
            if (inheritedDetails.ValueTransformerConfigs != null)
            {
                ValueTransformerConfigs ??= [];
                ValueTransformerConfigs.InsertRange(0, inheritedDetails.ValueTransformerConfigs);
            }
            return;
            void ApplyInheritedPropertyMaps(TypeMap inheritedTypeMap, TypeMap thisMap)
            {
                foreach (var inheritedMappedProperty in inheritedTypeMap._propertyMaps)
                {
                    if (!inheritedMappedProperty.IsMapped)
                    {
                        continue;
                    }
                    var conventionPropertyMap = thisMap.GetPropertyMap(inheritedMappedProperty);
                    if (conventionPropertyMap != null)
                    {
                        conventionPropertyMap.ApplyInheritedPropertyMap(inheritedMappedProperty);
                    }
                    else
                    {
                        thisMap.AddPropertyMap(new(inheritedMappedProperty, thisMap));
                    }
                }
            }
            void ApplyInheritedSourceMembers(TypeMapDetails inheritedTypeMap)
            {
                SourceMemberConfigs ??= [];
                foreach (var inheritedSourceConfig in inheritedTypeMap.SourceMemberConfigs)
                {
                    if (GetSourceMemberConfig(inheritedSourceConfig.SourceMember) == null)
                    {
                        SourceMemberConfigs.Add(inheritedSourceConfig);
                    }
                }
            }
        }
        void ApplyInheritedMapActions(IEnumerable<LambdaExpression> beforeMap, IEnumerable<LambdaExpression> afterMap)
        {
            if (beforeMap != null)
            {
                BeforeMapActions ??= [];
                BeforeMapActions.TryAdd(beforeMap);
            }
            if (afterMap != null)
            {
                AfterMapActions ??= [];
                AfterMapActions.TryAdd(afterMap);
            }
        }
        private PathMap[] NotOverridenPathMaps(TypeMap inheritedTypeMap)
        {
            if (inheritedTypeMap.PathMaps.Count == 0)
            {
                return [];
            }
            PathMaps ??= [];
            return inheritedTypeMap.PathMaps.Where(baseConfig => GetPathMap(baseConfig.MemberPath) == null).ToArray();
        }
    }
}