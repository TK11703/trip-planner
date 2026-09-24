using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// The details a leg requires that a recognized draft may not carry.
///
/// A tracked item tolerates a missing end and a missing end zone; a trip leg does not, and a
/// travel leg additionally needs an origin, a destination, and a mode. Recognition frequently
/// returns none of these, so the gap is the common case rather than the exception.
///
/// Every check here names the specific detail at fault so the traveler can fill it in, rather
/// than inventing a value on their behalf (FR-028, FR-029, FR-030, FR-031). Nothing is written
/// when a gap is found (FR-032).
///
/// These run before <see cref="TripLegValidator"/> rather than inside it: the validator takes a
/// <see cref="CreateTripLegRequest"/> whose end and zones are non-nullable, so it cannot express
/// "absent" at all. Naming the gap first is what keeps the validator unchanged.
/// </summary>
internal static class LegConfirmationGaps
{
    /// <summary>
    /// Returns the first missing detail as a validation error, or null when the draft carries
    /// everything a leg needs. Ordered so the traveler is walked through the form top to bottom.
    /// </summary>
    public static ApiError? Find(ParsedItemDraftRecord draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Title))
            return ApiError.ValidationFailed("Title is required.", "title");

        if (draft.StartLocal is null)
            return ApiError.ValidationFailed("Start date and time is required.", "startLocal");

        if (string.IsNullOrWhiteSpace(draft.StartTimeZoneId))
            return ApiError.ValidationFailed("Start timezone is required.", "startTimeZoneId");

        // The two a leg demands and an item does not. Named separately because the email that
        // stated neither is the same email that stated no arrival time — telling the traveler
        // about only one of them would send them round the loop twice (FR-029).
        if (draft.EndLocal is null)
            return ApiError.ValidationFailed("End date and time is required for a trip leg.", "endLocal");

        if (string.IsNullOrWhiteSpace(draft.EndTimeZoneId))
            return ApiError.ValidationFailed("End timezone is required for a trip leg.", "endTimeZoneId");

        // A stay leg has no route, so the remaining checks apply only to travel.
        if (!IsTravel(draft))
            return null;

        if (string.IsNullOrWhiteSpace(draft.TransportationMode))
            return ApiError.ValidationFailed("Choose how you are traveling: flight, train, bus, boat, or car.", "transportationMode");

        if (!TransportationModes.IsValid(draft.TransportationMode))
            return ApiError.ValidationFailed("Choose how you are traveling: flight, train, bus, boat, or car.", "transportationMode");

        if (string.IsNullOrWhiteSpace(draft.Origin))
            return ApiError.ValidationFailed("Enter where this travel leg starts from.", "origin");

        if (string.IsNullOrWhiteSpace(draft.Destination))
            return ApiError.ValidationFailed("Enter where this travel leg arrives.", "destination");

        return null;
    }

    /// <summary>
    /// A draft confirmed as a leg is travel whenever it names a mode or a route. Only a draft
    /// that names none of them is treated as a stay.
    /// </summary>
    public static bool IsTravel(ParsedItemDraftRecord draft) =>
        !string.IsNullOrWhiteSpace(draft.TransportationMode)
        || !string.IsNullOrWhiteSpace(draft.Origin)
        || !string.IsNullOrWhiteSpace(draft.Destination);

    /// <summary>
    /// Builds the leg creation request from a draft that has already cleared <see cref="Find"/>,
    /// so every value read here is known to be present.
    ///
    /// The travel cost carries the recognized amount only. A leg has no currency of its own, so
    /// the recognized currency label stays on the draft and is never written here (FR-010).
    /// </summary>
    public static CreateTripLegRequest ToLegRequest(ParsedItemDraftRecord draft)
    {
        var isTravel = IsTravel(draft);
        return new CreateTripLegRequest(
            Title: draft.Title!,
            Origin: isTravel ? draft.Origin : null,
            Destination: isTravel ? draft.Destination : null,
            StartLocal: draft.StartLocal!.Value,
            StartTimeZoneId: draft.StartTimeZoneId!,
            EndLocal: draft.EndLocal!.Value,
            EndTimeZoneId: draft.EndTimeZoneId!,
            Notes: draft.Notes,
            LegKind: isTravel ? TripLegKinds.Travel : TripLegKinds.Stay,
            TransportationMode: isTravel ? TransportationModes.Normalize(draft.TransportationMode) : null,
            TravelCost: isTravel ? draft.TravelCost : null,
            ConfirmationCode: isTravel ? draft.ConfirmationCode : null);
    }
}
