using Dawn.Utils;
using Dusk;

namespace Rats
{
    internal static class Configs
    {
        // Performance
        public static int cfgMaxRats { get; private set; }
        public static int cfgBatchGroupCount { get; private set; }
        public static float cfgBatchUpdateInterval { get; private set; }

        // Other
        public static bool cfgHolidayRats { get; private set; }
        public static bool cfgUseJermaRats { get; private set; }
        public static bool cfgEnableInfestationSystem { get; private set; }

        // Rats
        public static int cfgThreatToAttackPlayer { get; private set; }
        public static int cfgThreatToAttackEnemy { get; private set; }
        public static float cfgSwarmRadius { get; private set; }
        public static int cfgEnemyHitsToDoDamage { get; private set; }
        public static float cfgSqueakChance { get; private set; }
        public static string[] cfgFoodItemNames { get; private set; } = null!;
        public static string[] cfgFoodItemTags { get; private set; } = null!;

        // RatNest
        public static BoundedRange cfgRatSpawnTime { get; private set; } = null!;
        public static int cfgFoodToSpawnRat { get; private set; }
        public static int cfgEnemyFoodPerHPPoint { get; private set; }
        public static int cfgMaxDefenseRats { get; private set; }
        public static int cfgPlayerFoodAmount { get; private set; }
        public static bool cfgRatsTakePlayerCorpses { get; private set; }

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
            // Performance
            cfgMaxRats = Plugin.Instance.Config.Bind("Performance", "Max Rats", 100, "The maximum number of rats that can be on the map. Lowering this can improve performance.").Value;
            cfgBatchGroupCount = Plugin.Instance.Config.Bind("Performance", "Batch Group Count", 5, "The amount of groups the rats will be split into to update. (if you dont know what this means, just leave this config alone)").Value;
            cfgBatchUpdateInterval = Plugin.Instance.Config.Bind("Performance", "Batch Update Interval", 0.2f, "The amount of time between each group update. (if you dont know what this means, just leave this config alone)").Value;

            // Other
            cfgHolidayRats = Plugin.Instance.Config.Bind("Other", "Holiday Rats", false, "Rats spawn with a santa hat").Value;
            cfgUseJermaRats = Plugin.Instance.Config.Bind("Other", "Use Jerma Rats", false, "Uses a lower quality model for the rats with no animations. Can help with performance if enabled.").Value;
            cfgEnableInfestationSystem = Plugin.Instance.Config.Bind("Other", "Enable Infestation System", true, "Enables the infestation system").Value;

            // Rats
            cfgThreatToAttackPlayer = Plugin.Instance.Config.Bind("Rats", "Threat To Attack Player", 75, "The threat level at which rats begin attacking the player").Value;
            cfgThreatToAttackEnemy = Plugin.Instance.Config.Bind("Rats", "Threat To Attack Enemy", 50, "The threat level at which rats begin attacking other enemies").Value;
            cfgSwarmRadius = Plugin.Instance.Config.Bind("Rats", "Swarm Radius", 2f, "The radius in which rats swarm around their target").Value;
            cfgEnemyHitsToDoDamage = Plugin.Instance.Config.Bind("Rats", "Enemy Hits To Do Damage", 10, "The amount of attacks needed to do 1 shovel hit of damage to an enemy. If 10, thumper will need to be attacked 40 times by a rat.").Value;
            cfgSqueakChance = Plugin.Instance.Config.Bind("Rats", "Squeak Chance", 0.01f, "The chance a rat will squeak when completing a run cycle (every second)").Value;
            cfgFoodItemNames = Plugin.Instance.Config.Bind("Rats", "Food Item Names", "melaniemeliciouscooked:meatzero,melaniemeliciouscooked:carrotzero,melaniemeliciouscooked:soupveggiezero,melaniemeliciouscooked:soupmeatzero,melaniemeliciouscooked:loafmeatzero,melaniemeliciouscooked:potatozero,melaniemeliciouscooked:wheatzero,melaniemeliciouscooked:flourzero,melaniemeliciouscooked:loafbreadzero,melaniemeliciouscooked:piezero,melaniemeliciouscooked:sliderzero,melaniemeliciouscooked:burgerzero,melaniemeliciouscooked:meatone,melaniemeliciouscooked:sandwichcheesezero,melaniemeliciouscooked:grapezero,melaniemeliciouscooked:juicezero,melaniemeliciouscooked:grapeone,melaniemeliciouscooked:juiceone,melaniemeliciouscooked:alcoholzero,melaniemeliciouscooked:alcoholone,melaniemeliciouscooked:alcoholtwo,melaniemeliciouscooked:alcoholthree,melaniemeliciouscooked:alcoholfour,melaniemeliciouscooked:juicetwo,melaniemeliciouscooked:juicethree,melaniemeliciouscooked:burstberryzero,melaniemeliciouscooked:burstberryone,melaniemeliciouscooked:pieone,melaniemeliciouscooked:honeyzero,melaniemeliciouscooked:hivezero,melaniemeliciouscooked:alcoholfive,melaniemeliciouscooked:hiveone,melaniemeliciouscooked:fishzero,melaniemeliciouscooked:fishone,melaniemeliciouscooked:baitzero,melaniemeliciouscooked:baitone,melaniemeliciouscooked:tomatozero,melaniemeliciouscooked:tomatoone,melaniemeliciouscooked:seedtomato,melaniemeliciouscooked:barrelone,melaniemeliciouscooked:pietwo,melaniemeliciouscooked:cookiezero,melaniemeliciouscooked:bruschettazero,melaniemeliciouscooked:kabobzero,melaniemeliciouscooked:popsiclezero,melaniemeliciouscooked:popsicleone,melaniemeliciouscooked:popsicletwo,melaniemeliciouscooked:fishtwo,melaniemeliciouscooked:fishthree,melaniemeliciouscooked:fishstickszero,lethal_company:candy,lethal_company:jar_of_pickles,lethal_company:pill_bottle,lethal_company:egg,lethal_company:hand,lethal_company:bone,lethal_company:ribcage,lethal_company:ear,lethal_company:foot,lethal_company:knee,lethal_company:heart,lethal_company:tongue", "Dawnlib NamespaceKeys for items rats should steal").Value.Replace(" ", "").ToLower().Split(",");
            cfgFoodItemTags = Plugin.Instance.Config.Bind("Rats", "Food Item Tags", "food,edible,organic,consumable,meat,produce,ingredient,snack,perishable,corpse,garbage,ration,snack,candy,waste,carcass,rat_food", "Dawnlib tags for items rats should steal").Value.Replace(" ", "").ToLower().Split(",");

