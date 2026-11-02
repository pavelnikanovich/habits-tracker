namespace HabitsTracker.Identity.Core.Tests;

// TimeZone reads the machine's IANA database, so these cases rely on its tzdata.
public class TimeZoneTests
{
    [Theory]
    [InlineData("Europe/Warsaw", "Europe/Warsaw", true)]
    [InlineData("America/Argentina/Buenos_Aires", "America/Argentina/Buenos_Aires", true)]
    [InlineData("UTC", "UTC", true)]
    [InlineData(null, "UTC", false)]
    [InlineData("", "UTC", false)]
    [InlineData("Mars/Olympus_Mons", "UTC", false)]
    // A Windows id: TimeZoneInfo finds it, but it is not an IANA id.
    [InlineData("Central European Standard Time", "UTC", false)]
    public void From_browser(string? browserId, string expectedId, bool expectedRecognized)
    {
        var zone = TimeZone.FromBrowser(browserId);

        Assert.Equal(expectedId, zone.Id);
        Assert.Equal(expectedRecognized, zone.Recognized);
    }

    [Theory]
    [InlineData("Europe/Warsaw", true)]
    [InlineData("UTC", false)]
    public void From_storage_keeps_a_known_zone(string id, bool recognized)
    {
        var zone = TimeZone.FromStorage(id, recognized);

        Assert.Equal(id, zone.Id);
        Assert.Equal(recognized, zone.Recognized);
    }
}
