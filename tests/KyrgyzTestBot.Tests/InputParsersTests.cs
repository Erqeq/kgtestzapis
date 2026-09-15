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

    private static readonly DateOnly FirstDate = new(2026, 9, 16);

    [Fact]
    public void Dates_ParsesBothFormatsFromFirstDate() =>
        Assert.Equal(new[] { new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 22) }, InputParsers.Dates("16.09.2026, 2026-09-22", FirstDate));

    [Fact]
    public void Dates_AnyMeansEmptyList() =>
        Assert.Empty(InputParsers.Dates(InputParsers.Any, FirstDate)!);

    [Theory]
    [InlineData("завтра")]
    [InlineData("31.02.2026")]
    [InlineData("21.09.2026 потом")]
    [InlineData("15.09.2026")]
    [InlineData("21.09.2026 15.09.2026")]
    public void Dates_RejectsInvalidAndEarlierThanFirstDate(string input) =>
        Assert.Null(InputParsers.Dates(input, FirstDate));

    [Fact]
    public void Shifts_MapsButtons()
    {
        Assert.Equal(new[] { Shift.Morning }, InputParsers.Shifts("09:00"));
        Assert.Equal(new[] { Shift.Afternoon }, InputParsers.Shifts("13:00"));
        Assert.Equal(new[] { Shift.Morning, Shift.Afternoon }, InputParsers.Shifts(InputParsers.Any));
        Assert.Null(InputParsers.Shifts("10:00"));
    }
}
