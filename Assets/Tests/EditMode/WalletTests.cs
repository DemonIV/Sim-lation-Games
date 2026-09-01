using NUnit.Framework;
using Sim.Core;

namespace Sim.Tests
{
    /// <summary>The purse: earning, affordability and the fact that a failed spend changes nothing.</summary>
    public class WalletTests
    {
        [Test]
        public void FreshWallet_StartsEmpty()
        {
            var w = new Wallet();
            Assert.AreEqual(0, w.Balance);
            Assert.AreEqual(0, w.LifetimeEarned);
        }

        [Test]
        public void StartingBalance_IsClampedToZero()
        {
            Assert.AreEqual(0, new Wallet(-500).Balance);
            Assert.AreEqual(120, new Wallet(120).Balance);
        }

        [Test]
        public void Earn_AddsToBalanceAndLifetimeButIgnoresNonPositive()
        {
            var w = new Wallet();
            w.Earn(300);
            w.Earn(200);
            Assert.AreEqual(500, w.Balance);
            Assert.AreEqual(500, w.LifetimeEarned);

            w.Earn(0);
            w.Earn(-999);
            Assert.AreEqual(500, w.Balance);
            Assert.AreEqual(500, w.LifetimeEarned);
        }

        [Test]
        public void TrySpend_DeductsWhenAffordable()
        {
            var w = new Wallet(500);
            Assert.IsTrue(w.TrySpend(200));
            Assert.AreEqual(300, w.Balance);

            // Spending the whole balance is allowed.
            Assert.IsTrue(w.TrySpend(300));
            Assert.AreEqual(0, w.Balance);
        }

        [Test]
        public void TrySpend_FailsAndDoesNotMutateOnInsufficientFunds()
        {
            var w = new Wallet(100);

            Assert.IsFalse(w.TrySpend(101));
            Assert.AreEqual(100, w.Balance, "a rejected spend must leave the balance untouched");

            Assert.IsFalse(w.TrySpend(int.MaxValue));
            Assert.AreEqual(100, w.Balance);
        }

        [Test]
        public void TrySpend_RejectsNegativeAmounts()
        {
            var w = new Wallet(100);
            Assert.IsFalse(w.TrySpend(-50));
            Assert.AreEqual(100, w.Balance, "a negative price must not top the wallet up");
        }

        [Test]
        public void CanAfford_MatchesTrySpend()
        {
            var w = new Wallet(250);
            Assert.IsTrue(w.CanAfford(0));
            Assert.IsTrue(w.CanAfford(250));
            Assert.IsFalse(w.CanAfford(251));
            Assert.IsFalse(w.CanAfford(-1));

            // Earning money never spends it.
            Assert.AreEqual(250, w.Balance);
        }

        [Test]
        public void Restore_ClampsGarbageToASaneState()
        {
            var w = new Wallet();
            w.Restore(-40, -10);
            Assert.AreEqual(0, w.Balance);
            Assert.AreEqual(0, w.LifetimeEarned);

            // Lifetime earnings can never be less than what is still in the purse.
            w.Restore(900, 100);
            Assert.AreEqual(900, w.Balance);
            Assert.AreEqual(900, w.LifetimeEarned);
        }
    }
}
