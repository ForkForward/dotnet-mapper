namespace ForkForward.Mapper.UnitTests.Projection;

// In ProjectTo, MaxDepth limits how many times a map is used in the query. A map that has derived maps is projected
// as a test of the type of the row, with one branch for the map itself and one for each derived map. When such a
// map reached its limit, building that test threw a NullReferenceException: at the limit the map has no branch.
// Now it gives there what a map without derived maps gives, whatever the type of the row: null, or an empty
// destination when AllowNullDestinationValues is off. Its derived maps are not used there either.
public class ProjectionMaxDepthWithDerivedMaps
{
    public class Category
    {
        public string Name { get; set; }
        public Category Parent { get; set; }
    }
    public class SpecialCategory : Category
    {
        public int Rank { get; set; }
    }
    public class CategoryDto
    {
        public string Name { get; set; }
        public CategoryDto Parent { get; set; }
    }
    public class SpecialCategoryDto : CategoryDto
    {
        public int Rank { get; set; }
    }
    public enum Route { Include, IncludeBase, IncludeAllDerived }

    static MapperConfiguration Hierarchy(int maxDepth, Route route = Route.Include, bool allowNull = true, bool? polymorphic = null) => new(cfg =>
    {
        // High enough for a model that refers to itself to be expanded as far as MaxDepth allows.
        cfg.Internal().RecursiveQueriesMaxDepth = 10;
        cfg.AllowNullDestinationValues = allowNull;
        var categories = cfg.CreateMap<Category, CategoryDto>().MaxDepth(maxDepth);
        var specialCategories = cfg.CreateMap<SpecialCategory, SpecialCategoryDto>();
        switch(route)
        {
            case Route.Include:
                categories.Include<SpecialCategory, SpecialCategoryDto>();
                break;
            case Route.IncludeBase:
                specialCategories.IncludeBase<Category, CategoryDto>();
                break;
            case Route.IncludeAllDerived:
                categories.IncludeAllDerived();
                break;
        }
        if(polymorphic is bool forThisMap)
        {
            categories.PolymorphicProjection(forThisMap);
        }
    });
    // "CSC" is a Category whose parent is a SpecialCategory whose parent is a Category. Each has its level as its name.
    static Category Chain(string kinds)
    {
        Category category = null;
        for(var level = kinds.Length - 1; level >= 0; level--)
        {
            var parent = category;
            category = kinds[level] == 'S' ? new SpecialCategory { Rank = level } : new Category();
            category.Name = level.ToString();
            category.Parent = parent;
        }
        return category;
    }
    // What was projected, in the same notation: a letter for the type that was created on each level, and "_" for
    // a destination that has none of its members set. A row that is null gives "".
    static string Kinds(CategoryDto category)
    {
        var kinds = "";
        for(var level = 0; category != null; category = category.Parent, level++)
        {
            if(category.Name == null)
            {
                category.ShouldBeOfType<CategoryDto>();
                kinds += '_';
                continue;
            }
            category.Name.ShouldBe(level.ToString());
            if(category is SpecialCategoryDto special)
            {
                special.Rank.ShouldBe(level);
                kinds += 'S';
            }
            else
            {
                category.ShouldBeOfType<CategoryDto>();
                kinds += 'C';
            }
        }
        return kinds;
    }
    static string[] Project(MapperConfiguration configuration, params string[] chains) =>
        chains.Select(Chain).ToArray().AsQueryable().ProjectTo<CategoryDto>(configuration).AsEnumerable().Select(Kinds).ToArray();

    [Fact]
    public void The_projection_is_built_and_stops_at_the_limit_of_the_base_map()
    {
        var configuration = Hierarchy(maxDepth: 2);

        // Before: a NullReferenceException inside "Error building the projection.".
        var projected = Project(configuration, "CCCC", "CSCS");

        // The member that would be the third use of the map is null. Above the limit a row of the derived type
        // still gets the derived destination type.
        projected.ShouldBe(new[] { "CC", "CS" });
    }

