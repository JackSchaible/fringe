namespace Fringe.Data.Tests;

/// <summary>Unit tests for <see cref="VenueAddressHasher"/>.</summary>
public sealed class VenueAddressHasherTests
{
    [Fact]
    public void ComputeHashSameInputsProduceSameHash()
    {
        string first = VenueAddressHasher.ComputeHash(1, "123 Main St", "T5J 2R7");
        string second = VenueAddressHasher.ComputeHash(1, "123 Main St", "T5J 2R7");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeHashDifferentVenueNumberProducesDifferentHash()
    {
        string first = VenueAddressHasher.ComputeHash(1, "123 Main St", "T5J 2R7");
        string second = VenueAddressHasher.ComputeHash(2, "123 Main St", "T5J 2R7");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeHashDifferentAddressProducesDifferentHash()
    {
        string first = VenueAddressHasher.ComputeHash(1, "123 Main St", "T5J 2R7");
        string second = VenueAddressHasher.ComputeHash(1, "456 Main St", "T5J 2R7");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeHashDifferentPostalCodeProducesDifferentHash()
    {
        string first = VenueAddressHasher.ComputeHash(1, "123 Main St", "T5J 2R7");
        string second = VenueAddressHasher.ComputeHash(1, "123 Main St", "T5J 3X1");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeHashAddressWhitespaceAndCaseAreNormalized()
    {
        string first = VenueAddressHasher.ComputeHash(1, "123   Main   St", "T5J 2R7");
        string second = VenueAddressHasher.ComputeHash(1, "123 main st", "T5J 2R7");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeHashAddressLeadingAndTrailingWhitespaceAreNormalized()
    {
        string first = VenueAddressHasher.ComputeHash(1, "  123 Main St  ", "T5J 2R7");
        string second = VenueAddressHasher.ComputeHash(1, "123 Main St", "T5J 2R7");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeHashPostalCodeSpacingAndCaseAreNormalized()
    {
        string first = VenueAddressHasher.ComputeHash(1, "123 Main St", "T5J2R7");
        string second = VenueAddressHasher.ComputeHash(1, "123 Main St", "t5j 2r7");

        Assert.Equal(first, second);
    }

    [Fact]
    public void NormalizeAddressCollapsesWhitespaceAndUppercases()
    {
        string result = VenueAddressHasher.NormalizeAddress("  123   main st  ");

        Assert.Equal("123 MAIN ST", result);
    }

    [Fact]
    public void NormalizePostalCodeStripsWhitespaceAndUppercases()
    {
        string result = VenueAddressHasher.NormalizePostalCode("t5j 2r7");

        Assert.Equal("T5J2R7", result);
    }
}
