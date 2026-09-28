using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using TripPlanner.Contracts.TripDataChat;

namespace TripPlanner.Web.Features.TripDataChat;

public sealed record TripChatSessionMessage(
    string Role,
    string Text,
    IReadOnlyList<TripDataChatCitation>? Citations = null);

public sealed record TripChatSessionSnapshot(
    IReadOnlyList<TripChatSessionMessage> Messages,
    bool IsOpen,
    double ScrollTop)
{
    public static TripChatSessionSnapshot Empty { get; } = new([], false, 0);
}

public sealed class TripChatSessionStore(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task<TripChatSessionSnapshot> LoadAsync(string epoch)
    {
        try
        {
            _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/tripDataChat.js");
            return await _module.InvokeAsync<TripChatSessionSnapshot>("load", epoch) ?? TripChatSessionSnapshot.Empty;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or OperationCanceledException)
        {
            return TripChatSessionSnapshot.Empty;
        }
    }

    public async Task SaveAsync(string epoch, TripChatSessionSnapshot snapshot)
    {
        try
        {
            _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/tripDataChat.js");
            await _module.InvokeVoidAsync("save", epoch, snapshot);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or OperationCanceledException)
        {
        }
    }

    public async Task ClearAsync(string epoch)
    {
        try
        {
            _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/tripDataChat.js");
            await _module.InvokeVoidAsync("clear", epoch);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or OperationCanceledException)
        {
        }
    }

    public async Task FocusAsync(ElementReference element)
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("focus", element);
        }
    }

    public async Task SyncPaneAsync(ElementReference element, bool isOpen)
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("syncPane", element, isOpen);
        }
    }

    public async Task RestoreScrollAsync(ElementReference element, double scrollTop)
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("restoreScroll", element, scrollTop);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    public async Task<double> ReadScrollAsync(ElementReference element)
    {
        if (_module is null) return 0;
        return await _module.InvokeAsync<double>("readScroll", element);
    }
}