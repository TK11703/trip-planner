using AngleSharp.Dom;
using Bunit;

namespace TripPlanner.Web.Tests.Infrastructure;

public static class MarkupAssertionHelpers
{
    public static IElement ShouldHaveElement(this IRenderedFragment fragment, string cssSelector)
        => fragment.Find(cssSelector);

    public static void ShouldContainText(this IRenderedFragment fragment, string expected)
        => Assert.Contains(expected, fragment.Markup, StringComparison.OrdinalIgnoreCase);

    public static void ShouldHaveClass(this IElement element, string className)
        => Assert.Contains(element.ClassList, value => string.Equals(value, className, StringComparison.Ordinal));
}
