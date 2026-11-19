using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ForkForward.Mapper.Extensions.Microsoft.DependencyInjection.Tests
{
    using System.Linq;

    public class DestinationFactoryTests
    {
        public class Order
        {
            public int Id { get; set; }
        }

        public class OrderDto
        {
            public OrderDto(int createdWith) => CreatedWith = createdWith;

            public int CreatedWith { get; }
            public int Id { get; set; }
        }

        public class Box<T>
        {
            public T Value { get; set; }
        }

        public class BoxDto<T>
        {
            public BoxDto(int createdWith) => CreatedWith = createdWith;

            public int CreatedWith { get; }
            public T Value { get; set; }
        }

        // The factories are found when the assembly is scanned. The maps are not in a profile, so the other
        // tests, which scan this assembly too, see the same maps as before.
        public class OrderDtoFactory : IDestinationFactory<Order, OrderDto>
        {
            private readonly ISomeService _service;

            public OrderDtoFactory(ISomeService service) => _service = service;

            public OrderDto Create(Order source, ResolutionContext context) => new OrderDto(_service.Modify(source.Id));
        }

        public class BoxDtoFactory<T> : IDestinationFactory<Box<T>, BoxDto<T>>
        {
            private readonly ISomeService _service;

            public BoxDtoFactory(ISomeService service) => _service = service;

            public BoxDto<T> Create(Box<T> source, ResolutionContext context) => new BoxDto<T>(_service.Modify(0));
        }

        private static IServiceCollection Services()
        {
            var services = new ServiceCollection();
            services.AddAutoMapper(cfg =>
            {
                cfg.CreateMap<Order, OrderDto>().ConstructUsing<OrderDtoFactory>();
                cfg.CreateMap(typeof(Box<>), typeof(BoxDto<>)).ConstructUsing(typeof(BoxDtoFactory<>));
            }, typeof(DestinationFactoryTests).Assembly);
            return services;
        }

        [Fact]
        public void ShouldRegisterDestinationFactoriesAsTransient()
        {
            var services = Services();

            services.Single(sd => sd.ServiceType == typeof(OrderDtoFactory)).Lifetime.ShouldBe(ServiceLifetime.Transient);
            services.Single(sd => sd.ServiceType == typeof(BoxDtoFactory<>)).Lifetime.ShouldBe(ServiceLifetime.Transient);
        }

        [Fact]
        public void ShouldConstructWithDependency()
        {
            var services = Services();
            services.AddTransient<ISomeService>(sp => new FooService(5));
            var provider = services.BuildServiceProvider();
            provider.GetService<IConfigurationProvider>().AssertConfigurationIsValid();

            var dest = provider.GetService<IMapper>().Map<OrderDto>(new Order { Id = 37 });

            dest.CreatedWith.ShouldBe(42);
            dest.Id.ShouldBe(37);
        }

        [Fact]
        public void ShouldConstructWithScopedDependency()
        {
            var services = Services();
            services.AddScoped<ISomeService, MutableService>();
            var provider = services.BuildServiceProvider();

            using (var firstScope = provider.CreateScope())
            using (var secondScope = provider.CreateScope())
            {
                ((MutableService)firstScope.ServiceProvider.GetService<ISomeService>()).Value = 10;
                ((MutableService)secondScope.ServiceProvider.GetService<ISomeService>()).Value = 20;

                firstScope.ServiceProvider.GetService<IMapper>().Map<OrderDto>(new Order { Id = 1 }).CreatedWith.ShouldBe(11);
                secondScope.ServiceProvider.GetService<IMapper>().Map<OrderDto>(new Order { Id = 1 }).CreatedWith.ShouldBe(21);
            }
        }

        [Fact]
        public void ShouldConstructWithOpenGenericFactory()
        {
            var services = Services();
            services.AddTransient<ISomeService>(sp => new FooService(5));
            var provider = services.BuildServiceProvider();

            var dest = provider.GetService<IMapper>().Map<BoxDto<string>>(new Box<string> { Value = "value" });

            dest.CreatedWith.ShouldBe(5);
            dest.Value.ShouldBe("value");
        }

        [Fact]
        public void ShouldPreferServiceConstructorOfTheCall()
        {
            var services = Services();
            services.AddTransient<ISomeService>(sp => new FooService(5));
            var provider = services.BuildServiceProvider();

            var dest = provider.GetService<IMapper>().Map<Order, OrderDto>(new Order { Id = 1 },
                opt => opt.ConstructServicesUsing(type => new OrderDtoFactory(new FooService(100))));

            dest.CreatedWith.ShouldBe(101);
        }
    }
}
