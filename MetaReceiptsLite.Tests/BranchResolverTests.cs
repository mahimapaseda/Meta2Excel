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
        [InlineData("Gamp - OCT ad set 02", "Kur", "campaign", "Gampaha")]
        [InlineData("Gampa - oct ad set 01", "Amp", "campaign", "Gampaha")]
        [InlineData("Anura - OCT ad set 02", "Gam", "campaign", "Anuradhapura")]
        [InlineData("Kiri - oct ad set 01", "Wen", "campaign", "Kiribathgoda")]
        [InlineData("Ambal - OCT ad set 02", "Kur", "campaign", "Ambalangoda")]
        [InlineData("Nuwara - oct ad set 01", "Gam", "campaign", "Nuwara Eliya")]
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

        [Theory]
        [InlineData("Gam", "Gampaha")]
        [InlineData("Gamp", "Gampaha")]
        [InlineData("Gampa", "Gampaha")]
        [InlineData("Gampaha", "Gampaha")]
        [InlineData("Kurun", "Kurunegala")]
        [InlineData("Kurune", "Kurunegala")]
        [InlineData("Ampar", "Ampara")]
        [InlineData("Band", "Bandarawela")]
        [InlineData("Kuli", "Kuliyapitiya")]
        [InlineData("Wenn", "Wennappuwa")]
        public void LookupName_LongerAliases_MapToFullBranch(string alias, string expected)
        {
            Assert.Equal(expected, BranchResolver.LookupName(alias));
        }

        [Fact]
        public void ExtractAdSetCode_FirstWord()
        {
            Assert.Equal("Kuru", BranchResolver.ExtractAdSetCode("Kuru - OCT ad set 02"));
            Assert.Equal("neg", BranchResolver.ExtractAdSetCode("neg- oct ad set 01"));
            Assert.Equal("Gamp", BranchResolver.ExtractAdSetCode("Gamp - OCT ad set 02"));
        }
    }
}
