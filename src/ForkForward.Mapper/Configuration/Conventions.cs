namespace ForkForward.Mapper.Configuration.Conventions;
public interface ISourceToDestinationNameMapper
{
    MemberInfo GetSourceMember(TypeDetails sourceTypeDetails, Type destType, Type destMemberType, string nameToSearch);
    void Merge(ISourceToDestinationNameMapper other);
}
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MemberConfiguration
{
    NameSplitMember _nameSplitMember;
    public INamingConvention SourceNamingConvention { get; set; }
    public INamingConvention DestinationNamingConvention { get; set; }
    public List<ISourceToDestinationNameMapper> NameToMemberMappers { get; } = [];
    public bool IsMatch(ProfileMap options, TypeDetails sourceTypeDetails, Type destType, Type destMemberType, string nameToSearch, List<MemberInfo> resolvers, bool isReverseMap)
    {
        var matchingMemberInfo = GetSourceMember(sourceTypeDetails, destType, destMemberType, nameToSearch);
        if (matchingMemberInfo != null)
        {
            resolvers.Add(matchingMemberInfo);
            return true;
        }
        return nameToSearch.Length == 0 || _nameSplitMember.IsMatch(options, sourceTypeDetails, destType, destMemberType, nameToSearch, resolvers, isReverseMap);
    }
    internal bool IsChainMatch(ProfileMap options, TypeDetails sourceTypeDetails, Type destType, Type destMemberType, string nameToSearch, List<MemberInfo> resolvers, bool isReverseMap) =>
        _nameSplitMember is ConventionsNameSplitMember conventions && conventions.IsChainMatch(options, sourceTypeDetails, destType, destMemberType, nameToSearch, resolvers, isReverseMap);
    public MemberInfo GetSourceMember(TypeDetails sourceTypeDetails, Type destType, Type destMemberType, string nameToSearch)
    {
        var sourceMember = sourceTypeDetails.GetMember(nameToSearch);
        if (sourceMember != null)
        {
            return sourceMember;
        }
        foreach (var mapper in NameToMemberMappers)
        {
            if ((sourceMember = mapper.GetSourceMember(sourceTypeDetails, destType, destMemberType, nameToSearch)) != null)
            {
                return sourceMember;
            }
        }
        return null;
    }
    public void Seal()
    {
        var isDefault = SourceNamingConvention == PascalCaseNamingConvention.Instance && DestinationNamingConvention == PascalCaseNamingConvention.Instance;
        _nameSplitMember = isDefault ? new DefaultNameSplitMember() : new ConventionsNameSplitMember();
        _nameSplitMember.Parent = this;
    }
    public void Merge(MemberConfiguration other)
    {
        var initialCount = NameToMemberMappers.Count;
        for (int index = 0; index < other.NameToMemberMappers.Count; index++)
        {
            var otherMapper = other.NameToMemberMappers[index];
            if (index < initialCount)
            {
                var nameToMemberMapper = NameToMemberMappers[index];
                if (nameToMemberMapper.GetType() == otherMapper.GetType())
                {
                    nameToMemberMapper.Merge(otherMapper);
                    continue;
                }
            }
            NameToMemberMappers.Add(otherMapper);
        }
        SourceNamingConvention ??= other.SourceNamingConvention;
        DestinationNamingConvention ??= other.DestinationNamingConvention;
    }
}
public sealed class PrePostfixName : ISourceToDestinationNameMapper
{
    public List<string> DestinationPrefixes { get; } = [];
    public List<string> DestinationPostfixes { get; } = [];
    public MemberInfo GetSourceMember(TypeDetails sourceTypeDetails, Type destType, Type destMemberType, string nameToSearch)
    {
        MemberInfo member;
        foreach (var possibleSourceName in TypeDetails.PossibleNames(nameToSearch, DestinationPrefixes, DestinationPostfixes))
        {
            if ((member = sourceTypeDetails.GetMember(possibleSourceName)) != null)
            {
                return member;
            }
        }
        return null;
    }
    public void Merge(ISourceToDestinationNameMapper other)
    {
        var typedOther = (PrePostfixName)other;
        DestinationPrefixes.TryAdd(typedOther.DestinationPrefixes);
        DestinationPostfixes.TryAdd(typedOther.DestinationPostfixes);
    }
}
public sealed class ReplaceName : ISourceToDestinationNameMapper
{
    public List<MemberNameReplacer> MemberNameReplacers { get; } = [];
    public MemberInfo GetSourceMember(TypeDetails sourceTypeDetails, Type destType, Type destMemberType, string nameToSearch)
    {
        var possibleSourceNames = PossibleNames(nameToSearch);
        if (possibleSourceNames.Count == 0)
        {
            return null;
        }
        var possibleDestNames = Array.ConvertAll(sourceTypeDetails.ReadAccessors, mi => (mi, possibles : PossibleNames(mi.Name)));
        foreach (var sourceName in possibleSourceNames)
        {
            foreach (var (mi, possibles) in possibleDestNames)
            {
                if (possibles.Contains(sourceName, StringComparer.OrdinalIgnoreCase))
                {
                    return mi;
                }
            }
        }
        return null;
    }
    public void Merge(ISourceToDestinationNameMapper other) => MemberNameReplacers.TryAdd(((ReplaceName)other).MemberNameReplacers);
    private List<string> PossibleNames(string nameToSearch) => [..MemberNameReplacers.Select(r => nameToSearch.Replace(r.OriginalValue, r.NewValue)), 
        MemberNameReplacers.Aggregate(nameToSearch, (s, r) => s.Replace(r.OriginalValue, r.NewValue)), nameToSearch];
}
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly record struct MemberNameReplacer(string OriginalValue, string NewValue);
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class NameSplitMember
{
    public MemberConfiguration Parent { get; set; }
    public abstract bool IsMatch(ProfileMap options, TypeDetails sourceType, Type destType, Type destMemberType, string nameToSearch, List<MemberInfo> resolvers, bool isReverseMap);
}
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DefaultNameSplitMember : NameSplitMember
{
    public sealed override bool IsMatch(ProfileMap options, TypeDetails sourceType, Type destType, Type destMemberType, string nameToSearch, List<MemberInfo> resolvers, bool isReverseMap)
    {
        MemberInfo matchingMemberInfo = null;
        int index = 1;
        for (; index < nameToSearch.Length; index++)
        {
            if (char.IsUpper(nameToSearch[index]) && Found())
            {
                return true;
            }
        }
        return matchingMemberInfo != null && Found();
        bool Found()
        {
            var first = nameToSearch[..index];
            matchingMemberInfo = Parent.GetSourceMember(sourceType, destType, destMemberType, first);
            if (matchingMemberInfo == null)
            {
                return false;
            }
            resolvers.Add(matchingMemberInfo);
            var second = nameToSearch[index..];
            var details = options.CreateTypeDetails(matchingMemberInfo.GetMemberType());
            if (Parent.IsMatch(options, details, destType, destMemberType, second, resolvers, isReverseMap))
            {
                return true;
            }
            resolvers.RemoveAt(resolvers.Count - 1);
            return false;
        }
    }
}
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ConventionsNameSplitMember : NameSplitMember
{
    public sealed override bool IsMatch(ProfileMap options, TypeDetails sourceType, Type destType, Type destMemberType, string nameToSearch, List<MemberInfo> resolvers, bool isReverseMap)
    {
        var destinationNamingConvention = isReverseMap ? Parent.SourceNamingConvention : Parent.DestinationNamingConvention;
        var matches = destinationNamingConvention.Split(nameToSearch);
        var length = matches.Length;
        if (length < 2)
        {
            return false;
        }
        var sourceNamingConvention = isReverseMap ? Parent.DestinationNamingConvention : Parent.SourceNamingConvention;
        var separator = sourceNamingConvention.SeparatorCharacter;
        if (IsMatch(options, sourceType, destType, destMemberType, matches, separator, resolvers, isReverseMap))
        {
            return true;
        }
        // Nothing was found with every capital read as the start of a word, which is what PascalCaseNamingConvention.Split
        // says: FormXML is Form, X, M, L and the source member would be form_x_m_l. Only then a run of capitals is read
        // as one word: Form, XML. Without a separator both readings give the same names, and there is nothing more to try.
        // Only the name of the destination member is read again: what is left of it after a source member was found
        // (resolvers) has been put together with the separator and is not in PascalCase.
        if (resolvers.Count != 0)
        {
            return false;
        }
        var words = RunsOfCapitals(destinationNamingConvention, separator, nameToSearch);
        return words != null && IsMatch(options, sourceType, destType, destMemberType, words, separator, resolvers, isReverseMap);
    }
    static string[] RunsOfCapitals(INamingConvention destinationNamingConvention, string separator, string nameToSearch) =>
        destinationNamingConvention is PascalCaseNamingConvention && !string.IsNullOrEmpty(separator) ? PascalCaseNamingConvention.SplitRunsOfCapitals(nameToSearch) : null;
    // Tried for the name of a destination member when IsMatch found nothing for it (ProfileMap.MapDestinationPropertyToSource),
    // so that a name that was matched before is matched to the same members. IsMatch puts what is left of the name after a
    // source member was found together with the separator of the source side and, when that is not a member of its type,
    // splits it again with the convention of the destination side. With two different conventions it cannot be split:
    // Address_City is not in PascalCase and addresscity has no underscore. So a chain of two source members is found there
    // and a chain of three is not. Here the words of the destination member are followed as they are, in the one reading
    // and then in the other. A chain of two is what IsMatch looks for: it takes three words to find another one.
    internal bool IsChainMatch(ProfileMap options, TypeDetails sourceType, Type destType, Type destMemberType, string nameToSearch, List<MemberInfo> resolvers, bool isReverseMap)
    {
        var destinationNamingConvention = isReverseMap ? Parent.SourceNamingConvention : Parent.DestinationNamingConvention;
        var matches = destinationNamingConvention.Split(nameToSearch);
        if (matches.Length < 3)
        {
            return false;
        }
        var separator = (isReverseMap ? Parent.DestinationNamingConvention : Parent.SourceNamingConvention).SeparatorCharacter;
        if (IsChain(options, sourceType, destType, destMemberType, matches, 0, separator, resolvers))
        {
            return true;
        }
        var words = RunsOfCapitals(destinationNamingConvention, separator, nameToSearch);
        return words?.Length > 2 && IsChain(options, sourceType, destType, destMemberType, words, 0, separator, resolvers);
    }
    bool IsMatch(ProfileMap options, TypeDetails sourceType, Type destType, Type destMemberType, string[] matches, string separator, List<MemberInfo> resolvers, bool isReverseMap)
    {
        var length = matches.Length;
        for (var index = 1; index <= length; index++)
        {
            var first = string.Join(separator, matches, 0, index);
            var matchingMemberInfo = Parent.GetSourceMember(sourceType, destType, destMemberType, first);
            if (matchingMemberInfo != null)
            {
                resolvers.Add(matchingMemberInfo);
                var second = string.Join(separator, matches, index, length - index);
                var details = options.CreateTypeDetails(matchingMemberInfo.GetMemberType());
                if (Parent.IsMatch(options, details, destType, destMemberType, second, resolvers, isReverseMap))
                {
                    return true;
                }
                resolvers.RemoveAt(resolvers.Count - 1);
            }
        }
        return false;
    }
    // The words from start on as a chain of source members: the first of them, put together with the separator, are a
    // source member, and the words after them are a chain in the type of that member, down to the last word.
    bool IsChain(ProfileMap options, TypeDetails sourceType, Type destType, Type destMemberType, string[] words, int start, string separator, List<MemberInfo> resolvers)
    {
        var length = words.Length;
        for (var index = start + 1; index <= length; index++)
        {
            var first = string.Join(separator, words, start, index - start);
            var matchingMemberInfo = Parent.GetSourceMember(sourceType, destType, destMemberType, first);
            if (matchingMemberInfo != null)
            {
                resolvers.Add(matchingMemberInfo);
                if (index == length || IsChain(options, options.CreateTypeDetails(matchingMemberInfo.GetMemberType()), destType, destMemberType, words, index, separator, resolvers))
                {
                    return true;
                }
                resolvers.RemoveAt(resolvers.Count - 1);
            }
        }
        return false;
    }
}