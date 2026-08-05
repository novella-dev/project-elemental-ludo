using System.Collections.Generic;
using ElementalLudo.Tokens;

namespace ElementalLudo.Gameplay
{
    /// <summary>
    /// The branching path of a run: stages front to back, each holding the
    /// nodes you can be standing on at that depth.
    ///
    /// Generated from a seed with System.Random rather than UnityEngine.Random,
    /// so a map is reproducible from its number alone. That makes the generator
    /// testable outside the Editor and gives A7 a run it can save as one int
    /// instead of a serialised graph.
    /// </summary>
    public sealed class LudoRunMap
    {
        /// <summary>First and last stages are always single nodes.</summary>
        public const int MinimumStages = 3;

        private readonly List<List<LudoRunNode>> stages;

        private LudoRunMap(List<List<LudoRunNode>> stages, int seed)
        {
            this.stages = stages;
            Seed = seed;
        }

        public int Seed { get; }
        public int StageCount => stages.Count;

        /// <summary>The final stage, which is always the boss alone.</summary>
        public LudoRunNode Boss => stages[stages.Count - 1][0];

        public IReadOnlyList<LudoRunNode> Stage(int index) => stages[index];

        public LudoRunNode Node(int stage, int lane) => stages[stage][lane];

        /// <summary>
        /// Builds a map.
        ///
        /// The shape is fixed at both ends: stage 0 is a lone reward, so a run
        /// opens by choosing what to carry rather than by fighting, and the
        /// last stage is the boss alone, so every path converges on it.
        /// </summary>
        public static LudoRunMap Generate(
            int stageCount,
            int seed,
            LudoElement playerElement)
        {
            int total = stageCount < MinimumStages ? MinimumStages : stageCount;
            System.Random random = new System.Random(seed);

            List<List<LudoRunNodeKind>> kinds = BuildKinds(total, random);
            List<List<List<int>>> links = BuildLinks(kinds, random);
            List<List<LudoElement>> rivals =
                BuildRivals(kinds, random, playerElement);

            List<List<LudoRunNode>> stages = new List<List<LudoRunNode>>(total);
            for (int stage = 0; stage < total; stage++)
            {
                List<LudoRunNode> row = new List<LudoRunNode>(kinds[stage].Count);
                for (int lane = 0; lane < kinds[stage].Count; lane++)
                {
                    row.Add(new LudoRunNode(
                        kinds[stage][lane],
                        stage,
                        lane,
                        links[stage][lane],
                        rivals[stage][lane]));
                }

                stages.Add(row);
            }

            return new LudoRunMap(stages, seed);
        }

        private static List<List<LudoRunNodeKind>> BuildKinds(
            int stageCount,
            System.Random random)
        {
            List<List<LudoRunNodeKind>> kinds =
                new List<List<LudoRunNodeKind>>(stageCount);

            kinds.Add(new List<LudoRunNodeKind> { LudoRunNodeKind.Reward });

            for (int stage = 1; stage < stageCount - 1; stage++)
            {
                int width = random.Next(2, 4);
                List<LudoRunNodeKind> row = new List<LudoRunNodeKind>(width);
                for (int lane = 0; lane < width; lane++)
                {
                    row.Add(PickKind(stage, random));
                }

                kinds.Add(row);
            }

            kinds.Add(new List<LudoRunNodeKind> { LudoRunNodeKind.Boss });
            PlaceHeal(kinds, random);
            return kinds;
        }

        /// <summary>
        /// Turns one node of the stage before the boss into a heal.
        ///
        /// Placed rather than rolled, so the chance to arrive at the final game
        /// patched up always exists — but only on one lane, so reaching it is a
        /// choice made several stages earlier and not a given. A stage that is
        /// only one node wide is left alone: there the heal would be
        /// compulsory, which is the same as not being a decision at all.
        /// </summary>
        private static void PlaceHeal(
            List<List<LudoRunNodeKind>> kinds,
            System.Random random)
        {
            int stage = kinds.Count - 2;
            if (stage < 1)
            {
                return;
            }

            List<LudoRunNodeKind> row = kinds[stage];
            if (row.Count < 2)
            {
                return;
            }

            row[random.Next(row.Count)] = LudoRunNodeKind.Heal;
        }

        /// <summary>
        /// What a middle stage offers.
        ///
        /// Never a match: the board game is what the run builds up to, and only
        /// the boss is one. Everything on the way is a duel, a free reward or
        /// an elite, so the run stays quick and the final game keeps its weight.
        ///
        /// Stage 1 is never an elite — the player has had exactly one reward by
        /// then and nothing to bring to a hard fight.
        /// </summary>
        private static LudoRunNodeKind PickKind(int stage, System.Random random)
        {
            int roll = random.Next(100);

            if (stage <= 1)
            {
                return roll < 70
                    ? LudoRunNodeKind.Duel
                    : LudoRunNodeKind.Reward;
            }

            if (roll < 52)
            {
                return LudoRunNodeKind.Duel;
            }

            return roll < 78
                ? LudoRunNodeKind.Elite
                : LudoRunNodeKind.Reward;
        }

