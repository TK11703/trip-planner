namespace TripPlanner.Web.Features.EntryPages;

public sealed record BreadcrumbItem(string Label, string? Href = null, bool IsCurrent = false);
