using System;
using FluentAssertions;
using Jellyfin.Plugin.LocalRecs.Utilities;
using Xunit;

namespace Jellyfin.Plugin.LocalRecs.Tests.Unit
{
    public class WeightCalculatorTests
    {
        [Fact]
        public void ExponentialDecay_ZeroDays_ReturnsOne()
        {
            var result = WeightCalculator.ExponentialDecay(daysSince: 0, halfLifeDays: 365);

            result.Should().BeApproximately(1.0f, 0.0001f);
        }

        [Fact]
        public void ExponentialDecay_AtHalfLife_ReturnsHalf()
        {
            var result = WeightCalculator.ExponentialDecay(daysSince: 365, halfLifeDays: 365);

            result.Should().BeApproximately(0.5f, 0.0001f);
        }

        [Fact]
        public void ExponentialDecay_TwoHalfLives_ReturnsQuarter()
        {
            var result = WeightCalculator.ExponentialDecay(daysSince: 730, halfLifeDays: 365);

            result.Should().BeApproximately(0.25f, 0.0001f);
        }

        [Fact]
        public void ExponentialDecay_NegativeDays_ThrowsArgumentException()
        {
            Action act = () => WeightCalculator.ExponentialDecay(daysSince: -1, halfLifeDays: 365);

            act.Should().Throw<ArgumentException>().WithParameterName("daysSince");
        }

        [Fact]
        public void ExponentialDecay_ZeroHalfLife_ThrowsArgumentException()
        {
            Action act = () => WeightCalculator.ExponentialDecay(daysSince: 10, halfLifeDays: 0);

            act.Should().Throw<ArgumentException>().WithParameterName("halfLifeDays");
        }

        [Fact]
        public void ExponentialDecay_NegativeHalfLife_ThrowsArgumentException()
        {
            Action act = () => WeightCalculator.ExponentialDecay(daysSince: 10, halfLifeDays: -365);

            act.Should().Throw<ArgumentException>().WithParameterName("halfLifeDays");
        }

        [Fact]
        public void ApplyFavoriteBoost_IsFavorite_AppliesBoost()
        {
            var result = WeightCalculator.ApplyFavoriteBoost(baseWeight: 1.0f, isFavorite: true, favoriteBoost: 2.0f);

            result.Should().Be(2.0f);
        }

        [Fact]
        public void ApplyFavoriteBoost_NotFavorite_ReturnsBaseWeight()
        {
            var result = WeightCalculator.ApplyFavoriteBoost(baseWeight: 1.0f, isFavorite: false, favoriteBoost: 2.0f);

            result.Should().Be(1.0f);
        }

        [Fact]
        public void ApplyFavoriteBoost_NegativeBaseWeight_ThrowsArgumentException()
        {
            Action act = () => WeightCalculator.ApplyFavoriteBoost(baseWeight: -1.0f, isFavorite: true, favoriteBoost: 2.0f);

            act.Should().Throw<ArgumentException>().WithParameterName("baseWeight");
        }

        [Fact]
        public void ApplyFavoriteBoost_NegativeFavoriteBoost_ThrowsArgumentException()
        {
            Action act = () => WeightCalculator.ApplyFavoriteBoost(baseWeight: 1.0f, isFavorite: true, favoriteBoost: -2.0f);

            act.Should().Throw<ArgumentException>().WithParameterName("favoriteBoost");
        }

        [Fact]
        public void ApplyRecentWatchBoost_ZeroBoost_ReturnsDecay()
        {
            var result = WeightCalculator.ApplyRecentWatchBoost(decay: 0.5f, recentWatchBoost: 0.0f);

            result.Should().BeApproximately(0.5f, 0.0001f);
        }

        [Fact]
        public void ApplyRecentWatchBoost_RecentItem_AmplifiedByBoost()
        {
            // decay=1.0, boost=1.0: result = 1.0 * (1 + 1.0 * 1.0) = 2.0
            var result = WeightCalculator.ApplyRecentWatchBoost(decay: 1.0f, recentWatchBoost: 1.0f);

            result.Should().BeApproximately(2.0f, 0.0001f);
        }

        [Fact]
        public void ApplyRecentWatchBoost_OldItem_BoostNearZero()
        {
            // decay=0.01, boost=2.0: result = 0.01 * (1 + 2.0 * 0.01) ≈ 0.0102
            var result = WeightCalculator.ApplyRecentWatchBoost(decay: 0.01f, recentWatchBoost: 2.0f);

            result.Should().BeApproximately(0.0102f, 0.001f);
        }

        [Fact]
        public void ApplyRecentWatchBoost_NegativeDecay_ThrowsArgumentException()
        {
            Action act = () => WeightCalculator.ApplyRecentWatchBoost(decay: -0.1f, recentWatchBoost: 1.0f);

            act.Should().Throw<ArgumentException>().WithParameterName("decay");
        }

        [Fact]
        public void ApplyRecentWatchBoost_DecayAboveOne_ThrowsArgumentException()
        {
            Action act = () => WeightCalculator.ApplyRecentWatchBoost(decay: 1.1f, recentWatchBoost: 1.0f);

            act.Should().Throw<ArgumentException>().WithParameterName("decay");
        }

        [Fact]
        public void ApplyRecentWatchBoost_NegativeBoost_ThrowsArgumentException()
        {
            Action act = () => WeightCalculator.ApplyRecentWatchBoost(decay: 0.5f, recentWatchBoost: -1.0f);

            act.Should().Throw<ArgumentException>().WithParameterName("recentWatchBoost");
        }

        [Fact]
        public void ComputeCombinedWeight_FavoriteWithBoost_ReturnsCompoundedWeight()
        {
            // decay=0.5, recentWatchBoost=1.0: weight = 0.5*(1+1.0*0.5) = 0.75, then *2.0 = 1.5
            var result = WeightCalculator.ComputeCombinedWeight(
                daysSince: 365,
                halfLifeDays: 365,
                isFavorite: true,
                favoriteBoost: 2.0f,
                recentWatchBoost: 1.0f);

            result.Should().BeApproximately(1.5f, 0.0001f);
        }

        [Fact]
        public void ComputeCombinedWeight_NotFavoriteZeroBoost_ReturnsDecayOnly()
        {
            // decay=0.5, recentWatchBoost=0: weight = 0.5*(1+0*0.5) = 0.5, not favorite so *1 = 0.5
            var result = WeightCalculator.ComputeCombinedWeight(
                daysSince: 365,
                halfLifeDays: 365,
                isFavorite: false,
                favoriteBoost: 2.0f,
                recentWatchBoost: 0.0f);

            result.Should().BeApproximately(0.5f, 0.0001f);
        }

    }
}
