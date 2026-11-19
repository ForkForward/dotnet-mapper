namespace ForkForward.Mapper.IntegrationTests.BuiltInTypes;

// A destination member that is a set interface is created as a HashSet<T>; before, building the projection failed.
// The unit tests show the expression. This shows that Entity Framework Core translates it, for values and for
// mapped elements.
public class ProjectEnumerableToISet : IntegrationTest<ProjectEnumerableToISet.DatabaseInitializer>
{
    public class Customer
    {
        [Key]
        public int Id { get; set; }
        public string FirstName { get; set; }
        public List<Item> Items { get; set; }
    }

    public class Item
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class ItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class CustomerViewModel
    {
        public string FirstName { get; set; }
        public ISet<int> ItemsIds { get; set; }
        public IReadOnlySet<ItemViewModel> Items { get; set; }
    }

    public class Context : LocalDbContext
    {
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Item> Items { get; set; }
    }

    public class DatabaseInitializer : DropCreateDatabaseAlways<Context>
    {
        protected override void Seed(Context context)
        {
            context.Customers.Add(new Customer
            {
                FirstName = "Bob",
                Items = [new Item { Name = "one" }, new Item { Name = "two" }, new Item { Name = "three" }]
            });

            base.Seed(context);
        }
    }

    protected override MapperConfiguration CreateConfiguration() => new(cfg =>
    {
        cfg.CreateProjection<Customer, CustomerViewModel>().ForMember(d => d.ItemsIds, o => o.MapFrom(s => s.Items.Select(i => i.Id)));
        cfg.CreateProjection<Item, ItemViewModel>();
    });

    [Fact]
    public void Can_map_with_projection()
    {
        using (var context = new Context())
        {
            var customer = ProjectTo<CustomerViewModel>(context.Customers).Single();
            customer.FirstName.ShouldBe("Bob");
            customer.ItemsIds.ShouldBeOfType<HashSet<int>>().ShouldBe(new[] { 1, 2, 3 }, ignoreOrder: true);
            customer.Items.ShouldBeOfType<HashSet<ItemViewModel>>().Select(item => item.Name).ShouldBe(new[] { "one", "two", "three" }, ignoreOrder: true);
        }
    }
}
