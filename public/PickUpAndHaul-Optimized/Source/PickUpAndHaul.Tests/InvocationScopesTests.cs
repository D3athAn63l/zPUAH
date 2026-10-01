using System;
using Xunit;
using PickUpAndHaul.NativeOpportunity;

namespace PickUpAndHaul.Tests;

/// <summary>
/// The lifecycle promises of the TryOpportunisticJob hook: the search fires at most once per owner invocation, a nested invocation
/// cannot consume the outer one's search, and a scope is gone after its invocation however that ended.
/// </summary>
public class InvocationScopesTests
{
    private sealed class Ctx
    {
        public string Name;
        public bool OwnsEverything = true;
        public override string ToString() => Name;
    }

    private static bool Owns(Ctx ctx, int _) => ctx.OwnsEverything;

    private static InvocationScopes<Ctx>.Scope Claim(InvocationScopes<Ctx> scopes) => scopes.TryClaimSearch(0, Owns);

    [Fact]
    public void NothingIsOpenUntilAnInvocationStarts()
    {
        var scopes = new InvocationScopes<Ctx>();
        Assert.False(scopes.AnyOpen);
        Assert.Equal(0, scopes.Depth);
        Assert.Null(Claim(scopes)); // a haulables query outside any invocation is nobody's
    }

    [Fact]
    public void TheSearchFiresAtMostOncePerInvocation()
    {
        var scopes = new InvocationScopes<Ctx>();
        var scope = scopes.Open(new Ctx { Name = "outer" });

        Assert.Same(scope, Claim(scopes));
        Assert.Null(Claim(scopes));   // the search itself asks the haulables question again: it passes straight through
        Assert.Null(Claim(scopes));
        Assert.True(scope.SearchClaimed);
    }

    [Fact]
    public void AQueryThatIsNotTheInvocationsOwnLeavesItsSearchAvailable()
    {
        var scopes = new InvocationScopes<Ctx>();
        var ctx = new Ctx { Name = "outer", OwnsEverything = false };
        var scope = scopes.Open(ctx);

        Assert.Null(Claim(scopes));   // e.g. another map's lister
        Assert.False(scope.SearchClaimed);

        ctx.OwnsEverything = true;
        Assert.Same(scope, Claim(scopes));
    }

    [Fact]
    public void ANestedInvocationCannotConsumeTheOuterOnesSearch()
    {
        var scopes = new InvocationScopes<Ctx>();
        var outer = scopes.Open(new Ctx { Name = "outer" });
        var inner = scopes.Open(new Ctx { Name = "inner" });
        Assert.Equal(2, scopes.Depth);

        // queries made while the inner scope is open belong to it alone
        Assert.Same(inner, Claim(scopes));
        Assert.Null(Claim(scopes));
        Assert.False(outer.SearchClaimed);

        scopes.Close(inner);
        Assert.Equal(1, scopes.Depth);

        // the outer search is untouched and still available
        Assert.Same(outer, Claim(scopes));
        scopes.Close(outer);
        Assert.False(scopes.AnyOpen);
    }

    [Fact]
    public void ANestedInvocationThatDoesNotOwnTheQueryDoesNotHideTheOuterOne_ButAlsoDoesNotLetItBeStolen()
    {
        // Only the innermost scope is ever consulted: a query it does not own is not answered by the outer scope instead.
        var scopes = new InvocationScopes<Ctx>();
        var outer = scopes.Open(new Ctx { Name = "outer" });
        scopes.Open(new Ctx { Name = "inner", OwnsEverything = false });

        Assert.Null(Claim(scopes));
        Assert.False(outer.SearchClaimed);
    }

    [Fact]
    public void ClosingIsIdempotentAndRestoresTheEnclosingScope()
    {
        var scopes = new InvocationScopes<Ctx>();
        var outer = scopes.Open(new Ctx { Name = "outer" });
        var inner = scopes.Open(new Ctx { Name = "inner" });

        scopes.Close(inner);
        scopes.Close(inner);
        scopes.Close(null);
        Assert.Equal(1, scopes.Depth);
        Assert.False(outer.Closed);

        scopes.Close(outer);
        scopes.Close(outer);
        Assert.False(scopes.AnyOpen);
        Assert.True(outer.Closed && inner.Closed);
        Assert.Null(Claim(scopes));
    }

    [Fact]
    public void ClosingAnOuterScopeAlsoEndsAnInnerScopeThatWasLeftOpen()
    {
        var scopes = new InvocationScopes<Ctx>();
        var outer = scopes.Open(new Ctx { Name = "outer" });
        var leaked = scopes.Open(new Ctx { Name = "leaked" });

        scopes.Close(outer);

        Assert.False(scopes.AnyOpen);
        Assert.True(leaked.Closed);
        scopes.Close(leaked);          // the late close of the leaked one must not disturb anything
        Assert.False(scopes.AnyOpen);

        var next = scopes.Open(new Ctx { Name = "next" });
        Assert.Same(next, Claim(scopes));
    }

    [Fact]
    public void ClosingAScopeThatIsNoLongerOpenDoesNotTouchOtherScopes()
    {
        var scopes = new InvocationScopes<Ctx>();
        var gone = scopes.Open(new Ctx { Name = "gone" });
        scopes.Close(gone);

        var live = scopes.Open(new Ctx { Name = "live" });
        scopes.Close(gone);            // stale close

        Assert.False(live.Closed);
        Assert.Equal(1, scopes.Depth);
        Assert.Same(live, Claim(scopes));
    }

    [Fact]
    public void AnExceptionThatUnwindsTheInvocationLeavesNoScopeBehind()
    {
        var scopes = new InvocationScopes<Ctx>();

        Action ownerThatThrows = () =>
        {
            var scope = scopes.Open(new Ctx { Name = "boom" });
            try
            {
                Assert.Same(scope, Claim(scopes));
                throw new InvalidOperationException("the owner method threw");
            }
            finally
            {
                scopes.Close(scope);   // what the finalizer does
            }
        };

        Assert.Throws<InvalidOperationException>(ownerThatThrows);

        Assert.False(scopes.AnyOpen);
        Assert.Null(Claim(scopes));
    }

    [Fact]
    public void AClosedScopeCanNeverBeClaimed()
    {
        var scopes = new InvocationScopes<Ctx>();
        var scope = scopes.Open(new Ctx { Name = "a" });
        scopes.Close(scope);
        Assert.Null(Claim(scopes));
    }
}