        /// <summary>
        /// Picks who is waiting at each node.
        ///
        /// Drawn from the same seeded stream as everything else, so the rivals
        /// are part of the map rather than rolled when a fight starts — that is
        /// what lets the map show them, and what makes reading the elemental
        /// matchups a reason to take one fork over another.
        ///
        /// Reward nodes get an element too. It is never used, but leaving a
        /// hole in the stream would make every later draw depend on how many
        /// rewards happened to come up.
        ///
        /// The player's own element is excluded. A seat only exists once, so a
        /// node claiming to hold the player's element had to be quietly swapped
        /// for a different one when the fight actually started — and the map
        /// had already promised the wrong opponent.
        /// </summary>
        private static List<List<LudoElement>> BuildRivals(
            List<List<LudoRunNodeKind>> kinds,
            System.Random random,
            LudoElement playerElement)
        {
            List<LudoElement> pool = new List<LudoElement>(4);
            foreach (LudoElement candidate in new[]
                     {
                         LudoElement.Fire,
                         LudoElement.Water,
                         LudoElement.Lightning,
                         LudoElement.Plant
                     })
            {
                if (candidate != playerElement)
                {
                    pool.Add(candidate);
                }
            }

            LudoElement[] elements = pool.ToArray();

            List<List<LudoElement>> rivals =
                new List<List<LudoElement>>(kinds.Count);
            foreach (List<LudoRunNodeKind> row in kinds)
            {
                List<LudoElement> line = new List<LudoElement>(row.Count);
                for (int lane = 0; lane < row.Count; lane++)
                {
                    line.Add(elements[random.Next(elements.Length)]);
                }

                rivals.Add(line);
            }

            return rivals;
        }

        /// <summary>
        /// Wires each stage to the next.
        ///
        /// Two passes, because one is not enough. Linking every node forward to
        /// its nearest neighbour leaves no dead ends but can orphan a node in
        /// the next stage that nobody points at; the second pass gives any
        /// orphan an incoming link. Together they guarantee every node is both
        /// reachable from the start and able to reach the boss, which is the
        /// property a map has to have and the one easiest to lose.
        /// </summary>
        private static List<List<List<int>>> BuildLinks(
            List<List<LudoRunNodeKind>> kinds,
            System.Random random)
        {
            List<List<List<int>>> links = new List<List<List<int>>>(kinds.Count);
            for (int stage = 0; stage < kinds.Count; stage++)
            {
                List<List<int>> row = new List<List<int>>(kinds[stage].Count);
                for (int lane = 0; lane < kinds[stage].Count; lane++)
                {
                    row.Add(new List<int>(2));
                }

                links.Add(row);
            }

            for (int stage = 0; stage < kinds.Count - 1; stage++)
            {
                int width = kinds[stage].Count;
                int nextWidth = kinds[stage + 1].Count;

                for (int lane = 0; lane < width; lane++)
                {
                    int nearest = NearestLane(lane, width, nextWidth);
                    links[stage][lane].Add(nearest);

                    // A second way forward, often but not always, so the map
                    // reads as a fork rather than a corridor.
                    if (nextWidth > 1 && random.Next(100) < 45)
                    {
                        int other = nearest + (random.Next(2) == 0 ? -1 : 1);
                        other = Clamp(other, 0, nextWidth - 1);
                        if (other != nearest)
                        {
                            links[stage][lane].Add(other);
                        }
                    }
                }

                for (int nextLane = 0; nextLane < nextWidth; nextLane++)
                {
                    if (HasIncoming(links[stage], nextLane))
                    {
                        continue;
                    }

                    int from = NearestLane(nextLane, nextWidth, width);
                    links[stage][from].Add(nextLane);
                }
            }

            return links;
        }

        /// <summary>
        /// The lane in a row of <paramref name="toWidth"/> sitting closest to
        /// where <paramref name="lane"/> sits in a row of
        /// <paramref name="fromWidth"/>, comparing their positions across the
        /// row rather than their raw indices — rows differ in width, so index 2
        /// of three is the far edge but the middle of five.
        /// </summary>
        private static int NearestLane(int lane, int fromWidth, int toWidth)
        {
            if (toWidth <= 1)
            {
                return 0;
            }

            float position = fromWidth <= 1
                ? 0.5f
                : (float)lane / (fromWidth - 1);

            // Half-up on purpose, rather than Math.Round: that rounds halves to
            // the nearest even number, so a lone node feeding a stage of two
            // would silently always pick lane 0. Exact halves land here often —
            // a single node above a pair produces one every time — so the tie
            // rule is worth stating instead of inheriting.
            int nearest = (int)(position * (toWidth - 1) + 0.5f);
            return Clamp(nearest, 0, toWidth - 1);
        }

        private static bool HasIncoming(List<List<int>> stageLinks, int lane)
        {
            foreach (List<int> laneLinks in stageLinks)
            {
                if (laneLinks.Contains(lane))
                {
                    return true;
                }
            }

            return false;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }
}
