namespace Fringe.Data.Tests;

/// <summary>Unit tests for <see cref="TransferMatrixHasher"/>.</summary>
public sealed class TransferMatrixHasherTests
{
    private static readonly (int VenueNumber, double Latitude, double Longitude)[] threeVenues =
    [
        (1, 53.5461, -113.4938),
        (2, 53.5183, -113.4926),
        (3, 53.5400, -113.4910)
    ];

    [Fact]
    public void ComputeHashSameInputsProduceSameHash()
    {
        string first = TransferMatrixHasher.ComputeHash(threeVenues);
        string second = TransferMatrixHasher.ComputeHash(threeVenues);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeHashIsOrderIndependent()
    {
        string first = TransferMatrixHasher.ComputeHash(threeVenues);
        string second = TransferMatrixHasher.ComputeHash([.. threeVenues.Reverse()]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeHashAddingAVenueChangesHash()
    {
        string before = TransferMatrixHasher.ComputeHash(threeVenues);
        string after = TransferMatrixHasher.ComputeHash([.. threeVenues, (4, 53.5500, -113.5000)]);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void ComputeHashRemovingAVenueChangesHash()
    {
        string before = TransferMatrixHasher.ComputeHash(threeVenues);
        string after = TransferMatrixHasher.ComputeHash(threeVenues[..2]);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void ComputeHashCoordinateChangeChangesHash()
    {
        (int, double, double) moved = (1, 53.9999, -113.4938);
        string before = TransferMatrixHasher.ComputeHash(threeVenues);
        string after = TransferMatrixHasher.ComputeHash([moved, threeVenues[1], threeVenues[2]]);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void ComputeHashSubMillimeterFloatingPointNoiseProducesSameHash()
    {
        (int, double, double) venue = (1, 53.5461, -113.4938);
        (int, double, double) venueWithNoise = (1, 53.5461000000001, -113.4938000000002);

        string first = TransferMatrixHasher.ComputeHash([venue]);
        string second = TransferMatrixHasher.ComputeHash([venueWithNoise]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeHashDifferentVenueNumberSameCoordinatesChangesHash()
    {
        string first = TransferMatrixHasher.ComputeHash([(1, 53.5461, -113.4938)]);
        string second = TransferMatrixHasher.ComputeHash([(2, 53.5461, -113.4938)]);

        Assert.NotEqual(first, second);
    }
}
