using MetaReceiptsLite;
using Xunit;

namespace MetaReceiptsLite.Tests
{
    public class BranchResolverTests
    {
        [Theory]
        [InlineData("kur - oct ad set 01", "Kur", "campaign", "Kurunegala")]
        [InlineData("Kuru - OCT ad set 02", "Amp", "campaign", "Kurunegala")]
        [InlineData("KUR - oct ad set 01", "Wen", "campaign", "Kurunegala")]
        [InlineData("amp - oct ad set 01", "Kur", "campaign", "Ampara")]
        [InlineData("neg- oct ad set 01", "Gam", "campaign", "Negombo")]
        public void ResolveName_UsesAdSetPrefix_NotCampaignCode(
            string adSet, string campaignCode, string campaignName, string expectedBranch)
        {
            var it = new LineItem
            {
                AdSet = adSet,
                CampaignCode = campaignCode,
                CampaignName = campaignName
            };
            Assert.Equal(expectedBranch, BranchResolver.ResolveName(it));
        }

        [Fact]
        public void ResolveName_FallsBackToCampaign_WhenAdSetEmpty()
        {
            var it = new LineItem { CampaignCode = "Kur", AdSet = "" };
            Assert.Equal("Kurunegala", BranchResolver.ResolveName(it));
        }

        [Fact]
        public void LookupName_KurAndKuru_BothKurunegala()
        {
            Assert.Equal("Kurunegala", BranchResolver.LookupName("Kur"));
            Assert.Equal("Kurunegala", BranchResolver.LookupName("Kuru"));
            Assert.Equal("Kurunegala", BranchResolver.LookupName("kuru"));
        }

        [Fact]
        public void ExtractAdSetCode_FirstWord()
        {
            Assert.Equal("Kuru", BranchResolver.ExtractAdSetCode("Kuru - OCT ad set 02"));
            Assert.Equal("neg", BranchResolver.ExtractAdSetCode("neg- oct ad set 01"));
        }
    }
}
