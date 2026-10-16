using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Pages.Trips;
using TripPlanner.Web.Components.Shared;
using TripPlanner.Web.Features.EntryPages;
using TripPlanner.Web.Tests.Infrastructure;
using TripPlanner.Web.Tests.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.FavoriteDestinations;
using TripPlanner.Web.Tests.FavoriteDestinations;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Web.Components.Pages.FavoriteDestinations;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Tests.Profile;

namespace TripPlanner.Web.Tests.EntryPages;

public sealed class EntryPageLifecycleTests : BunitContext
{
    [Fact]
    public void Shell_ShowsBannerAboveCardWithContextAndIcon()
    {
        var cut = Render<EntryPageShell>(parameters => parameters
            .Add(component => component.Title, "Entry")
            .Add(component => component.Context, "Plan your trip.")
            .Add(component => component.Icon, "trip")
            .AddChildContent("<form id='entry-form'><div class='card-body'></div><div class='card-footer'></div></form>"));

        Assert.Equal("Entry", cut.Find(".entry-page-shell > header.entry-page-heading h1").TextContent);
        Assert.Equal("-1", cut.Find("h1").GetAttribute("tabindex"));
        Assert.Equal("Plan your trip.", cut.Find(".entry-page-subtitle").TextContent);
        Assert.Equal("trip", cut.FindComponent<ActionIcon>().Instance.Name);
        Assert.Equal("true", cut.Find(".entry-page-icon svg").GetAttribute("aria-hidden"));
        Assert.NotNull(cut.Find("header.entry-page-heading + .entry-form-card"));
        Assert.Empty(cut.FindAll(".entry-form-card > .card-header"));
        Assert.NotNull(cut.Find(".entry-form-card > form > .card-body"));
        Assert.NotNull(cut.Find(".entry-form-card > form > .card-footer"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HeadingWithoutContextOrActionsKeepsItsTitleAndDecorativeIcon(string? context)
    {
        var cut = Render<EntryPageHeading>(parameters => parameters
            .Add(component => component.Title, "Profile")
            .Add(component => component.Context, context)
            .Add(component => component.Icon, "profile"));

        Assert.Equal("Profile", Assert.Single(cut.FindAll("h1")).TextContent);
        Assert.Equal("-1", cut.Find("h1").GetAttribute("tabindex"));
        Assert.Equal("true", cut.Find(".entry-page-icon svg").GetAttribute("aria-hidden"));
        Assert.Equal("false", cut.Find(".entry-page-icon svg").GetAttribute("focusable"));
        Assert.Empty(cut.FindAll(".entry-page-subtitle, .entry-page-actions"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegEntry_UsesRequestedHeadingAndCurrentBreadcrumb(bool isEdit)
    {
        var trip = RegisterTrip();
        var cut = Render<TripLegEntry>(parameters => parameters
            .Add(page => page.TripId, trip.TripId)
            .Add(page => page.LegId, isEdit ? trip.Legs[0].TripLegId : null));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".entry-form-card form .card-body")));
        AssertFooter(cut);
        Assert.Equal(isEdit ? "Edit this leg" : "Create a leg", cut.Find("h1").TextContent);
        Assert.Equal(["Home", "Trips", trip.Name, isEdit ? trip.Legs[0].Title : "New Leg"],
            cut.FindAll(".breadcrumb-item").Select(item => item.TextContent.Trim()));
        Assert.Single(cut.FindAll("[aria-current='page']"));
        AssertBanner(cut, "leg", "Plan the travel or stay that connects your itinerary.");
        Assert.Equal("Save", cut.Find(".card-footer button[type='submit']").TextContent.Trim());
        Assert.Equal(isEdit ? "Save changes to this trip leg" : "Save this new leg to the trip", cut.Find(".card-footer button[type='submit']").GetAttribute("title"));
        Assert.Contains(cut.FindComponents<ActionIcon>(), icon => icon.Instance.Name == "save");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ItemEntry_UsesRequestedHeadingAndCurrentBreadcrumb(bool isEdit, bool assigned)
    {
        var trip = RegisterTrip(assigned);
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo($"/trips/{trip.TripId:D}/items/new" + (assigned ? $"?legId={trip.Legs[0].TripLegId:D}" : string.Empty));
        var cut = Render<TrackedItemEntry>(parameters => parameters
            .Add(page => page.TripId, trip.TripId)
            .Add(page => page.ItemId, isEdit ? trip.TrackedItems[0].TrackedItemId : null));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".entry-form-card form .card-body")));
        AssertFooter(cut);
        Assert.Equal(isEdit ? "Edit this item" : "Create an item", cut.Find("h1").TextContent);
        var expected = new List<string> { "Home", "Trips", isEdit && assigned ? $"{trip.Name} - {trip.Legs[0].Title}" : trip.Name };
        if (isEdit && !assigned) expected.Add("Unassigned items");
        expected.Add(isEdit ? trip.TrackedItems[0].Title : "New Item");
        Assert.Equal(expected, cut.FindAll(".breadcrumb-item").Select(item => item.TextContent.Trim()));
        Assert.Single(cut.FindAll("[aria-current='page']"));
        if (isEdit && assigned)
        {
            Assert.Equal($"/trips/{trip.TripId:D}#leg-{trip.Legs[0].TripLegId:D}", cut.FindAll(".breadcrumb-item a")[^1].GetAttribute("href"));
        }
        AssertBanner(cut, "item", "Organize an activity, reservation, or other detail for your trip.");
        Assert.Equal(isEdit ? "blue" : "purple", cut.Find("#item-color-name").TextContent);
        Assert.Equal("Save", cut.Find(".card-footer button[type='submit']").TextContent.Trim());
        Assert.Equal(isEdit ? "Save changes to this trip item" : "Save this new item to the trip", cut.Find(".card-footer button[type='submit']").GetAttribute("title"));
        Assert.Contains(cut.FindComponents<ActionIcon>(), icon => icon.Instance.Name == "save");
    }

    [Fact]
    public void EditTrip_EndsBreadcrumbAtTripAndHasContextualBanner()
    {
        var trip = RegisterTrip();
        var cut = Render<EditTrip>(parameters => parameters.Add(page => page.TripId, trip.TripId));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".entry-form-card form .card-body")));
        AssertFooter(cut);
        Assert.Equal("Edit this trip", cut.Find("h1").TextContent);
        Assert.Equal("Save", cut.Find(".card-footer button[type='submit']").TextContent.Trim());
        Assert.Equal("Save changes to this trip", cut.Find(".card-footer button[type='submit']").GetAttribute("title"));
        Assert.Equal("Cancel without saving changes to this trip", cut.Find(".card-footer a").GetAttribute("title"));
        Assert.Equal(["Home", "Trips", trip.Name], cut.FindAll(".breadcrumb-item").Select(item => item.TextContent.Trim()));
        Assert.Equal(trip.Name, cut.Find("[aria-current='page']").TextContent);
        var warning = cut.Find(".alert-warning");
        Assert.Contains("Changing trip dates can leave existing legs or items outside the trip range.", warning.TextContent);
        Assert.Contains(cut.FindComponents<ActionIcon>(), icon => icon.Instance.Name == "warning");
        Assert.NotNull(warning.QuerySelector("svg"));
        AssertBanner(cut, "trip", "Update your trip details and travel dates.");
    }

    [Fact]
    public void ShareTrip_HasContextualBannerAndSingleReturnActionNavigatesToTrip()
    {
        var trip = RegisterTrip();
        var cut = Render<TripPlanner.Web.Components.Pages.Trips.TripSharing>(parameters => parameters.Add(page => page.TripId, trip.TripId));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".card-footer button")));
        Assert.Equal("Share trip", cut.Find(".entry-page-heading h1").TextContent);
        AssertBanner(cut, "share", "Manage who can view or edit this trip. Changes save automatically.");
        Assert.Equal(["Home", "Trips", trip.Name, "Share trip"], cut.FindAll(".breadcrumb-item").Select(item => item.TextContent.Trim()));
        var actions = cut.FindAll(".card-footer button");
        var back = Assert.Single(actions);
        Assert.Equal("Return", back.TextContent.Trim());
        Assert.Contains("btn-outline-secondary", back.ClassList);
        Assert.Equal("Return to the trip; sharing changes are saved automatically", back.GetAttribute("title"));
        Assert.Equal("Return to the trip", back.GetAttribute("aria-label"));
        Assert.Equal("back", cut.FindComponents<ActionIcon>().Single(icon => icon.Instance.Name == "back").Instance.Name);
        Assert.Equal("true", back.QuerySelector("svg")!.GetAttribute("aria-hidden"));
        Assert.Empty(cut.FindAll("#share-save, #share-cancel"));

        back.Click();

        Assert.EndsWith($"/trips/{trip.TripId:D}", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void ItemColorSelection_MarksEntryDirty()
    {
        var trip = RegisterTrip();
        var cut = Render<TrackedItemEntry>(parameters => parameters
            .Add(page => page.TripId, trip.TripId)
            .Add(page => page.ItemId, trip.TrackedItems[0].TrackedItemId));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#item-color")));
        Assert.False(cut.FindComponent<EntryNavigationGuard>().Instance.HasUnsavedChanges);

        cut.Find("#item-color").Click();
        cut.FindAll(".item-color-option").Single(option => option.TextContent.Trim().Equals("purple", StringComparison.OrdinalIgnoreCase)).Click();

        Assert.True(cut.FindComponent<EntryNavigationGuard>().Instance.HasUnsavedChanges);
    }

    private TripDetail RegisterTrip(bool assigned = true)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var tripId = Guid.NewGuid();
        var start = new DateTime(2026, 7, 14, 9, 0, 0);
        var leg = TrackedItemFormTestData.Leg(start, start.AddDays(6), legKind: "stay", title: "Berlin stay") with { TripId = tripId };
        var item = TrackedItemFormTestData.Item(assigned ? leg.TripLegId : null, start, start.AddHours(1)) with { TripId = tripId };
        var trip = new TripDetail(tripId, "Summer trip", null, new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 20),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [leg], [item]);
        var api = new StubTripApiClient();
        api.Details[tripId] = trip;
        Services.AddSingleton<TripPlanner.Web.Features.Trips.ITripApiClient>(api);
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
        Services.AddSingleton<IProfileApiClient>(new RecordingProfileApiClient(ProfileTestData.CompleteProfile(
            personalization: new(null, null, null, null, new Dictionary<string, string> { ["activity"] = "purple" }))));
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
        Services.AddSingleton<IFavoriteDestinationApiClient>(new RecordingFavoriteDestinationApiClient());
        return trip;
    }

    [Theory]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, true)]
    public async Task SuccessfulLegOrItemMutation_ReturnsAfterGuardIsClean(bool isLeg, bool delete, bool delayed, bool dirty)
    {
        var trip = RegisterTrip();
        var api = (StubTripApiClient)Services.GetRequiredService<TripPlanner.Web.Features.Trips.ITripApiClient>();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task Mutate()
        {
            calls++;
            return delayed ? completion.Task : Task.CompletedTask;
        }
        api.LegMutation = Mutate;
        api.ItemMutation = Mutate;
        var nav = Services.GetRequiredService<NavigationManager>();
        var destination = EntryPageUrls.TripDetails(trip.TripId, "timeline", new DateOnly(2026, 7, 16), tab: "details");
        var entryUrl = isLeg
            ? EntryPageUrls.LegEdit(trip.TripId, trip.Legs[0].TripLegId, destination)
            : EntryPageUrls.ItemEdit(trip.TripId, trip.TrackedItems[0].TrackedItemId, destination);
        nav.NavigateTo(entryUrl);
        var originalUri = nav.Uri;
        IRenderedComponent<IComponent> cut = isLeg
            ? Render<TripLegEntry>(parameters => parameters.Add(page => page.TripId, trip.TripId).Add(page => page.LegId, trip.Legs[0].TripLegId))
            : Render<TrackedItemEntry>(parameters => parameters.Add(page => page.TripId, trip.TripId).Add(page => page.ItemId, trip.TrackedItems[0].TrackedItemId));
        var prefix = isLeg ? "leg" : "item";
        var guard = cut.FindComponent<EntryNavigationGuard>().Instance;
        if (dirty) await cut.InvokeAsync(guard.MarkDirtyAsync);
        Assert.Equal(dirty, guard.HasUnsavedChanges);
        if (delete) cut.Find($"#{prefix}-delete").Click();

        var mutation = delete
            ? cut.Find($"#{prefix}-delete-confirm").ClickAsync(new())
            : cut.Find("form").SubmitAsync();
        if (delayed)
        {
            cut.WaitForAssertion(() => Assert.True(guard.IsSubmitting));
            await cut.InvokeAsync(() => nav.NavigateTo("/trips"));
            Assert.Equal(originalUri, nav.Uri);
            Assert.False(await guard.CanNavigateAwayAsync());
            completion.SetResult();
        }
        await mutation;

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(nav.ToAbsoluteUri(destination).AbsoluteUri, nav.Uri);
            Assert.False(guard.HasUnsavedChanges);
            Assert.False(guard.IsSubmitting);
        });
        Assert.Equal(1, calls);
        Assert.DoesNotContain(JSInterop.Invocations, invocation => invocation.Identifier == "confirm");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedLegOrItemDeletion_PreservesDirtyGuardAndAllowsRetry(bool isLeg)
    {
        var trip = RegisterTrip();
        var api = (StubTripApiClient)Services.GetRequiredService<TripPlanner.Web.Features.Trips.ITripApiClient>();
        Task Refuse() => Task.FromException(new InvalidOperationException("Delete refused."));
        api.LegMutation = Refuse;
        api.ItemMutation = Refuse;
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(isLeg
            ? EntryPageUrls.LegEdit(trip.TripId, trip.Legs[0].TripLegId)
            : EntryPageUrls.ItemEdit(trip.TripId, trip.TrackedItems[0].TrackedItemId));
        var originalUri = nav.Uri;
        IRenderedComponent<IComponent> cut = isLeg
            ? Render<TripLegEntry>(parameters => parameters.Add(page => page.TripId, trip.TripId).Add(page => page.LegId, trip.Legs[0].TripLegId))
            : Render<TrackedItemEntry>(parameters => parameters.Add(page => page.TripId, trip.TripId).Add(page => page.ItemId, trip.TrackedItems[0].TrackedItemId));
        var guard = cut.FindComponent<EntryNavigationGuard>().Instance;
        await cut.InvokeAsync(guard.MarkDirtyAsync);
        var prefix = isLeg ? "leg" : "item";
        cut.Find($"#{prefix}-delete").Click();
        cut.Find(".modal .btn-outline-secondary").Click();
        Assert.Empty(cut.FindAll(".modal"));
        Assert.True(guard.HasUnsavedChanges);
        Assert.Equal(originalUri, nav.Uri);

        cut.Find($"#{prefix}-delete").Click();
        await cut.Find($"#{prefix}-delete-confirm").ClickAsync(new());

        Assert.Contains("Delete refused.", cut.Find(".modal").TextContent);
        Assert.False(cut.Find($"#{prefix}-delete-confirm").HasAttribute("disabled"));
        Assert.True(guard.HasUnsavedChanges);
        Assert.False(guard.IsSubmitting);
        Assert.Equal(originalUri, nav.Uri);
        Assert.False(await guard.CanNavigateAwayAsync());

        api.LegMutation = () => Task.CompletedTask;
        api.ItemMutation = () => Task.CompletedTask;
        await cut.Find($"#{prefix}-delete-confirm").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.NotEqual(originalUri, nav.Uri));
        Assert.False(guard.HasUnsavedChanges);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task SuccessfulTripSave_ReturnsAfterGuardIsClean(bool edit, bool explicitReturn, bool delayed)
    {
        var trip = RegisterTrip();
        var api = (StubTripApiClient)Services.GetRequiredService<TripPlanner.Web.Features.Trips.ITripApiClient>();
        var response = new CreateTripResponse(trip.TripId, trip.Name, trip.Description, trip.StartDate, trip.EndDate);
        var completion = new TaskCompletionSource<CreateTripResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.TripMutation = () => delayed ? completion.Task : Task.FromResult(response);
        var nav = Services.GetRequiredService<NavigationManager>();
        var returnUrl = "/trips?page=2&pageSize=24";
        nav.NavigateTo(edit ? EntryPageUrls.TripEdit(trip.TripId, explicitReturn ? returnUrl : null)
            : explicitReturn ? EntryPageUrls.TripCreate(returnUrl) : "/trips/new");
        var originalUri = nav.Uri;
        IRenderedComponent<IComponent> cut = edit
            ? Render<EditTrip>(parameters => parameters.Add(page => page.TripId, trip.TripId))
            : Render<NewTrip>();
        cut.Find("#name").Change("Saved trip");
        var guard = cut.FindComponent<EntryNavigationGuard>().Instance;
        await cut.InvokeAsync(guard.MarkDirtyAsync);

        var save = cut.Find("form").SubmitAsync();
        if (delayed)
        {
            cut.WaitForAssertion(() => Assert.True(guard.IsSubmitting));
            await cut.InvokeAsync(() => nav.NavigateTo("/trips"));
            Assert.Equal(originalUri, nav.Uri);
            completion.SetResult(response);
        }
        await save;

        var destination = explicitReturn ? returnUrl : $"/trips/{trip.TripId:D}";
        cut.WaitForAssertion(() => Assert.Equal(nav.ToAbsoluteUri(destination).AbsoluteUri, nav.Uri));
        Assert.False(guard.HasUnsavedChanges);
        Assert.False(guard.IsSubmitting);
        Assert.DoesNotContain(JSInterop.Invocations, invocation => invocation.Identifier == "confirm");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task SuccessfulFavoriteMutation_ReturnsAfterGuardIsClean(string operation)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var favorite = new FavoriteDestinationDto(Guid.NewGuid(), "Museum", "Museum address", null, null, null, null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var api = new RecordingFavoriteDestinationApiClient([favorite])
        {
            UpdateHandler = (_, _) => Task.FromResult(new FavoriteDestinationMutationResult(favorite, null, null)),
            DeleteHandler = _ => Task.FromResult(true)
        };
        Services.AddSingleton<IFavoriteDestinationApiClient>(api);
        var nav = Services.GetRequiredService<NavigationManager>();
        var destination = EntryPageUrls.Favorites("museum");
        nav.NavigateTo(operation == "create" ? EntryPageUrls.FavoriteCreate(destination)
            : EntryPageUrls.FavoriteEdit(favorite.FavoriteDestinationId, destination));
        var cut = Render<FavoriteEntry>(parameters => parameters.Add(page => page.FavoriteId,
            operation == "create" ? null : favorite.FavoriteDestinationId));
        cut.Find("#favorite-name").Change("Updated museum");
        cut.Find("#favorite-address").Change("Updated museum address");
        var guard = cut.FindComponent<EntryNavigationGuard>().Instance;
        await cut.InvokeAsync(guard.MarkDirtyAsync);

        if (operation == "delete")
        {
            cut.Find("#favorite-delete").Click();
            await cut.Find(".modal .btn-danger").ClickAsync(new());
        }
        else
        {
            await cut.Find("form").SubmitAsync();
        }

        cut.WaitForAssertion(() => Assert.Equal(nav.ToAbsoluteUri(destination).AbsoluteUri, nav.Uri));
        Assert.False(guard.HasUnsavedChanges);
        Assert.False(guard.IsSubmitting);
        Assert.DoesNotContain(JSInterop.Invocations, invocation => invocation.Identifier == "confirm");
    }

    private static void AssertFooter<TComponent>(IRenderedComponent<TComponent> cut) where TComponent : IComponent
    {
        Assert.NotNull(cut.Find("header.entry-page-heading + .entry-form-card"));
        Assert.Empty(cut.FindAll(".entry-form-card > .card-header"));
        Assert.Empty(cut.FindAll(".card-body button[type='submit']"));
        var actions = cut.Find(".card-footer .entry-form-actions");
        Assert.Contains("justify-content-start", actions.ClassList);
        Assert.Contains("w-100", actions.ClassList);
        Assert.Contains("gap-3", actions.ClassList);
        Assert.Equal("submit", actions.Children[0].GetAttribute("type"));
        Assert.Contains("btn-primary", actions.Children[0].ClassList);
        foreach (var delete in actions.QuerySelectorAll(".btn-outline-danger"))
        {
            Assert.Equal(actions.LastElementChild, delete);
            Assert.Contains("ms-auto", delete.ClassList);
        }
    }

    private static void AssertBanner<TComponent>(IRenderedComponent<TComponent> cut, string icon, string subtitle) where TComponent : class, IComponent
    {
        Assert.NotNull(cut.Find("header.entry-page-heading + .entry-form-card"));
        Assert.Empty(cut.FindAll(".entry-form-card > .card-header"));
        Assert.Equal(subtitle, cut.Find(".entry-page-subtitle").TextContent);
        Assert.Equal(icon, cut.FindComponent<EntryPageShell>().Instance.Icon);
        Assert.Equal("true", cut.Find(".entry-page-icon svg").GetAttribute("aria-hidden"));
    }

    [Fact]
    public async Task Guard_TracksInputAsDirtyAndCanBeExplicitlyClearedAfterSuccess()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var dirty = false;
        var cut = Render<EntryNavigationGuard>(parameters => parameters
            .Add(component => component.IsDirtyChanged, value => dirty = value)
            .AddChildContent("<input aria-label='entry field' />"));

        cut.Find("input").Input("unsaved");
        Assert.True(dirty);
        await cut.Instance.MarkCleanAsync();
        Assert.False(dirty);
    }

    [Fact]
    public async Task Guard_CustomDirtyNotificationAndSubmittingStateProtectNavigation()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        var dirty = false;
        var cut = Render<EntryNavigationGuard>(parameters => parameters
            .Add(component => component.IsDirtyChanged, value => dirty = value)
            .Add(component => component.IsSubmitting, true));

        await cut.Instance.MarkDirtyAsync();

        Assert.True(dirty);
        Assert.True(cut.Instance.HasUnsavedChanges);
        Assert.False(await cut.Instance.CanNavigateAwayAsync());
        Assert.DoesNotContain(JSInterop.Invocations, invocation => invocation.Identifier == "confirm");
    }

    [Fact]
    public async Task Guard_RequiresConfirmationForDirtyNavigationAndDoesNotPromptWhenClean()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        JSInterop.Setup<bool>("confirm", "You have unsaved changes. Leave this page and discard them?").SetResult(false);
        var cut = Render<EntryNavigationGuard>();

        await cut.Instance.MarkDirtyAsync();
        Assert.False(await cut.Instance.CanNavigateAwayAsync());
        Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "confirm");

        await cut.Instance.MarkCleanAsync();
        Assert.True(await cut.Instance.CanNavigateAwayAsync());
        Assert.Single(JSInterop.Invocations, invocation => invocation.Identifier == "confirm");
    }

    [Theory]
    [InlineData(EntryPageShell.EntryLoadState.MissingOrDenied, "/trips", "This entry is unavailable")]
    [InlineData(EntryPageShell.EntryLoadState.Error, "/favorites", "We couldn't load this page")]
    public void Shell_ShowsSafeFailureStateWithoutRenderingTheForm(EntryPageShell.EntryLoadState state, string backHref, string message)
    {
        var cut = Render<EntryPageShell>(parameters => parameters
            .Add(component => component.Title, "Edit entry")
            .Add(component => component.LoadState, state)
            .Add(component => component.SafeBackHref, backHref)
            .Add(component => component.ChildContent, builder => builder.AddMarkupContent(0, "<input id='must-not-render' />")));

        Assert.Contains(message, cut.Markup, StringComparison.Ordinal);
        Assert.Equal(backHref, cut.Find("a.btn").GetAttribute("href"));
        Assert.Empty(cut.FindAll("#must-not-render"));
    }

    [Fact]
    public void TripLegEntry_RejectsLegFromAnotherOrMissingParentOnColdLoad()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var tripId = Guid.NewGuid();
        var api = new StubTripApiClient();
        api.Details[tripId] = new TripDetail(tripId, "Test trip", null, new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 20),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<TripLegDto>(), Array.Empty<TrackedItemDto>(), TripAccessLevel.Owner, true);
        Services.AddSingleton<TripPlanner.Web.Features.Trips.ITripApiClient>(api);

        var cut = Render<TripLegEntry>(parameters => parameters.Add(component => component.TripId, tripId).Add(component => component.LegId, Guid.NewGuid()));

        cut.WaitForAssertion(() => Assert.Contains("This entry is unavailable", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public void RoutableEntryPages_DeclareAuthorizationAtTheRouteComponent()
    {
        Assert.NotEmpty(typeof(TripLegEntry).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.NotEmpty(typeof(TrackedItemEntry).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.NotEmpty(typeof(EditTrip).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.NotEmpty(typeof(TripPlanner.Web.Components.Pages.Trips.TripSharing).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
    }
}
