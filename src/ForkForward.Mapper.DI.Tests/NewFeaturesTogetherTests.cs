using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace ForkForward.Mapper.Extensions.Microsoft.DependencyInjection.Tests
{
    public class TogetherSource
    {
        public int Value { get; set; }
    }

    public class TogetherDest
    {
        public int Value { get; set; }
        public int CreatedWith { get; set; }
    }

    // Each of the three takes the same service. In one scope they must all get the instance of that scope.
    internal class TogetherPreCondition : IMemberPreCondition<TogetherSource, TogetherDest>
    {
        private readonly ISomeService _service;

        public TogetherPreCondition(ISomeService service) => _service = service;

        public bool ShouldMap(TogetherSource source, TogetherDest destination, ResolutionContext context) => _service.Modify(0) >= 0;
    }

    internal class TogetherCondition : IMemberCondition<TogetherSource, TogetherDest, int>
    {
        private readonly ISomeService _service;

        public TogetherCondition(ISomeService service) => _service = service;

        public bool ShouldMap(TogetherSource source, TogetherDest destination, int sourceMember, int destinationMember, ResolutionContext context) =>
            _service.Modify(sourceMember) > 0;
    }

    internal class TogetherFactory : IDestinationFactory<TogetherSource, TogetherDest>
    {
        private readonly ISomeService _service;

        public TogetherFactory(ISomeService service) => _service = service;

        public TogetherDest Create(TogetherSource source, ResolutionContext context) => new TogetherDest { CreatedWith = _service.Modify(0) };
    }

    public class NewFeaturesTogetherTests
    {
        // The map is added here and not in a profile: other tests count the maps of the profiles in this assembly.
        private static IServiceCollection Services()
        {
            var services = new ServiceCollection();
            services.AddAutoMapper(cfg => cfg.CreateMap<TogetherSource, TogetherDest>()
                .ConstructUsing<TogetherFactory>()
                .ForMember(d => d.CreatedWith, opt => opt.Ignore())
                .ForMember(d => d.Value, opt =>
                {
                    opt.PreCondition<TogetherPreCondition>();
                    opt.Condition<TogetherCondition>();
                }), typeof(Source).Assembly);
            services.AddScoped<ISomeService, MutableService>();
            return services;
        }

        [Theory]
        [InlineData(typeof(TogetherPreCondition))]
        [InlineData(typeof(TogetherCondition))]
        [InlineData(typeof(TogetherFactory))]
        public void One_scan_registers_conditions_preconditions_and_factories_as_transient(Type type) =>
            Services().Single(descriptor => descriptor.ServiceType == type).Lifetime.ShouldBe(ServiceLifetime.Transient);

        [Fact]
        public void A_factory_a_precondition_and_a_condition_of_one_map_get_the_services_of_the_scope()
        {
            var provider = Services().BuildServiceProvider();

            using (var scope = provider.CreateScope())
            {
                ((MutableService)scope.ServiceProvider.GetService<ISomeService>()).Value = 10;
                var mapper = scope.ServiceProvider.GetService<IMapper>();

                var mapped = mapper.Map<TogetherDest>(new TogetherSource { Value = -3 });
                // Created by the factory with this scope's service; -3 + 10 is positive, so the member is mapped.
                mapped.CreatedWith.ShouldBe(10);
                mapped.Value.ShouldBe(-3);
            }
            using (var scope = provider.CreateScope())
            {
                ((MutableService)scope.ServiceProvider.GetService<ISomeService>()).Value = 1;
                var mapper = scope.ServiceProvider.GetService<IMapper>();

                var mapped = mapper.Map<TogetherDest>(new TogetherSource { Value = -3 });
                // -3 + 1 is negative: the condition says no, and the member keeps the default of the new object.
                mapped.CreatedWith.ShouldBe(1);
                mapped.Value.ShouldBe(0);
            }
            using (var scope = provider.CreateScope())
            {
                ((MutableService)scope.ServiceProvider.GetService<ISomeService>()).Value = -1;
                var mapper = scope.ServiceProvider.GetService<IMapper>();

                // The precondition says no before the source value is read.
                mapper.Map<TogetherDest>(new TogetherSource { Value = 50 }).Value.ShouldBe(0);
            }
        }

        [Fact]
        public void The_configuration_with_all_three_is_valid()
        {
            var provider = Services().BuildServiceProvider();

            provider.GetService<IConfigurationProvider>().AssertConfigurationIsValid();
        }
    }
}