    [Theory]
    [InlineData(Route.Include)]
    [InlineData(Route.IncludeBase)]
    [InlineData(Route.IncludeAllDerived)]
    public void It_makes_no_difference_how_the_derived_map_was_included(Route route) =>
        Project(Hierarchy(maxDepth: 2, route), "CCCC", "CSCS").ShouldBe(new[] { "CC", "CS" });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void No_row_is_projected_deeper_than_the_limit(int maxDepth)
    {
        var configuration = Hierarchy(maxDepth);
        // Every chain of five that can be made of the two types.
        var chains = Enumerable.Range(0, 32).Select(bits => new string(Enumerable.Range(0, 5).Select(level => (bits >> level & 1) == 1 ? 'S' : 'C').ToArray())).ToArray();

        var projected = Project(configuration, chains);

        for(var index = 0; index < chains.Length; index++)
        {
            projected[index].Length.ShouldBeInRange(1, maxDepth);
            chains[index].ShouldStartWith(projected[index]);
        }
        // A chain of the base type alone gets all the levels the limit allows.
        projected[0].ShouldBe(new string('C', maxDepth));
    }

    [Fact]
    public void The_limit_counts_the_uses_of_the_map_in_the_query_and_the_branch_of_the_base_type_comes_first()
    {
        var configuration = Hierarchy(maxDepth: 2);

        var projected = Project(configuration, "CCCC", "CSCS", "SCSC", "SSSS");

        // new CategoryDto { Parent = (parent is SpecialCategory) ? new SpecialCategoryDto { } : new CategoryDto { } } is
        // the branch for a row of the base type, and it holds both uses of the map that the limit allows. The branch for
        // a row of the derived type is built after it, and the Parent in it would be the third use: it is not projected.
        // The same happens to the second of two members that use one map with MaxDepth(1).
        projected.ShouldBe(new[] { "CC", "CS", "S", "S" });
    }

    [Fact]
    public void The_map_alone_is_projected_to_its_limit_when_polymorphic_projection_is_off()
    {
        // This worked before, and gives the same.
        var configuration = Hierarchy(maxDepth: 2, polymorphic: false);

        Project(configuration, "CCCC", "CSCS", "SCSC", "SSSS").ShouldBe(new[] { "CC", "CC", "CC", "CC" });
    }

    [Fact]
    public void An_empty_destination_of_the_base_type_ends_every_row_when_null_destination_values_are_not_allowed()
    {
        var configuration = Hierarchy(maxDepth: 2, allowNull: false);

        var projected = Project(configuration, "CCCC", "CSCS", "SCSC", "SSSS", "CCSSSSSS");

        // Before, nothing was thrown here, but only a row of the base type ended at the limit, as "CC_". At the limit
        // the derived map was still used, and the levels below it too, as far as RecursiveQueriesMaxDepth allows:
        // the last chain came out as "CCSSSSSS_". The others were "CS", "S" and "S".
        projected.ShouldBe(new[] { "CC_", "CS_", "S_", "S_", "CC_" });
    }

    [Fact]
    public void The_same_without_null_destination_values_and_without_polymorphic_projection()
    {
        // This worked before, and gives the same.
        var configuration = Hierarchy(maxDepth: 2, allowNull: false, polymorphic: false);

        Project(configuration, "CCCC", "CSCS", "SSSS").ShouldBe(new[] { "CC_", "CC_", "CC_" });
    }

    public class Owner
    {
        public Category First { get; set; }
        public Category Second { get; set; }
    }
    public class OwnerDto
    {
        public CategoryDto First { get; set; }
        public CategoryDto Second { get; set; }
    }

