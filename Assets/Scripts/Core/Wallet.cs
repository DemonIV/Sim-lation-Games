using UnityEngine;

namespace Sim.Core
{
    /// <summary>
    /// The player's money purse. Deliberately tiny and total: it can never hold a negative balance,
    /// never overspend and never throw.
    ///
    /// <para>
    /// <see cref="Earn"/> ignores a non-positive amount; <see cref="TrySpend"/> returns false and
    /// leaves the balance UNTOUCHED when the amount is negative or larger than the balance, so a
    /// failed purchase can never half-apply.
    /// </para>
    ///
    /// Pure logic; no Unity serialization attributes (the Runtime save layer maps to its own DTO).
    /// </summary>
    public class Wallet
    {
        /// <summary>Current balance, never negative.</summary>
        public int Balance { get; private set; }

        /// <summary>Everything earned over the campaign's lifetime (never spent down) — a HUD stat.</summary>
        public int LifetimeEarned { get; private set; }

        public Wallet() : this(0) { }

        public Wallet(int startingBalance)
        {
            Balance = Mathf.Max(0, startingBalance);
        }

        /// <summary>Adds mission money. A zero or negative amount is ignored.</summary>
        public void Earn(int amount)
        {
            if (amount <= 0) return;
            Balance += amount;
            LifetimeEarned += amount;
        }

        /// <summary>True when the balance covers <paramref name="amount"/> (a free item always does).</summary>
        public bool CanAfford(int amount)
        {
            if (amount < 0) return false;
            return Balance >= amount;
        }

        /// <summary>
        /// Spends <paramref name="amount"/>. Returns false — WITHOUT changing the balance — for a
        /// negative amount or insufficient funds. Spending 0 succeeds and is a no-op.
        /// </summary>
        public bool TrySpend(int amount)
        {
            if (!CanAfford(amount)) return false;
            Balance -= amount;
            return true;
        }

        /// <summary>
        /// Restores a saved purse. Both values are clamped to zero, so a corrupt save can only ever
        /// produce a poor player, never a negative or nonsensical one.
        /// </summary>
        public void Restore(int balance, int lifetimeEarned)
        {
            Balance = Mathf.Max(0, balance);
            LifetimeEarned = Mathf.Max(Balance, Mathf.Max(0, lifetimeEarned));
        }
    }
}
