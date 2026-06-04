using Dawn.Utils;
using Dusk;

namespace Rats
{
    internal static class Configs
    {
        public static int cfgMaxRats { get; private set; }
        public static int cfgBatchGroupCount { get; private set; }
        public static float cfgBatchUpdateInterval { get; private set; }
        public static BoundedRange cfgRatSpawnTime { get; private set; } = null!;
        public static int cfgFoodToSpawnRat { get; private set; }
        public static int cfgEnemyFoodPerHPPoint { get; private set; }
        public static bool cfgHolidayRats { get; private set; }
        public static bool cfgUseJermaRats { get; private set; }
        public static int cfgThreatToAttackPlayer { get; private set; }
        public static int cfgThreatToAttackEnemy { get; private set; }
        public static float cfgSwarmRadius { get; private set; }
        public static int cfgMaxDefenseRats { get; private set; }
        public static int cfgEnemyHitsToDoDamage { get; private set; }
        public static int cfgPlayerFoodAmount { get; private set; }
        public static float cfgSqueakChance { get; private set; }
        public static bool cfgRatsTakePlayerCorpses { get; private set; }
        public static bool cfgEnableInfestationSystem { get; private set; }

        // RatPoison
        public static float cfgMaxFluid { get; private set; }
        public static float cfgPourRate { get; private set; }
        public static float cfgPoisonToCloseNest { get; private set; }

        // GlueTrap
        public static int cfgGlueBoardAmount { get; private set; }
        public static int cfgScrapValuePerRat { get; private set; }
        public static int cfgMaxRatsOnGlueBoard { get; private set; }

        // BoxOfSnapTraps
        public static int cfgSnapTrapAmount { get; private set; }
        public static float cfgDespawnTime { get; private set; }

        public static void Init()
        {
            var nest = ContentHandler<RatsContentHandler>.Instance.RatNest!;
            var poison = ContentHandler<RatsContentHandler>.Instance.RatPoison!;
            var glue = ContentHandler<RatsContentHandler>.Instance.GlueTrap!;
            var box = ContentHandler<RatsContentHandler>.Instance.BoxOfSnapTraps!;

            cfgMaxRats = nest.GetConfig<int>("Max Rats").Value;
            cfgBatchGroupCount = nest.GetConfig<int>("Batch Group Count").Value;
            cfgBatchUpdateInterval = nest.GetConfig<float>("Batch Update Interval").Value;
            cfgRatSpawnTime = nest.GetConfig<BoundedRange>("Rat Spawn Time").Value;
            cfgFoodToSpawnRat = nest.GetConfig<int>("Food To Spawn Rat").Value;
            cfgEnemyFoodPerHPPoint = nest.GetConfig<int>("Enemy Food Per HP Point").Value;
            cfgHolidayRats = nest.GetConfig<bool>("Holiday Rats").Value;
            cfgUseJermaRats = nest.GetConfig<bool>("Use Jerma Rats").Value;
            cfgThreatToAttackPlayer = nest.GetConfig<int>("Threat To Attack Player").Value;
            cfgThreatToAttackEnemy = nest.GetConfig<int>("Threat To Attack Enemy").Value;
            cfgSwarmRadius = nest.GetConfig<float>("Swarm Radius").Value;
            cfgMaxDefenseRats = nest.GetConfig<int>("Max Defense Rats").Value;
            cfgEnemyHitsToDoDamage = nest.GetConfig<int>("Enemy Hits To Do Damage").Value;
            cfgPlayerFoodAmount = nest.GetConfig<int>("Player Food Amount").Value;
            cfgSqueakChance = nest.GetConfig<float>("Squeak Chance").Value;
            cfgRatsTakePlayerCorpses = nest.GetConfig<bool>("Rats Take Player Corpses").Value;
            cfgEnableInfestationSystem = nest.GetConfig<bool>("Enable Infestation System").Value;

            // RatPoison
            cfgMaxFluid = poison.GetConfig<float>("Max Fluid").Value;
            cfgPourRate = poison.GetConfig<float>("Pour Rate").Value;
            cfgPoisonToCloseNest = poison.GetConfig<float>("Poison To Close Nest").Value;

            // GlueTrap
            cfgGlueBoardAmount = glue.GetConfig<int>("Glue Board Amount").Value;
            cfgScrapValuePerRat = glue.GetConfig<int>("Scrap Value Per Rat").Value;
            cfgMaxRatsOnGlueBoard = glue.GetConfig<int>("Max Rats On Glue Board").Value;

            // BoxOfSnapTraps
            cfgSnapTrapAmount = box.GetConfig<int>("Snap Trap Amount").Value;
            cfgDespawnTime = box.GetConfig<float>("Despawn Time").Value;
        }
    }
}