    [Fact]
    public void A_derived_map_at_its_own_limit_gives_null_for_the_rows_of_its_type()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Owner, OwnerDto>();
            cfg.CreateMap<Category, CategoryDto>().Include<SpecialCategory, SpecialCategoryDto>();
            cfg.CreateMap<SpecialCategory, SpecialCategoryDto>().MaxDepth(1);
        });
        var owners = new[]
        {
            new Owner { First = Chain("S"), Second = Chain("S") },
            new Owner { First = Chain("C"), Second = Chain("C") },
        };

        // Before: an ArgumentNullException inside "Error building the projection.".
        var projected = owners.AsQueryable().ProjectTo<OwnerDto>(configuration).ToArray();

        // Second is the second use of the derived map. Only the rows of that type are affected.
        Kinds(projected[0].First).ShouldBe("S");
        projected[0].Second.ShouldBeNull();
        Kinds(projected[1].First).ShouldBe("C");
        Kinds(projected[1].Second).ShouldBe("C");
    }

    [Fact]
    public void A_base_map_at_its_limit_gives_null_for_the_rows_of_every_type()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Owner, OwnerDto>();
            cfg.CreateMap<Category, CategoryDto>().Include<SpecialCategory, SpecialCategoryDto>().MaxDepth(1);
            cfg.CreateMap<SpecialCategory, SpecialCategoryDto>();
        });
        var owners = new[]
        {
            new Owner { First = Chain("S"), Second = Chain("S") },
            new Owner { First = Chain("C"), Second = Chain("C") },
        };

        // Before: a NullReferenceException inside "Error building the projection.".
        var projected = owners.AsQueryable().ProjectTo<OwnerDto>(configuration).ToArray();

        Kinds(projected[0].First).ShouldBe("S");
        projected[0].Second.ShouldBeNull();
        Kinds(projected[1].First).ShouldBe("C");
        projected[1].Second.ShouldBeNull();
    }

    [Fact]
    public void The_rows_a_derived_map_gives_null_for_can_be_the_rows_of_the_query()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.Internal().RecursiveQueriesMaxDepth = 10;
            cfg.CreateMap<Category, CategoryDto>().Include<SpecialCategory, SpecialCategoryDto>().MaxDepth(3);
            cfg.CreateMap<SpecialCategory, SpecialCategoryDto>().MaxDepth(1);
        });

        // Before: a NullReferenceException inside "Error building the projection.".
        var projected = Project(configuration, "CCCC", "CCSS", "CSCS", "SCSC");

        // The branch for the derived type is built after the branch for the base type, so the innermost level comes
        // first: the one use the derived map has is spent on the third level. On the second level, and for the
        // rows of the query, a row of the derived type gives null.
        projected.ShouldBe(new[] { "CCC", "CCS", "C", "" });
    }

    public class Folder
    {
        public string Name { get; set; }
        public List<Folder> Children { get; set; } = [];
    }
    public class Archive : Folder
    {
    }
    public class FolderDto
    {
        public string Name { get; set; }
        public List<FolderDto> Children { get; set; }
    }
    public class ArchiveDto : FolderDto
    {
    }

    [Fact]
    public void A_collection_of_the_base_type_is_not_projected_at_the_limit()
    {
        var configuration = new MapperConfiguration(cfg =>
        {
            cfg.Internal().RecursiveQueriesMaxDepth = 10;
            cfg.CreateMap<Folder, FolderDto>().Include<Archive, ArchiveDto>().MaxDepth(2);
            cfg.CreateMap<Archive, ArchiveDto>();
        });
        var folders = new[]
        {
            new Folder { Name = "root", Children = { new Archive { Name = "archive", Children = { new Folder { Name = "deep" } } }, new Folder { Name = "folder", Children = { new Folder { Name = "deep" } } } } },
        };

        // Before: a NullReferenceException inside "Error building the projection.".
        var root = folders.AsQueryable().ProjectTo<FolderDto>(configuration).Single();

        root.ShouldBeOfType<FolderDto>().Name.ShouldBe("root");
        root.Children.Select(child => child.Name).ShouldBe(new[] { "archive", "folder" });
        root.Children[0].ShouldBeOfType<ArchiveDto>();
        root.Children[1].ShouldBeOfType<FolderDto>();
        root.Children.ShouldAllBe(child => child.Children == null);
    }
}
