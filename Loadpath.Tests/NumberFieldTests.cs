using System.Globalization;
using Loadpath.Controls;
using Xunit;

namespace Loadpath.Tests;

/// <summary>Inspector numbers read and write like the labels beside them: in the current culture.</summary>
public class NumberFieldTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void A_value_shows_with_the_cultures_decimal_separator()
    {
        Assert.Equal("1,500", NumberField.Format(1.5, 3, French));
        Assert.Equal("1.500", NumberField.Format(1.5, 3, English));
        // The labels beside the fields format the same way.
        Assert.Equal($"{1.5:0.000}", NumberField.Format(1.5, 3, CultureInfo.CurrentCulture));
    }

    [Theory]
    [InlineData("fr-FR", "1,5", 1.5)]
    [InlineData("fr-FR", " -2,25 ", -2.25)]
    [InlineData("fr-FR", "−2,25", -2.25)]
    [InlineData("en-US", "1.5", 1.5)]
    [InlineData("en-US", "−0.75", -0.75)]
    public void Typed_text_reads_in_the_current_culture(string culture, string text, double expected)
    {
        Assert.True(NumberField.TryParse(text, CultureInfo.GetCultureInfo(culture), out var value));
        Assert.Equal(expected, value, 12);
    }

    [Theory]
    [InlineData("en-US", "1,5")]
    [InlineData("fr-FR", "1.5")]
    [InlineData("en-US", "abc")]
    [InlineData("en-US", "NaN")]
    [InlineData("en-US", "Infinity")]
    [InlineData("en-US", "1e999")]
    public void Text_that_is_not_a_finite_number_in_the_culture_is_refused(string culture, string text)
    {
        Assert.False(NumberField.TryParse(text, CultureInfo.GetCultureInfo(culture), out _));
    }
}
