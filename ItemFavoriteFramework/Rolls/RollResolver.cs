using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    internal sealed class RollTally {
        internal long Pid;
        internal RollChoice Choice;
        // 1-100, the player's last roll; 0 for a pass.
        internal int Roll;
        internal int Rerolls;
    }

    // Who wins, from the votes alone. Need beats Greed, Pass never wins. Everyone who chose Need or Greed rolls
    // 1-100 (so the numbers can be shown), only the best choice's rollers contend, and a tie at the top is
    // rolled again among the tied players only -- after ten tied rounds one of them is picked at random.
    internal static class RollResolver {
        private const int MaxRounds = 10;

        // Test hook (iff-roll-debug forcetie): the next resolve's first round rolls 50 for everyone.
        internal static bool ForceTieOnce;

        // random(minInclusive, maxExclusive).
        internal static RollOutcome Resolve(IList<long> members, IDictionary<long, RollChoice> votes, Func<int, int, int> random,
            out long winner, out List<RollTally> tallies) {
            winner = 0;
            tallies = new List<RollTally>();
            foreach (long pid in members) {
                RollChoice choice = votes.TryGetValue(pid, out RollChoice voted) && voted != RollChoice.None ? voted : RollChoice.Pass;
                tallies.Add(new RollTally { Pid = pid, Choice = choice });
            }

            bool forceTie = ForceTieOnce;
            ForceTieOnce = false;
            foreach (RollTally tally in tallies) {
                if (tally.Choice == RollChoice.Need || tally.Choice == RollChoice.Greed) { tally.Roll = forceTie ? 50 : random(1, 101); }
            }

            RollChoice best = tallies.Count == 0 ? RollChoice.Pass : tallies.Max(t => t.Choice);
            if (best <= RollChoice.Pass) { return RollOutcome.AllPassed; }

            List<RollTally> contenders = tallies.Where(t => t.Choice == best).ToList();
            for (int round = 0; ; round++) {
                int top = contenders.Max(t => t.Roll);
                List<RollTally> tied = contenders.Where(t => t.Roll == top).ToList();
                if (tied.Count == 1) {
                    winner = tied[0].Pid;
                    return RollOutcome.Won;
                }
                if (round >= MaxRounds) {
                    winner = tied[random(0, tied.Count)].Pid;
                    return RollOutcome.Won;
                }
                foreach (RollTally tally in tied) {
                    tally.Roll = random(1, 101);
                    tally.Rerolls++;
                }
                contenders = tied;
            }
        }
    }
}
