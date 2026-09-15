using KyrgyzTestBot.Applicants;
using KyrgyzTestBot.Conversation;

namespace KyrgyzTestBot.Tests;

public class InputParsersTests
{
    [Theory]
    [InlineData("0700123456")]
    [InlineData("700123456")]
    [InlineData("+996 700 123 456")]
    [InlineData("996-700-12-34-56")]
    public void Phone_NormalizesToMiniAppFormat(string input) =>
        Assert.Equal("0700123456", InputParsers.Phone(input));

    [Theory]
    [InlineData("12345")]
    [InlineData("+7 700 123 45 67")]
    [InlineData("телефон")]
    public void Phone_RejectsInvalid(string input) =>
        Assert.Null(InputParsers.Phone(input));

    [Theory]
    [InlineData("12345678901234", true)]
    [InlineData("1234567890123", false)]
    [InlineData("1234567890123a", false)]
    public void Inn_Requires14Digits(string input, bool valid) =>
        Assert.Equal(valid, InputParsers.Inn(input) is not null);

    [Fact]
    public void FullName_RequiresTwoWordsAndCollapsesSpaces()
    {
        Assert.Equal("Асанов Асан Асанович", InputParsers.FullName("Асанов   Асан Асанович"));
        Assert.Null(InputParsers.FullName("Асан"));
    }

    [Fact]
    public void Dates_ParsesBothFormats() =>
        Assert.Equal(new[] { new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22) }, InputParsers.Dates("21.09.2026, 2026-09-22"));

    [Fact]
    public void Dates_AnyMeansEmptyList() =>
        Assert.Empty(InputParsers.Dates(InputParsers.Any)!);

    [Theory]
    [InlineData("завтра")]
    [InlineData("31.02.2026")]
    [InlineData("21.09.2026 потом")]
    public void Dates_RejectsInvalid(string input) =>
        Assert.Null(InputParsers.Dates(input));

    [Fact]
    public void Shifts_MapsButtons()
    {
        Assert.Equal(new[] { Shift.Morning }, InputParsers.Shifts("09:00"));
        Assert.Equal(new[] { Shift.Afternoon }, InputParsers.Shifts("13:00"));
        Assert.Equal(new[] { Shift.Morning, Shift.Afternoon }, InputParsers.Shifts(InputParsers.Any));
        Assert.Null(InputParsers.Shifts("10:00"));
    }
}
