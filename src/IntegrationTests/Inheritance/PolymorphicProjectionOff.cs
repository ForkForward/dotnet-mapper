namespace ForkForward.Mapper.IntegrationTests.Inheritance;

public class PolymorphicProjectionOff : IntegrationTest<PolymorphicProjectionOff.DatabaseInitializer>
{
    public class Vehicle
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Motorcycle : Vehicle
    {
        public bool HasSidecar { get; set; }
    }

    public class Bicycle : Vehicle
    {
        public bool EBike { get; set; }
    }

    public class VehicleModel
    {
        public string Name { get; set; }
    }

    public class MotorcycleModel : VehicleModel
    {
        public bool HasSidecar { get; set; }
    }

    public class BicycleModel : VehicleModel
    {
        public bool EBike { get; set; }
    }

    public class Context : LocalDbContext
    {
        public DbSet<Vehicle> Vehicles { get; set; }

        public DbSet<Motorcycle> Motorcycles { get; set; }

        public DbSet<Bicycle> Bicycles { get; set; }
    }

    static MapperConfiguration Hierarchy(bool polymorphic) => new(cfg =>
    {
        cfg.CreateMap<Vehicle, VehicleModel>()
            .IncludeAllDerived()
            .PolymorphicProjection(polymorphic);

        cfg.CreateMap<Motorcycle, MotorcycleModel>();
        cfg.CreateMap<Bicycle, BicycleModel>();
    });

    protected override MapperConfiguration CreateConfiguration() => Hierarchy(polymorphic: false);

    public class DatabaseInitializer : DropCreateDatabaseAlways<Context>
    {
        protected override void Seed(Context context)
        {
            context.Vehicles.Add(new Vehicle { Name = "Car" });
            context.Vehicles.Add(new Bicycle { Name = "Bicycle", EBike = true });
            context.Vehicles.Add(new Motorcycle { Name = "Motorcycle", HasSidecar = true });

            base.Seed(context);
        }
    }

    [Fact]
    public void Should_query_only_what_the_base_map_needs()
    {
        using var context = new Context();
        var vehicles = context.Vehicles.OrderBy(v => v.Name);

        // On, the type tests reach the database: the discriminator and the columns of every derived type.
        var polymorphic = vehicles.ProjectTo<VehicleModel>(Hierarchy(polymorphic: true));
        var polymorphicSql = polymorphic.ToQueryString();
        polymorphicSql.ShouldContain("[Discriminator]");
        polymorphicSql.ShouldContain(nameof(Motorcycle.HasSidecar));
        polymorphicSql.ShouldContain(nameof(Bicycle.EBike));
        polymorphic.ToArray().Select(v => v.GetType()).ShouldBe(new[] { typeof(BicycleModel), typeof(VehicleModel), typeof(MotorcycleModel) });

        // Off, the query is the one a hand-written Select of the base members gives.
        var projection = ProjectTo<VehicleModel>(vehicles);
        var sql = projection.ToQueryString();
        sql.ShouldNotContain("Discriminator");
        sql.ShouldNotContain(nameof(Motorcycle.HasSidecar));
        sql.ShouldNotContain(nameof(Bicycle.EBike));
        sql.ShouldBe(vehicles.Select(v => new VehicleModel { Name = v.Name }).ToQueryString());

        var results = projection.ToArray();
        results.ShouldAllBe(v => v.GetType() == typeof(VehicleModel));
        results.Select(v => v.Name).ShouldBe(new[] { "Bicycle", "Car", "Motorcycle" });
    }
}
