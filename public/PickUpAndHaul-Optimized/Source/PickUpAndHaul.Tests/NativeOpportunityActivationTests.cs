using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using PickUpAndHaul.NativeOpportunity;

namespace PickUpAndHaul.Tests;

/// <summary>The coexistence rule: native opportunity hauling never runs next to a standalone While You're Up.</summary>
public class NativeOpportunityActivationTests
{
    private static Func<string, bool> Only(params string[] active)
        => id => active.Contains(id, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void TheKnownStandalonePackageIdsAreExactlyThese()
    {
        Assert.Equal(
            new[] { "D3athAn63l.zWYU", "CodeOptimist.JobsOfOpportunity", "hoodie.whileyoureup", "kevlou127.WhileHYouOreHUpHQ1V0S" },
            NativeOpportunityActivation.StandaloneWyuPackageIds);
    }

    [Fact]
    public void TheFeatureIsOffWhenTheSettingIsOffWhateverElseIsLoaded()
    {
        Assert.Equal(NativeOpportunityStatus.Off, NativeOpportunityActivation.Evaluate(false, Only()));
        foreach (var id in NativeOpportunityActivation.StandaloneWyuPackageIds)
        {
            Assert.Equal(NativeOpportunityStatus.Off, NativeOpportunityActivation.Evaluate(false, Only(id)));
        }
    }

    [Fact]
    public void TheFeatureIsActiveOnlyWhenEnabledAndNoStandaloneWyuIsLoaded()
    {
        Assert.Equal(NativeOpportunityStatus.Active, NativeOpportunityActivation.Evaluate(true, Only()));
        Assert.Equal(NativeOpportunityStatus.Active, NativeOpportunityActivation.Evaluate(true, Only("brrainz.harmony", "ludeon.rimworld")));
    }

    [Theory]
    [InlineData("D3athAn63l.zWYU")]
    [InlineData("CodeOptimist.JobsOfOpportunity")]
    [InlineData("hoodie.whileyoureup")]
    [InlineData("kevlou127.WhileHYouOreHUpHQ1V0S")]
    public void EachStandaloneWyuMakesTheEnabledFeatureInactive(string packageId)
    {
        Assert.Equal(NativeOpportunityStatus.InactiveStandaloneWyuActive, NativeOpportunityActivation.Evaluate(true, Only(packageId)));
        Assert.True(NativeOpportunityActivation.AnyStandaloneWyuActive(Only(packageId)));
        // package ids are case-insensitive in RimWorld; the lookup is the caller's, the id list is just the names
        Assert.True(NativeOpportunityActivation.AnyStandaloneWyuActive(Only(packageId.ToLowerInvariant())));
    }

    [Fact]
    public void EveryKnownIdIsAskedAbout()
    {
        var asked = new List<string>();
        Assert.False(NativeOpportunityActivation.AnyStandaloneWyuActive(id => { asked.Add(id); return false; }));
        Assert.Equal(NativeOpportunityActivation.StandaloneWyuPackageIds, asked);
        Assert.Throws<ArgumentNullException>(() => NativeOpportunityActivation.Evaluate(true, null));
    }
}
