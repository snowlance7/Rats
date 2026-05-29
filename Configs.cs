using Dawn.Utils;
using Dusk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rats
{
    internal static class Configs
    {
        public static int cfgMaxRats => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Maximum Rats").Value;
        public static int cfgBatchGroupCount => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Batch Group Count").Value;
        public static float cfgBatchUpdateInterval => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<float>("Batch Update Interval").Value;
        public static BoundedRange cfgRatSpawnTime => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<BoundedRange>("Rat Spawn Time").Value;
        public static int cfgFoodToSpawnRat => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Food Required to Spawn Rat").Value;
        public static int cfgEnemyFoodPerHPPoint => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Food Per HP Point").Value;
        public static bool cfgHolidayRats => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<bool>("Holiday Rats").Value;
        public static bool cfgUseJermaRats => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<bool>("Use Jerma Rats").Value;
        public static int cfgThreatToAttackPlayer => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Threat to Attack Player").Value;
        public static int cfgHighPlayerThreat => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("High Player Threat").Value;
        public static int cfgThreatToAttackEnemy => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Threat to Attack Enemy").Value;
        public static float cfgSwarmRadius => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<float>("Swarm Radius").Value;
        public static int cfgMaxDefenseRats => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Maximum Defense Rats").Value;
        public static int cfgEnemyHitsToDoDamage => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Enemy Hits to Do Damage").Value;
        public static int cfgPlayerFoodAmount => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Player Food Amount").Value;
        public static float cfgSqueakChance => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<float>("Squeak Chance").Value;
        public static bool cfgRatsTakePlayerCorpses => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<bool>("Rats Take Player Corpes").Value;
        public static bool cfgEnableInfestationSystem => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<bool>("Enable Infestation System").Value;

        // RatPoison
        public static float cfgRatPoisonMaxFluid => ContentHandler<RatsContentHandler>.Instance.RatPoison!.GetConfig<float>("Max Fluid").Value;
        public static float cfgRatPoisonPourRate => ContentHandler<RatsContentHandler>.Instance.RatPoison!.GetConfig<float>("Pour Rate").Value;
        public static float cfgPoisonToCloseNest => ContentHandler<RatsContentHandler>.Instance.RatPoison!.GetConfig<float>("Poison To Close Nest").Value;

        // GlueTrap
        public static int cfgGlueBoardAmount => ContentHandler<RatsContentHandler>.Instance.GlueTrap!.GetConfig<int>("Glue Board Amount").Value;
        public static int cfgScrapValuePerRat => ContentHandler<RatsContentHandler>.Instance.GlueTrap!.GetConfig<int>("Scrap Value Per Rat").Value;
        public static int cfgMaxRatsOnGlueTrap => ContentHandler<RatsContentHandler>.Instance.GlueTrap!.GetConfig<int>("Maximum Rats On Glue Board").Value;

        // BoxOfSnapTraps
        public static int cfgSnapTrapAmount => ContentHandler<RatsContentHandler>.Instance.BoxOfSnapTraps!.GetConfig<int>("Snap Trap Amount").Value;
        public static float cfgSnapTrapsDespawnTime => ContentHandler<RatsContentHandler>.Instance.BoxOfSnapTraps!.GetConfig<float>("Despawn Time").Value;
    }
}
/*
configMaxRats = Config.Bind("Performance", "Maximum Rats", 50, "The maximum number of rats that can be on the map. Lowering this can improve performance.");
configBatchGroupCount = Config.Bind("Performance", "Batch Group Count", 5, "The amount of groups the rats will be split into to update. (if you dont know what this means, just leave this config alone)");
configBatchUpdateInterval = Config.Bind("Performance", "Batch Update Interval", 0.2f, "The amount of time between each group update. (if you dont know what this means, just leave this config alone)");
configFoodToSpawnRat = Config.Bind("Nest", "Food Required to Spawn Rat", 5, "The amount of food needed in the nest to spawn a new rat.");
configEnemyFoodPerHPPoint = Config.Bind("Nest", "Food Per HP Point", 10, "How much food points one HP will equal for enemies. ex: if 10, thumper will give 40 food points.");
configHolidayRats = Config.Bind("General", "Holiday Rats", false, "Rats spawn with a santa hat");
configUseJermaRats = Config.Bind("Rats", "Use Jerma Rats", false, "Uses a lower quality model for the rats with no animations. Can help with performance if enabled.");
configThreatToAttackPlayer = Config.Bind("Rats", "Threat to Attack Player", 100, "The threat level at which rats begin attacking the player.");
configHighPlayerThreat = Config.Bind("Rats", "High Player Threat", 250, "The threat level at which rats will form kill squads to attack players.");
configThreatToAttackEnemy = Config.Bind("Rats", "Threat to Attack Enemy", 50, "The threat level at which rats begin attacking enemy entities.");
configSwarmRadius = Config.Bind("Rats", "Swarm Radius", 3f, "The radius in which rats swarm around their target.");
configMaxDefenseRats = Config.Bind("Rats", "Maximum Defense Rats", 10, "The maximum number of defense rats assigned to protect the nest.");
configEnemyHitsToDoDamage = Config.Bind("Rats", "Enemy Hits to Do Damage", 10, "The amount of attacks needed to do 1 shovel hit of damage to an enemy. If 10, thumper will need to be attacked 40 times by a rat.");
configPlayerFoodAmount = Config.Bind("Rats", "Player Food Amount", 30, "How much food points a player corpse gives when brought to the nest.");
configSqueakChance = Config.Bind("Rats", "Squeak Chance", 0.01f, "The chance a rat will squeak when completing a run cycle (every second)");
configRatsTakePlayerCorpses = Config.Bind("Rats", "Rats Take Player Corpes", true, "If this is true, allows rats to drag players to their nest to eat."); // TODO: Add this in

// RatPoison
configRatPoisonMaxFluid = Config.Bind("Rat Poison", "Max Fluid", 5f, "The amount of rat poison in a container of rat poison.");
configRatPoisonPourRate = Config.Bind("Rat Poison", "Pour Rate", 0.1f, "How fast the rat poison pours out of the container.");
configPoisonToCloseNest = Config.Bind("Rat Poison", "Poison To Close Nest", 1f, "The amount of poison you need to pour in a rat nest to disable it. Disabling a nest prevents rats from spawning and has a chance to spawn the rat king.");

// GlueTrap
configGlueBoardAmount = Config.Bind("Glue Trap", "Glue Board Amount", 4, "The amount of glue boards you get in the glue trap item.");
configScrapValuePerRat = Config.Bind("Glue Trap", "Scrap Value Per Rat", 2, "The scrap value added to the glue board per rat stuck.");
configMaxRatsOnGlueTrap = Config.Bind("Glue Trap", "Maximum Rats On Glue Board", 5, "The maximum number of rats that can be caught on a single glue trap before it becomes full.");

// BoxOfSnapTraps
configSnapTrapAmount = Config.Bind("Snap Traps", "Snap Trap Amount", 100, "The amount of snap traps that come with a Box Of Snap Traps.");
configSnapTrapsDespawnTime = Config.Bind("Snap Traps", "Despawn Time", 10f, "The time for snap traps to despawn after being triggered.");
*/
