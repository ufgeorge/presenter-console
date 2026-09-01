using PresenterConsole.Desktop;
using Xunit;

namespace PresenterConsole.Sync.Tests;

public sealed class NotesTextBuilderTests
{
    [Fact]
    public void NoneHasNoPrefix()
    {
        Assert.Equal(
            string.Empty,
            NotesTextBuilder.BuildParagraphPrefix(0, -1, 8226, 1));
    }

    [Fact]
    public void UnnumberedUsesBulletCharacter()
    {
        Assert.Equal(
            "• ",
            NotesTextBuilder.BuildParagraphPrefix(1, -1, 8226, 1));
    }

    [Fact]
    public void NumberedUsesEachParagraphNumber()
    {
        Assert.Equal(
            "1. ",
            NotesTextBuilder.BuildParagraphPrefix(2, -1, 8226, 1));
        Assert.Equal(
            "2. ",
            NotesTextBuilder.BuildParagraphPrefix(2, -1, 8226, 2));
    }

    [Fact]
    public void InvisibleBulletHasNoPrefix()
    {
        Assert.Equal(
            string.Empty,
            NotesTextBuilder.BuildParagraphPrefix(2, 0, 8226, 1));
    }

    [Fact]
    public void PictureBulletHasNoPrefix()
    {
        Assert.Equal(
            string.Empty,
            NotesTextBuilder.BuildParagraphPrefix(3, -1, 8226, 1));
    }
}
