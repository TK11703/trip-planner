using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace TripPlanner.Web.Tests.Infrastructure;

public static class MarkupAssertionHelpers
{
    public static IElement ShouldHaveElement<TComponent>(this IRenderedComponent<TComponent> fragment, string cssSelector)
        where TComponent : IComponent
        => fragment.Find(cssSelector);

    public static void ShouldContainText<TComponent>(this IRenderedComponent<TComponent> fragment, string expected)
        where TComponent : IComponent
        => Assert.Contains(expected, fragment.Markup, StringComparison.OrdinalIgnoreCase);

    public static void ShouldHaveClass(this IElement element, string className)
        => Assert.Contains(element.ClassList, value => string.Equals(value, className, StringComparison.Ordinal));
}
