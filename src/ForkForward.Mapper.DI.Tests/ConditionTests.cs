using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ForkForward.Mapper.Extensions.Microsoft.DependencyInjection.Tests
{
    public class ConditionSource
    {
        public int Value { get; set; }
    }

    public class ConditionDest
    {
        public int Value { get; set; }
    }

    // Maps the member when the service makes the source value positive.
    internal class DependencyCondition : IMemberCondition<object, object, int>
    {
        private readonly ISomeService _service;

        public DependencyCondition(ISomeService service) => _service = service;

        public bool ShouldMap(object source, object destination, int sourceMember, int destinationMember, ResolutionContext context)
            => _service.Modify(sourceMember) > 0;
    }

    // Maps the member when the service makes zero positive.
    internal class DependencyPreCondition : IMemberPreCondition<ConditionSource, ConditionDest>
    {
        private readonly ISomeService _service;

        public DependencyPreCondition(ISomeService service) => _service = service;

        public bool ShouldMap(ConditionSource source, ConditionDest destination, ResolutionContext context)
            => _service.Modify(0) > 0;
    }

    internal class FooCondition : IMemberCondition<object, object, object>
    {
        public bool ShouldMap(object source, object destination, object sourceMember, object destinationMember, ResolutionContext context)
            => sourceMember != null;
    }

    internal class FooPreCondition : IMemberPreCondition<object, object>
    {
        public bool ShouldMap(object source, object destination, ResolutionContext context) => true;
    }

    public class ConditionTests
    {
        // The maps are added here and not in a profile: other tests count the maps of the profiles in this assembly.
        private static IServiceCollection Services(Action<IMapperConfigurationExpression> createMap)
        {
            var services = new ServiceCollection();
            services.AddAutoMapper(createMap, typeof(Source).Assembly);
            return services;
        }

        private static int Map(IMapper mapper, int value) => mapper.Map(new ConditionSource { Value = value }, new ConditionDest { Value = 7 }).Value;

        [Theory]
        [InlineData(typeof(FooCondition))]
        [InlineData(typeof(FooPreCondition))]
        [InlineData(typeof(DependencyCondition))]
        [InlineData(typeof(DependencyPreCondition))]
        public void ShouldRegisterConditionsAndPreConditionsAsTransient(Type type)
        {
            var services = new ServiceCollection();
            services.AddAutoMapper(typeof(Source).Assembly);

            services.Single(descriptor => descriptor.ServiceType == type).Lifetime.ShouldBe(ServiceLifetime.Transient);
        }

        [Fact]
        public void ShouldResolveConditionAndPreCondition()
        {
            var provider = Services(cfg => { }).BuildServiceProvider();

            provider.GetService<FooCondition>().ShouldNotBeNull();
            provider.GetService<FooPreCondition>().ShouldNotBeNull();
        }

        [Fact]
        public void ShouldCreateConditionWithDependency()
        {
            var services = Services(cfg => cfg.CreateMap<ConditionSource, ConditionDest>().ForMember(d => d.Value, opt => opt.Condition<DependencyCondition>()));
            services.AddTransient<ISomeService>(sp => new FooService(5));
            var mapper = services.BuildServiceProvider().GetService<IMapper>();

            Map(mapper, -3).ShouldBe(-3);
            Map(mapper, -10).ShouldBe(7);
        }

        [Fact]
        public void ShouldCreateConditionGivenAsTypeWithDependency()
        {
            var services = Services(cfg => cfg.CreateMap(typeof(ConditionSource), typeof(ConditionDest)).ForAllMembers(opt => opt.Condition(typeof(DependencyCondition))));
            services.AddTransient<ISomeService>(sp => new FooService(5));
            var mapper = services.BuildServiceProvider().GetService<IMapper>();

            Map(mapper, -3).ShouldBe(-3);
            Map(mapper, -10).ShouldBe(7);
        }

        [Theory]
        [InlineData(5, 3)]
        [InlineData(0, 7)]
        public void ShouldCreatePreConditionWithDependency(int serviceValue, int expected)
        {
            var services = Services(cfg => cfg.CreateMap<ConditionSource, ConditionDest>().ForMember(d => d.Value, opt => opt.PreCondition<DependencyPreCondition>()));
            services.AddTransient<ISomeService>(sp => new FooService(serviceValue));
            var mapper = services.BuildServiceProvider().GetService<IMapper>();

            Map(mapper, 3).ShouldBe(expected);
        }

        [Fact]
        public void Can_depend_on_scoped_services()
        {
            var services = Services(cfg => cfg.CreateMap<ConditionSource, ConditionDest>().ForMember(d => d.Value, opt =>
            {
                opt.PreCondition<DependencyPreCondition>();
                opt.Condition<DependencyCondition>();
            }));
            services.AddScoped<ISomeService, MutableService>();
            var provider = services.BuildServiceProvider();

            using (var scope = provider.CreateScope())
            {
                ((MutableService)scope.ServiceProvider.GetService<ISomeService>()).Value = 10;

                Map(scope.ServiceProvider.GetService<IMapper>(), -3).ShouldBe(-3);
            }
            using (var scope = provider.CreateScope())
            {
                ((MutableService)scope.ServiceProvider.GetService<ISomeService>()).Value = 1;

                Map(scope.ServiceProvider.GetService<IMapper>(), -3).ShouldBe(7);
            }
        }

        [Fact]
        public void Can_override_the_container_for_one_map_call()
        {
            var services = Services(cfg => cfg.CreateMap<ConditionSource, ConditionDest>().ForMember(d => d.Value, opt => opt.Condition<DependencyCondition>()));
            services.AddTransient<ISomeService>(sp => new FooService(5));
            var mapper = services.BuildServiceProvider().GetService<IMapper>();

            mapper.Map(new ConditionSource { Value = -10 }, new ConditionDest { Value = 7 },
                opt => opt.ConstructServicesUsing(type => type == typeof(DependencyCondition) ? new DependencyCondition(new FooService(100)) : null)).Value.ShouldBe(-10);
            Map(mapper, -10).ShouldBe(7);
        }
    }
}
