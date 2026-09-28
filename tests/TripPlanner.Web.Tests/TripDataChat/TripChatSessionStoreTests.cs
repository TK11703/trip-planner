using Bunit;
using TripPlanner.Contracts.TripDataChat;
using TripPlanner.Web.Features.TripDataChat;

namespace TripPlanner.Web.Tests.TripDataChat;

public sealed class TripChatSessionStoreTests : BunitContext
{
    [Fact]
    public async Task LoadRestoresOnlySnapshotReturnedForTheCurrentEpoch()
    {
        const string epoch = "opaque-epoch-2";
        var expected = new TripChatSessionSnapshot(
            [new TripChatSessionMessage("user", "Find my hotel"), new TripChatSessionMessage("assistant", "The hotel is in Seattle.")],
            true,
            148.5);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./js/tripDataChat.js");
        module.Setup<TripChatSessionSnapshot>("load", epoch).SetResult(expected);
        var store = new TripChatSessionStore(JSInterop.JSRuntime);

        var actual = await store.LoadAsync(epoch);

        Assert.Equal(expected, actual);
        await store.DisposeAsync();
    }

    [Fact]
    public async Task LoadFailureFallsBackToAnEmptySession()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/tripDataChat.js");
        var store = new TripChatSessionStore(JSInterop.JSRuntime);

        var actual = await store.LoadAsync("opaque-epoch-3");

        Assert.Equal(TripChatSessionSnapshot.Empty, actual);
        await store.DisposeAsync();
    }
}