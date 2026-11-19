namespace ForkForward.Mapper.UnitTests.Projection;

public abstract class PolymorphicProjectionSpec
{
    protected class Animal
    {
        public string Name { get; set; }
    }
    protected class Cat : Animal
    {
        public bool Meows { get; set; }
    }
    protected class Kitten : Cat
    {
        public int Weeks { get; set; }
    }
    protected class AnimalDto
    {
        public string Name { get; set; }
    }
    protected class CatDto : AnimalDto
    {
        public bool Meows { get; set; }
    }
    protected class KittenDto : CatDto
    {
        public int Weeks { get; set; }
    }
    protected static IQueryable<Animal> Animals => new Animal[] { new Cat { Name = "Tom", Meows = true }, new Animal { Name = "Rex" } }.AsQueryable();
    protected static MapperConfiguration Build(Action<IMapperConfigurationExpression> configure)
    {
        MapperConfiguration configuration = new(configure);
        configuration.AssertConfigurationIsValid();
        return configuration;
    }
    // Today's projection: (animal is Cat) ? new CatDto { Meows = (animal as Cat).Meows, Name = (animal as Cat).Name } : new AnimalDto { Name = animal.Name }
    protected static void ShouldCreateDerivedTypes(IQueryable<AnimalDto> query)
    {
        var shape = ProjectionShape.Of(query);
        shape.TypeTests.ShouldHaveSingleItem().TypeOperand.ShouldBe(typeof(Cat));
        shape.Casts.ShouldNotBeEmpty();
        shape.Casts.ShouldAllBe(cast => cast.NodeType == ExpressionType.TypeAs && cast.Type == typeof(Cat));
        shape.Created.ShouldBe(new[] { typeof(CatDto), typeof(AnimalDto) }, ignoreOrder: true);
        shape.ReadFrom.ShouldBe(new[] { typeof(Cat), typeof(Animal) }, ignoreOrder: true);
        var results = query.ToArray();
        results[0].ShouldBeOfType<CatDto>().Meows.ShouldBeTrue();
        results[1].ShouldBeOfType<AnimalDto>();
        results.Select(result => result.Name).ShouldBe(new[] { "Tom", "Rex" });
    }
    // Off: new AnimalDto { Name = animal.Name }
    protected static void ShouldUseTheMapAlone(IQueryable<AnimalDto> query)
    {
        var shape = ProjectionShape.Of(query);
        shape.TypeTests.ShouldBeEmpty();
        shape.Casts.ShouldBeEmpty();
        shape.Conditions.ShouldBeEmpty();
        shape.Created.ShouldBe(new[] { typeof(AnimalDto) });
        shape.ReadFrom.ShouldBe(new[] { typeof(Animal) });
        var results = query.ToArray();
        results.ShouldAllBe(result => result.GetType() == typeof(AnimalDto));
        results.Select(result => result.Name).ShouldBe(new[] { "Tom", "Rex" });
    }
    protected sealed class ProjectionShape : ExpressionVisitor
    {
        public List<TypeBinaryExpression> TypeTests { get; } = [];
        public List<UnaryExpression> Casts { get; } = [];
        public List<ConditionalExpression> Conditions { get; } = [];
        public HashSet<Type> Created { get; } = [];
        public HashSet<Type> ReadFrom { get; } = [];
        public static ProjectionShape Of(IQueryable query)
        {
            ProjectionShape shape = new();
            shape.Visit(query.Expression);
            return shape;
        }
        protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        {
            TypeTests.Add(node);
            return base.VisitTypeBinary(node);
        }
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if(node.NodeType is ExpressionType.TypeAs or ExpressionType.Convert or ExpressionType.ConvertChecked)
            {
                Casts.Add(node);
            }
            return base.VisitUnary(node);
        }
        protected override Expression VisitConditional(ConditionalExpression node)
        {
            Conditions.Add(node);
            return base.VisitConditional(node);
        }
        protected override Expression VisitNew(NewExpression node)
        {
            Created.Add(node.Type);
            return base.VisitNew(node);
        }
        protected override Expression VisitMember(MemberExpression node)
        {
            ReadFrom.Add(node.Member.DeclaringType);
            return base.VisitMember(node);
        }
    }
}
public class PolymorphicProjectionSettings : PolymorphicProjectionSpec
{
    static MapperConfiguration Hierarchy(bool? global, bool? map) => Build(cfg =>
    {
        if(global is bool forAllMaps)
        {
            cfg.Internal().PolymorphicProjection = forAllMaps;
        }
        var animals = cfg.CreateMap<Animal, AnimalDto>().Include<Cat, CatDto>();
        if(map is bool forThisMap)
        {
            animals.PolymorphicProjection(forThisMap);
        }
        cfg.CreateMap<Cat, CatDto>();
    });
    [Fact]
    public void Should_be_on_by_default()
    {
        var configuration = Hierarchy(global: null, map: null);
        configuration.Internal().PolymorphicProjection.ShouldBeTrue();
        configuration.FindTypeMapFor<Animal, AnimalDto>().PolymorphicProjection.ShouldBeNull();
        ShouldCreateDerivedTypes(Animals.ProjectTo<AnimalDto>(configuration));
    }
    [Fact]
    public void Should_be_off_for_a_map_that_switches_it_off()
    {
        var configuration = Hierarchy(global: null, map: false);
        configuration.Internal().PolymorphicProjection.ShouldBeTrue();
        configuration.FindTypeMapFor<Animal, AnimalDto>().PolymorphicProjection.ShouldBe(false);
        ShouldUseTheMapAlone(Animals.ProjectTo<AnimalDto>(configuration));
        ShouldUseTheMapAlone(Animals.ProjectTo<AnimalDto>(Hierarchy(global: true, map: false)));
    }
    [Fact]
    public void Should_be_off_for_a_map_without_its_own_setting_when_off_globally()
    {
        var configuration = Hierarchy(global: false, map: null);
        configuration.Internal().PolymorphicProjection.ShouldBeFalse();
        configuration.FindTypeMapFor<Animal, AnimalDto>().PolymorphicProjection.ShouldBeNull();
        ShouldUseTheMapAlone(Animals.ProjectTo<AnimalDto>(configuration));
    }
    [Fact]
    public void Should_stay_on_for_a_map_that_switches_it_on_when_off_globally() =>
        ShouldCreateDerivedTypes(Animals.ProjectTo<AnimalDto>(Hierarchy(global: false, map: true)));
    [Fact]
    public void Should_be_the_same_through_the_mapper()
    {
        ShouldCreateDerivedTypes(Hierarchy(global: null, map: null).CreateMapper().ProjectTo<AnimalDto>(Animals));
        ShouldUseTheMapAlone(Hierarchy(global: null, map: false).CreateMapper().ProjectTo<AnimalDto>(Animals));
    }
    [Fact]
    public void Should_not_change_Map()
    {
        foreach(var configuration in new[] { Hierarchy(global: null, map: false), Hierarchy(global: false, map: null) })
        {
            var mapper = configuration.CreateMapper();
            Animal cat = new Cat { Name = "Tom", Meows = true };
            mapper.Map<AnimalDto>(cat).ShouldBeOfType<CatDto>().Meows.ShouldBeTrue();
            mapper.Map<Animal, AnimalDto>(cat).ShouldBeOfType<CatDto>().Meows.ShouldBeTrue();
            var all = mapper.Map<List<AnimalDto>>(Animals.ToList());
            all[0].ShouldBeOfType<CatDto>();
            all[1].ShouldBeOfType<AnimalDto>();
        }
    }
    [Fact]
    public void Should_use_the_derived_map_for_a_query_over_the_derived_type()
    {
        // Which map a query uses depends on its element type, not on this setting: no type test is needed for that.
        var query = new[] { new Cat { Name = "Tom", Meows = true } }.AsQueryable().ProjectTo<AnimalDto>(Hierarchy(global: null, map: false));
        var shape = ProjectionShape.Of(query);
        shape.TypeTests.ShouldBeEmpty();
        shape.Casts.ShouldBeEmpty();
        query.Single().ShouldBeOfType<CatDto>().Meows.ShouldBeTrue();
    }
    [Fact]
    public void Should_keep_the_setting_the_configuration_was_built_with()
    {
        // Projections are cached per configuration, and the setting is not part of the key.
        MapperConfigurationExpression expression = new();
        expression.CreateMap<Animal, AnimalDto>().Include<Cat, CatDto>();
        expression.CreateMap<Cat, CatDto>();
        MapperConfiguration configuration = new(expression);
        expression.Internal().PolymorphicProjection = false;
        configuration.Internal().PolymorphicProjection.ShouldBeTrue();
        ShouldCreateDerivedTypes(Animals.ProjectTo<AnimalDto>(configuration));
    }
}
public class PolymorphicProjectionOfMembers : PolymorphicProjectionSpec
{
    class Owner
    {
        public Animal Pet { get; set; }
        public List<Animal> Herd { get; set; }
    }
    class PetDto
    {
        public AnimalDto Pet { get; set; }
    }
    class HerdDto
    {
        public List<AnimalDto> Herd { get; set; }
    }
    static IQueryable<Owner> Owners => new[] { new Owner { Pet = new Cat { Name = "Tom", Meows = true }, Herd = [.. Animals] } }.AsQueryable();
    static MapperConfiguration Hierarchy(bool? global = null, bool? owner = null, bool? animal = null) => Build(cfg =>
    {
        if(global is bool forAllMaps)
        {
            cfg.Internal().PolymorphicProjection = forAllMaps;
        }
        var pets = cfg.CreateMap<Owner, PetDto>();
        var herds = cfg.CreateMap<Owner, HerdDto>();
        if(owner is bool forOwners)
        {
            pets.PolymorphicProjection(forOwners);
            herds.PolymorphicProjection(forOwners);
        }
        var animals = cfg.CreateMap<Animal, AnimalDto>().Include<Cat, CatDto>();
        if(animal is bool forAnimals)
        {
            animals.PolymorphicProjection(forAnimals);
        }
        cfg.CreateMap<Cat, CatDto>();
    });
    static void ShouldNotLookAtTheType(IQueryable query, Type destinationType)
    {
        var shape = ProjectionShape.Of(query);
        shape.TypeTests.ShouldBeEmpty();
        shape.Casts.ShouldBeEmpty();
        shape.Created.ShouldBe(new[] { destinationType, typeof(AnimalDto) }, ignoreOrder: true);
        shape.ReadFrom.ShouldBe(new[] { typeof(Owner), typeof(Animal) }, ignoreOrder: true);
    }
    static void ShouldLookAtTheType(IQueryable query, Type destinationType)
    {
        var shape = ProjectionShape.Of(query);
        shape.TypeTests.ShouldHaveSingleItem().TypeOperand.ShouldBe(typeof(Cat));
        shape.Created.ShouldBe(new[] { destinationType, typeof(CatDto), typeof(AnimalDto) }, ignoreOrder: true);
    }
    [Fact]
    public void Should_project_a_nested_member_with_its_map_alone()
    {
        var query = Owners.ProjectTo<PetDto>(Hierarchy(animal: false));
        ShouldNotLookAtTheType(query, typeof(PetDto));
        query.Single().Pet.ShouldBeOfType<AnimalDto>().Name.ShouldBe("Tom");
    }
    [Fact]
    public void Should_project_the_elements_of_a_collection_member_with_their_map_alone()
    {
        var query = Owners.ProjectTo<HerdDto>(Hierarchy(animal: false));
        ShouldNotLookAtTheType(query, typeof(HerdDto));
        var herd = query.Single().Herd;
        herd.ShouldAllBe(animal => animal.GetType() == typeof(AnimalDto));
        herd.Select(animal => animal.Name).ShouldBe(new[] { "Tom", "Rex" });
    }
    [Fact]
    public void Should_follow_the_global_setting_for_a_member_without_its_own()
    {
        var configuration = Hierarchy(global: false);
        var pets = Owners.ProjectTo<PetDto>(configuration);
        ShouldNotLookAtTheType(pets, typeof(PetDto));
        pets.Single().Pet.ShouldBeOfType<AnimalDto>();
        var herds = Owners.ProjectTo<HerdDto>(configuration);
        ShouldNotLookAtTheType(herds, typeof(HerdDto));
        herds.Single().Herd.ShouldAllBe(animal => animal.GetType() == typeof(AnimalDto));
    }
    [Fact]
    public void Should_follow_the_map_of_the_member_when_off_globally()
    {
        var configuration = Hierarchy(global: false, animal: true);
        var pets = Owners.ProjectTo<PetDto>(configuration);
        ShouldLookAtTheType(pets, typeof(PetDto));
        pets.Single().Pet.ShouldBeOfType<CatDto>().Meows.ShouldBeTrue();
        var herds = Owners.ProjectTo<HerdDto>(configuration);
        ShouldLookAtTheType(herds, typeof(HerdDto));
        var herd = herds.Single().Herd;
        herd[0].ShouldBeOfType<CatDto>().Meows.ShouldBeTrue();
        herd[1].ShouldBeOfType<AnimalDto>();
    }
    [Fact]
    public void Should_follow_the_map_of_the_member_not_the_map_that_contains_it()
    {
        var configuration = Hierarchy(owner: false);
        var pets = Owners.ProjectTo<PetDto>(configuration);
        ShouldLookAtTheType(pets, typeof(PetDto));
        pets.Single().Pet.ShouldBeOfType<CatDto>();
        var herds = Owners.ProjectTo<HerdDto>(configuration);
        ShouldLookAtTheType(herds, typeof(HerdDto));
        herds.Single().Herd[0].ShouldBeOfType<CatDto>();
        ShouldNotLookAtTheType(Owners.ProjectTo<PetDto>(Hierarchy(owner: true, animal: false)), typeof(PetDto));
    }
    [Fact]
    public void Should_not_change_Map()
    {
        var mapper = Hierarchy(global: false, animal: false).CreateMapper();
        var owner = Owners.Single();
        mapper.Map<PetDto>(owner).Pet.ShouldBeOfType<CatDto>().Meows.ShouldBeTrue();
        var herd = mapper.Map<HerdDto>(owner).Herd;
        herd[0].ShouldBeOfType<CatDto>();
        herd[1].ShouldBeOfType<AnimalDto>();
    }
}
public class PolymorphicProjectionToAbstractType : PolymorphicProjectionSpec
{
    abstract class PetDto
    {
        public string Name { get; set; }
    }
    class PetCatDto : PetDto
    {
        public bool Meows { get; set; }
    }
    class StrayDto : PetDto, IPetDto
    {
    }
    interface IPetDto
    {
        string Name { get; set; }
    }
    class Owner
    {
        public Animal Pet { get; set; }
    }
    class OwnerDto
    {
        public PetDto Pet { get; set; }
    }
    static MapperConfiguration Hierarchy(bool? polymorphic, Action<IMappingExpression<Animal, PetDto>> configure = null) => Build(cfg =>
    {
        var pets = cfg.CreateMap<Animal, PetDto>().Include<Cat, PetCatDto>();
        if(polymorphic is bool forThisMap)
        {
            pets.PolymorphicProjection(forThisMap);
        }
        configure?.Invoke(pets);
        cfg.CreateMap<Cat, PetCatDto>();
        cfg.CreateMap<Owner, OwnerDto>();
    });
    static void ShouldSayWhy(AutoMapperMappingException exception, TypeMap typeMap)
    {
        exception.TypeMap.ShouldBe(typeMap);
        exception.Types.ShouldBe(typeMap.Types);
        exception.Message.ShouldStartWith(
            $"ProjectTo cannot create {typeMap.DestinationType}: it is abstract, and polymorphic projection is switched off for the map from {typeMap.SourceType}, so no derived type is created in its place.");
        Assert.Contains("PolymorphicProjection(true)", exception.Message);
    }
    [Fact]
    public void Should_behave_as_before_when_on()
    {
        var query = Animals.ProjectTo<PetDto>(Hierarchy(polymorphic: null));
        ProjectionShape.Of(query).TypeTests.ShouldHaveSingleItem().TypeOperand.ShouldBe(typeof(Cat));
        var results = query.ToArray();
        results[0].ShouldBeOfType<PetCatDto>().Name.ShouldBe("Tom");
        results[1].ShouldBeNull();
    }
    [Fact]
    public void Should_fail_when_the_projection_is_built()
    {
        var configuration = Hierarchy(polymorphic: false);
        var exception = Should.Throw<AutoMapperMappingException>(() => Animals.ProjectTo<PetDto>(configuration));
        ShouldSayWhy(exception, configuration.FindTypeMapFor<Animal, PetDto>());
    }
    [Fact]
    public void Should_fail_for_a_member_whose_map_has_an_abstract_destination()
    {
        var configuration = Hierarchy(polymorphic: false);
        var owners = new[] { new Owner { Pet = new Cat { Name = "Tom" } } }.AsQueryable();
        var exception = Should.Throw<AutoMapperMappingException>(() => owners.ProjectTo<OwnerDto>(configuration));
        ShouldSayWhy(exception, configuration.FindTypeMapFor<Animal, PetDto>());
    }
    [Fact]
    public void Should_fail_when_off_globally()
    {
        var configuration = Build(cfg =>
        {
            cfg.Internal().PolymorphicProjection = false;
            cfg.CreateProjection<Animal, IPetDto>();
            cfg.CreateMap<Animal, PetDto>().Include<Cat, PetCatDto>();
            cfg.CreateMap<Cat, PetCatDto>();
        });
        ShouldSayWhy(Should.Throw<AutoMapperMappingException>(() => Animals.ProjectTo<IPetDto>(configuration)), configuration.FindTypeMapFor<Animal, IPetDto>());
        ShouldSayWhy(Should.Throw<AutoMapperMappingException>(() => Animals.ProjectTo<PetDto>(configuration)), configuration.FindTypeMapFor<Animal, PetDto>());
    }
    [Fact]
    public void Should_create_what_ConstructUsing_creates()
    {
        var query = Animals.ProjectTo<PetDto>(Hierarchy(polymorphic: false, pets => pets.ConstructUsing(animal => new StrayDto())));
        var shape = ProjectionShape.Of(query);
        shape.TypeTests.ShouldBeEmpty();
        shape.Casts.ShouldBeEmpty();
        shape.Created.ShouldBe(new[] { typeof(StrayDto) });
        var results = query.ToArray();
        results.ShouldAllBe(result => result.GetType() == typeof(StrayDto));
        results.Select(result => result.Name).ShouldBe(new[] { "Tom", "Rex" });
    }
    [Fact]
    public void Should_create_what_ConvertUsing_creates()
    {
        var query = Animals.ProjectTo<PetDto>(Hierarchy(polymorphic: false, pets => pets.ConvertUsing(animal => new StrayDto { Name = animal.Name + "!" })));
        var shape = ProjectionShape.Of(query);
        shape.TypeTests.ShouldBeEmpty();
        shape.Created.ShouldBe(new[] { typeof(StrayDto) });
        var results = query.ToArray();
        results.ShouldAllBe(result => result.GetType() == typeof(StrayDto));
        results.Select(result => result.Name).ShouldBe(new[] { "Tom!", "Rex!" });
    }
    [Fact]
    public void Should_create_what_As_names()
    {
        var configuration = Build(cfg =>
        {
            cfg.Internal().PolymorphicProjection = false;
            cfg.CreateMap<Animal, IPetDto>().As<StrayDto>();
            cfg.CreateMap<Animal, StrayDto>();
        });
        var results = Animals.ProjectTo<IPetDto>(configuration).ToArray();
        results.ShouldAllBe(result => result.GetType() == typeof(StrayDto));
        results.Select(result => result.Name).ShouldBe(new[] { "Tom", "Rex" });
    }
}
public class PolymorphicProjectionRoutes : PolymorphicProjectionSpec
{
    static void ShouldFollowTheSetting(Func<bool, MapperConfiguration> hierarchy)
    {
        ShouldCreateDerivedTypes(Animals.ProjectTo<AnimalDto>(hierarchy(true)));
        ShouldUseTheMapAlone(Animals.ProjectTo<AnimalDto>(hierarchy(false)));
    }
    [Fact]
    public void Should_work_with_Include() => ShouldFollowTheSetting(polymorphic => Build(cfg =>
    {
        cfg.CreateMap<Animal, AnimalDto>().PolymorphicProjection(polymorphic).Include<Cat, CatDto>();
        cfg.CreateMap<Cat, CatDto>();
    }));
    [Fact]
    public void Should_work_with_IncludeBase() => ShouldFollowTheSetting(polymorphic => Build(cfg =>
    {
        cfg.CreateMap<Animal, AnimalDto>().PolymorphicProjection(polymorphic);
        cfg.CreateMap<Cat, CatDto>().IncludeBase<Animal, AnimalDto>();
    }));
    [Fact]
    public void Should_work_with_IncludeAllDerived() => ShouldFollowTheSetting(polymorphic => Build(cfg =>
    {
        cfg.CreateMap<Animal, AnimalDto>().IncludeAllDerived().PolymorphicProjection(polymorphic);
        cfg.CreateMap<Cat, CatDto>();
    }));
    [Fact]
    public void Should_work_with_CreateProjection() => ShouldFollowTheSetting(polymorphic => Build(cfg =>
    {
        IProjectionExpression<Animal, AnimalDto> animals = cfg.CreateProjection<Animal, AnimalDto>().PolymorphicProjection(polymorphic);
        animals.Advanced().Include<Cat, CatDto>();
        cfg.CreateProjection<Cat, CatDto>();
    }));
    [Fact]
    public void Should_work_with_maps_created_from_types() => ShouldFollowTheSetting(polymorphic => Build(cfg =>
    {
        IMappingExpression animals = cfg.CreateMap(typeof(Animal), typeof(AnimalDto)).PolymorphicProjection(polymorphic);
        animals.Include(typeof(Cat), typeof(CatDto));
        cfg.CreateMap(typeof(Cat), typeof(CatDto));
    }));
    [Fact]
    public void Should_work_with_ForAllMaps() => ShouldFollowTheSetting(polymorphic => Build(cfg =>
    {
        cfg.Internal().PolymorphicProjection = !polymorphic;
        cfg.ForAllMaps((typeMap, map) => map.PolymorphicProjection(polymorphic));
        cfg.CreateMap<Animal, AnimalDto>().Include<Cat, CatDto>();
        cfg.CreateMap<Cat, CatDto>();
    }));
}
public class PolymorphicProjectionIsNotInherited : PolymorphicProjectionSpec
{
    static IQueryable<Cat> Cats => new[] { new Kitten { Name = "Kit", Weeks = 6 }, new Cat { Name = "Tom" } }.AsQueryable();
    static MapperConfiguration Hierarchy(bool? animal, bool? cat) => Build(cfg =>
    {
        var animals = cfg.CreateMap<Animal, AnimalDto>().Include<Cat, CatDto>();
        if(animal is bool forAnimals)
        {
            animals.PolymorphicProjection(forAnimals);
        }
        var cats = cfg.CreateMap<Cat, CatDto>().Include<Kitten, KittenDto>();
        if(cat is bool forCats)
        {
            cats.PolymorphicProjection(forCats);
        }
        cfg.CreateMap<Kitten, KittenDto>();
    });
    [Fact]
    public void A_derived_map_should_not_take_the_setting_of_its_base_map()
    {
        var configuration = Hierarchy(animal: false, cat: null);
        ShouldUseTheMapAlone(Animals.ProjectTo<AnimalDto>(configuration));
        var query = Cats.ProjectTo<CatDto>(configuration);
        ProjectionShape.Of(query).TypeTests.ShouldHaveSingleItem().TypeOperand.ShouldBe(typeof(Kitten));
        var results = query.ToArray();
        results[0].ShouldBeOfType<KittenDto>().Weeks.ShouldBe(6);
        results[1].ShouldBeOfType<CatDto>();
    }
    [Fact]
    public void A_base_map_should_not_take_the_setting_of_a_derived_map()
    {
        var configuration = Hierarchy(animal: null, cat: false);
        ShouldCreateDerivedTypes(Animals.ProjectTo<AnimalDto>(configuration));
        var query = Cats.ProjectTo<CatDto>(configuration);
        var shape = ProjectionShape.Of(query);
        shape.TypeTests.ShouldBeEmpty();
        shape.Casts.ShouldBeEmpty();
        shape.Created.ShouldBe(new[] { typeof(CatDto) });
        var results = query.ToArray();
        results.ShouldAllBe(result => result.GetType() == typeof(CatDto));
        results.Select(result => result.Name).ShouldBe(new[] { "Kit", "Tom" });
    }
    [Fact]
    public void A_reverse_map_should_not_take_the_setting_of_the_map_it_reverses()
    {
        var configuration = Build(cfg =>
        {
            cfg.CreateMap<Animal, AnimalDto>().Include<Cat, CatDto>().PolymorphicProjection(false).ReverseMap();
            cfg.CreateMap<Cat, CatDto>().ReverseMap();
        });
        configuration.FindTypeMapFor<Animal, AnimalDto>().PolymorphicProjection.ShouldBe(false);
        configuration.FindTypeMapFor<AnimalDto, Animal>().PolymorphicProjection.ShouldBeNull();
        ShouldUseTheMapAlone(Animals.ProjectTo<AnimalDto>(configuration));
        var query = new AnimalDto[] { new CatDto { Name = "Tom", Meows = true }, new AnimalDto { Name = "Rex" } }.AsQueryable().ProjectTo<Animal>(configuration);
        ProjectionShape.Of(query).TypeTests.ShouldHaveSingleItem().TypeOperand.ShouldBe(typeof(CatDto));
        var results = query.ToArray();
        results[0].ShouldBeOfType<Cat>().Meows.ShouldBeTrue();
        results[1].ShouldBeOfType<Animal>();
    }
}
