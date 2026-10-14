using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

/// <summary>
/// An item the traveler no longer needs has to be removable from the same place it is edited,
/// and because removal is irreversible the form asks before it acts.
/// </summary>
public class TrackedItemFormDeleteTests : BunitContext
{
    private readonly FormStubTripApiClient _api = new();

    public TrackedItemFormDeleteTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(_api);
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
    }

    private static readonly DateTime LegStart = new(2026, 9, 1, 8, 0, 0);
    private static readonly DateTime LegEnd = new(2026, 9, 10, 18, 0, 0);

    private IRenderedComponent<TrackedItemForm> RenderEdit(Action? onSaved = null, bool canEditContent = true, Action? onCancel = null)
    {
        var leg = TrackedItemFormTestData.Leg(LegStart, LegEnd);
        var item = TrackedItemFormTestData.Item(leg.TripLegId, new DateTime(2026, 9, 5, 14, 0, 0), new DateTime(2026, 9, 5, 16, 0, 0));
        return Render<TrackedItemForm>(p =>
        {
            p.Add(x => x.TripId, Guid.NewGuid());
            p.Add(x => x.Legs, new[] { leg });
            p.Add(x => x.Item, item);
            p.Add(x => x.CanEditContent, canEditContent);
            if (onSaved is not null)
            {
                p.Add(x => x.OnSaved, onSaved);
            }
            if (onCancel is not null)
            {
                p.Add(x => x.OnCancel, onCancel);
            }
        });
    }

    [Fact]
    public void FooterActions_PlaceSaveAndCancelOnTheLeftAndDeleteOnTheRight()
    {
        var cancelled = 0;
        var cut = RenderEdit(onCancel: () => cancelled++);
        var actions = cut.Find(".card-footer .entry-form-actions");
        Assert.Contains("justify-content-start", actions.ClassList);
        Assert.Contains("w-100", actions.ClassList);
        Assert.Contains("gap-3", actions.ClassList);
        var delete = cut.Find("#item-delete");
        Assert.Contains("btn-outline-danger", delete.ClassList);
        Assert.Contains("ms-auto", delete.ClassList);
        Assert.DoesNotContain("btn-sm", delete.ClassList);
        Assert.DoesNotContain("btn-outline-secondary", delete.ClassList);
        Assert.Equal("submit", actions.Children[0].GetAttribute("type"));
        Assert.Equal("Delete", delete.TextContent.Trim());
        Assert.Equal("Confirm deletion of this trip item", delete.GetAttribute("title"));
        Assert.Equal(["Save", "Cancel", "Delete"], actions.Children.Select(button => button.TextContent.Trim()));
        Assert.Equal("Save changes to this trip item", actions.Children[0].GetAttribute("title"));
        cut.Find("#item-cancel").Click();
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public void AnItemBeingCreated_OffersNoDelete()
    {
        var leg = TrackedItemFormTestData.Leg(LegStart, LegEnd);

        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.CanEditContent, true));

        Assert.Empty(cut.FindAll("#item-delete"));
    }

    /// <summary>Only an owner or a collaborator may remove an item; a viewer is never offered the action.</summary>
    [Fact]
    public void AViewer_IsOfferedNoDelete()
    {
        var cut = RenderEdit(canEditContent: false);

        Assert.Empty(cut.FindAll("#item-delete"));
    }

    [Fact]
    public void DeletingAnItem_AsksBeforeItRemovesAnything()
    {
        var cut = RenderEdit();

        cut.Find("#item-delete").Click();

        Assert.NotNull(cut.Find("[data-testid=item-delete-confirm]"));
        Assert.Contains("Are you sure you want to delete this item", cut.Find("[role='dialog']").TextContent);
        Assert.Empty(cut.FindAll(".card-footer [data-testid=item-delete-confirm]"));
        Assert.NotNull(cut.Find(".modal-backdrop"));
        Assert.Equal("Delete", cut.Find("#item-delete-confirm").TextContent.Trim());
        Assert.Equal("Permanently delete this trip item", cut.Find("#item-delete-confirm").GetAttribute("title"));
        Assert.Empty(_api.DeletedItems);
    }

    [Fact]
    public void ConfirmingTheDelete_RemovesTheItemAndTellsTheHost()
    {
        var saved = false;
        var cut = RenderEdit(() => saved = true);

        cut.Find("#item-delete").Click();
        cut.Find("#item-delete-confirm").Click();

        Assert.Single(_api.DeletedItems);
        Assert.True(saved);
    }

    [Fact]
    public void KeepingTheItem_LeavesItAlone()
    {
        var cut = RenderEdit();

        cut.Find("#item-delete").Click();
        cut.Find("[data-testid=item-delete-confirm] .btn-outline-secondary").Click();

        Assert.Empty(cut.FindAll("[data-testid=item-delete-confirm]"));
        Assert.Empty(_api.DeletedItems);
    }

    [Fact]
    public void RefusedDelete_SurfacesTheReasonAndKeepsAsking()
    {
        const string reason = "We couldn't remove this item.";
        _api.DeleteFailure = new InvalidOperationException(reason);

        var cut = RenderEdit();

        cut.Find("#item-delete").Click();
        cut.Find("#item-delete-confirm").Click();

        Assert.Contains(reason, cut.Markup, StringComparison.Ordinal);
        Assert.Contains(reason, cut.Find("[role='dialog'] [role='alert']").TextContent);
        Assert.NotNull(cut.Find("[data-testid=item-delete-confirm]"));
    }
}