            // RatNest
            cfgRatSpawnTime = Plugin.Instance.Config.Bind("RatNest", "Rat Spawn Time", new BoundedRange(10, 30), "The min/max time in seconds before a rat can spawn from a nest after the last rat was spawned").Value;
            cfgFoodToSpawnRat = Plugin.Instance.Config.Bind("RatNest", "Food To Spawn Rat", 5, "The amount of food needed in the nest to spawn a new rat").Value;
            cfgEnemyFoodPerHPPoint = Plugin.Instance.Config.Bind("RatNest", "Enemy Food Per HP Point", 10, "How much food points one HP will equal for enemies. ex: if 10, thumper will give 40 food points").Value;
            cfgMaxDefenseRats = Plugin.Instance.Config.Bind("RatNest", "Max Defense Rats", 10, "The maximum number of defense rats assigned to protect the nest").Value;
            cfgPlayerFoodAmount = Plugin.Instance.Config.Bind("RatNest", "Player Food Amount", 30, "How much food points a player corpse gives when brought to the nest").Value;
            cfgRatsTakePlayerCorpses = Plugin.Instance.Config.Bind("RatNest", "Rats Take Player Corpses", true, "If this is true, allows rats to drag players to their nest to eat").Value;

            // RatPoison
            cfgMaxFluid = Plugin.Instance.Config.Bind("RatPoison", "Max Fluid", 5f, "The amount of rat poison in a jug of rat poison").Value;
            cfgPourRate = Plugin.Instance.Config.Bind("RatPoison", "Pour Rate", 0.1f, "How fast the rat poison pours out of the container").Value;
            cfgPoisonToCloseNest = Plugin.Instance.Config.Bind("RatPoison", "Poison To Close Nest", 1f, "The amount of poison you need to pour in a rat nest to disable it").Value;

            // GlueTrap
            cfgGlueBoardAmount = Plugin.Instance.Config.Bind("GlueTrap", "Glue Board Amount", 4, "The amount of glue boards you can place down from a glue board trap item").Value;
            cfgScrapValuePerRat = Plugin.Instance.Config.Bind("GlueTrap", "Scrap Value Per Rat", 2, "The scrap value added to the glue board per rat stuck in trap").Value;
            cfgMaxRatsOnGlueBoard = Plugin.Instance.Config.Bind("GlueTrap", "Max Rats On Glue Board", 5, "The maximum number of rats that can be caught on a single glue board").Value;

            // BoxOfSnapTraps
            cfgSnapTrapAmount = Plugin.Instance.Config.Bind("BoxOfSnapTraps", "Snap Trap Amount", 100, "The amount of snap traps that come with a Box Of Snap Traps").Value;
            cfgDespawnTime = Plugin.Instance.Config.Bind("BoxOfSnapTraps", "Despawn Time", 65, "The time in seconds for snap traps to despawn after being triggered").Value;
        }
    }
}
