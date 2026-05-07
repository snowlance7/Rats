using Dusk;
using System;
using System.Collections.Generic;
using System.Text;

namespace Rats
{
    internal static class Configs
    {
        // Performance
        public static int cfgMaxRats => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<int>("Maximum Rats").Value;
        public static float cfgAIIntervalTime => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<float>("AI Interval Time").Value;
        public static int cfgBatchGroupCount => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<int>("Batch Group Count").Value;
        public static float cfgBatchUpdateInterval => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<float>("Batch Update Interval").Value;

        // Rat King
        public static float cfgRatKingSummonChanceRatDeath => ContentHandler<RatsContentHandler>.Instance.RatKing!.GetConfig<float>("Rat Death Summon Chance").Value;
        public static float cfgRatKingSummonChancePoison => ContentHandler<RatsContentHandler>.Instance.RatKing!.GetConfig<float>("Poison Summon Chance").Value;
        public static float cfgRatKingSummonChanceNests => ContentHandler<RatsContentHandler>.Instance.RatKing!.GetConfig<float>("All Nests Disabled Summon Chance").Value;
        public static int cfgRatKingDamage => ContentHandler<RatsContentHandler>.Instance.RatKing!.GetConfig<int>("Damage").Value;
        public static float cfgRatKingRallyCooldown => ContentHandler<RatsContentHandler>.Instance.RatKing!.GetConfig<float>("Rally Cooldown").Value;
        public static float cfgRatKingLoseDistance => ContentHandler<RatsContentHandler>.Instance.RatKing!.GetConfig<float>("Distance to Lose Rat King").Value;
        public static float cfgRatKingIdleTime => ContentHandler<RatsContentHandler>.Instance.RatKing!.GetConfig<float>("Idle Time").Value;

        // Nest
        public static string cfgSewerGrateSpawnWeightCurve => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<string>("Spawn Weight Curve").Value;
        public static int cfgMinRatSpawnTime => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Minimum Rat Spawn Time").Value;
        public static int cfgMaxRatSpawnTime => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Maximum Rat Spawn Time").Value;
        public static int cfgFoodToSpawnRat => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Food Required to Spawn Rat").Value;
        public static int cfgEnemyFoodPerHPPoint => ContentHandler<RatsContentHandler>.Instance.RatNest!.GetConfig<int>("Food Per HP Point").Value;

        // Rats
        public static bool cfgHolidayRats => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<bool>("Holiday Rats").Value;
        public static bool cfgUseJermaRats => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<bool>("Use Jerma Rats").Value;
        public static float cfgDefenseRadius => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<float>("Defense Radius").Value;
        public static float cfgTimeToIncreaseThreat => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<float>("Time to Increase Threat").Value;
        public static int cfgThreatToAttackPlayer => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<int>("Threat to Attack Player").Value;
        public static int cfgHighPlayerThreat => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<int>("High Player Threat").Value;
        public static int cfgThreatToAttackEnemy => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<int>("Threat to Attack Enemy").Value;
        public static float cfgSwarmRadius => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<float>("Swarm Radius").Value;
        public static int cfgMaxDefenseRats => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<int>("Maximum Defense Rats").Value;
        public static float cfgDistanceNeededToLoseRats => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<float>("Distance Needed to Lose Rats").Value;
        public static int cfgEnemyHitsToDoDamage => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<int>("Enemy Hits to Do Damage").Value;
        public static int cfgPlayerFoodAmount => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<int>("Player Food Amount").Value;
        public static int cfgRatDamage => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<int>("Rat Damage").Value;
        public static float cfgSqueakChance => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<float>("Squeak Chance").Value;
        public static string cfgEnemyWhitelist => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<string>("Enemy Whitelist").Value;
        public static bool cfgRatsTakePlayerCorpses => ContentHandler<RatsContentHandler>.Instance.Rat!.GetConfig<bool>("Rats Take Player Corpes").Value;

        // RatPoison
        public static float cfgRatPoisonMaxFluid => ContentHandler<RatsContentHandler>.Instance.RatPoison!.GetConfig<float>("Max Fluid").Value;
        public static float cfgRatPoisonPourRate => ContentHandler<RatsContentHandler>.Instance.RatPoison!.GetConfig<float>("Pour Rate").Value;
        public static float cfgPoisonToCloseNest => ContentHandler<RatsContentHandler>.Instance.RatPoison!.GetConfig<float>("Poison To Close Nest").Value;

        // GlueTrap
        public static int cfgGlueBoardAmount => ContentHandler<RatsContentHandler>.Instance.GlueTrap!.GetConfig<int>("Glue Board Amount").Value;
        public static int cfgScrapValuePerRat => ContentHandler<RatsContentHandler>.Instance.GlueTrap!.GetConfig<int>("Scrap Value Per Rat").Value;
        public static int cfgMaxRatsOnGlueTrap => ContentHandler<RatsContentHandler>.Instance.GlueTrap!.GetConfig<int>("Maximum Rats on Glue Trap").Value;

        // BoxOfSnapTraps
        public static int cfgSnapTrapAmount => ContentHandler<RatsContentHandler>.Instance.BoxOfSnapTraps!.GetConfig<int>("Snap Trap Amount").Value;
        public static float cfgSnapTrapsDespawnTime => ContentHandler<RatsContentHandler>.Instance.BoxOfSnapTraps!.GetConfig<float>("Despawn Time").Value;
    }
}
/*
// Performance
configMaxRats = Config.Bind("Performance", "Maximum Rats", 50, "The maximum number of rats that can be on the map. Lowering this can improve performance.");
configAIIntervalTime = Config.Bind("Performance", "AI Interval Time", 0.3f, "The interval in which rats will update their AI (Changing position, doing complex calculations, etc). Setting this higher can improve performance but can also make the rats freeze in place more often while lower values makes them constantly moving but can decrease performance. Funnily enough the rats move more rat like when this is set higher.");
configBatchGroupCount = Config.Bind("Performance", "Batch Group Count", 5, "The amount of groups the rats will be split into to update. (if you dont know what this means, just leave this config alone)");
configBatchUpdateInterval = Config.Bind("Performance", "Batch Update Interval", 0.2f, "The amount of time between each group update. (if you dont know what this means, just leave this config alone)");

// RatKing
configRatKingSummonChanceRatDeath = Config.Bind("Rat King", "Rat Death Summon Chance", 0.01f, "The chance the rat king will spawn when killing a rat at high threat.");
configRatKingSummonChancePoison = Config.Bind("Rat King", "Poison Summon Chance", 0.5f, "The chance the rat king will spawn when disabling a nest with rat poison.");
configRatKingSummonChanceNests = Config.Bind("Rat King", "All Nests Disabled Summon Chance", 0.85f, "The chance the rat king will spawn when all the nests are disabled.");
configRatKingDamage = Config.Bind("Rat King", "Damage", 25, "The amount of damage the rat king does.");
configRatKingRallyCooldown = Config.Bind("Rat King", "Rally Cooldown", 30f, "The cooldown for the rat kings rally ability.");
configRatKingLoseDistance = Config.Bind("Rat King", "Distance to Lose Rat King", 20f, "The distance from the rat king you need to be to lose him. Does not apply when rampaged or hunting.");
configRatKingIdleTime = Config.Bind("Rat King", "Idle Time", 5f, "The amount of time the rat king will spend idling when reaching a destination during his roam routine.");

// Nest
configSewerGrateSpawnWeightCurve = Config.Bind("Nest", "Spawn Weight Curve", "Vanilla - 0,0 ; 1,2 | Custom - 0,0 ; 1,2", "The MoonName - CurveSpawnWeight for the SewerGrate(Rat nest).");
configMinRatSpawnTime = Config.Bind("Nest", "Minimum Rat Spawn Time", 5, "The minimum time in seconds before a rat can spawn from the nest.");
configMaxRatSpawnTime = Config.Bind("Nest", "Maximum Rat Spawn Time", 20, "The maximum time in seconds before a rat can spawn from the nest.");
configFoodToSpawnRat = Config.Bind("Nest", "Food Required to Spawn Rat", 5, "The amount of food needed in the nest to spawn a new rat.");
configEnemyFoodPerHPPoint = Config.Bind("Nest", "Food Per HP Point", 10, "How much food points one HP will equal for enemies. ex: if 10 thumper will give 40 food points.");

// Rats
configHolidayRats = Config.Bind("General", "Holiday Rats", false, "Rats spawn with a santa hat");
configUseJermaRats = Config.Bind("Rats", "Use Jerma Rats", false, "Uses a lower quality model for the rats with no animations. Can help with performance if enabled.");
configDefenseRadius = Config.Bind("Rats", "Defense Radius", 5f, "The radius in which defense rats protect the nest.");
configTimeToIncreaseThreat = Config.Bind("Rats", "Time to Increase Threat", 2.5f, "The time needed to add a threat point for a player when they are in line of sight of the rat.");
configThreatToAttackPlayer = Config.Bind("Rats", "Threat to Attack Player", 100, "The threat level at which rats begin attacking the player.");
configHighPlayerThreat = Config.Bind("Rats", "High Player Threat", 250, "The threat level at which rats will call for the rat king and the rat king will attack players.");
configThreatToAttackEnemy = Config.Bind("Rats", "Threat to Attack Enemy", 50, "The threat level at which rats begin attacking enemy entities.");
configSwarmRadius = Config.Bind("Rats", "Swarm Radius", 3f, "The radius in which rats swarm around their target.");
configMaxDefenseRats = Config.Bind("Rats", "Maximum Defense Rats", 10, "The maximum number of defense rats assigned to protect the nest.");
configDistanceNeededToLoseRats = Config.Bind("Rats", "Distance Needed to Lose Rats", 25f, "The distance the player must be from rats to lose them.");
configEnemyHitsToDoDamage = Config.Bind("Rats", "Enemy Hits to Do Damage", 10, "The amount of attacks needed to do 1 shovel hit of damage to an enemy. If 10, thumper will need to be attacked 40 times by a rat.");
configPlayerFoodAmount = Config.Bind("Rats", "Player Food Amount", 30, "How much food points a player corpse gives when brought to the nest.");
configRatDamage = Config.Bind("Rats", "Rat Damage", 2, "The damage dealt by a rat when attacking.");
configSqueakChance = Config.Bind("Rats", "Squeak Chance", 0.01f, "The chance a rat will squeak when completing a run cycle (every second)");
configEnemyWhitelist = Config.Bind("Rats", "Enemy Whitelist", "Centipede,HoarderBug,Butler,Crawler,SandSpider,RatKingEnemy", "Whitelist of enemies the rats can kill and eat. Names should be the enemyType.name value of each enemy. You can find a list of these names in the README.");
configRatsTakePlayerCorpses = Config.Bind("Rats", "Rats Take Player Corpes", true, "If this is true, allows rats to drag players to their nest to eat."); // TODO: Add this in

// RatPoison
configRatPoisonMaxFluid = Config.Bind("Rat Poison", "Max Fluid", 5f, "The amount of rat poison in a container of rat poison.");
configRatPoisonPourRate = Config.Bind("Rat Poison", "Pour Rate", 0.1f, "How fast the rat poison pours out of the container.");
configPoisonToCloseNest = Config.Bind("Rat Poison", "Poison To Close Nest", 1f, "The amount of poison you need to pour in a rat nest to disable it. Disabling a nest prevents rats from spawning and has a chance to spawn the rat king.");

// GlueTrap
configGlueBoardAmount = Config.Bind("Glue Trap", "Glue Board Amount", 4, "The amount of glue boards you get in the glue trap item.");
configScrapValuePerRat = Config.Bind("Glue Trap", "Scrap Value Per Rat", 2, "The scrap value added to the glue board per rat stuck.");
configMaxRatsOnGlueTrap = Config.Bind("Glue Trap", "Maximum Rats on Glue Trap", 5, "The maximum number of rats that can be caught on a single glue trap before it becomes full.");

// BoxOfSnapTraps
configSnapTrapAmount = Config.Bind("Snap Traps", "Snap Trap Amount", 100, "The amount of snap traps that come with a Box Of Snap Traps.");
configSnapTrapsDespawnTime = Config.Bind("Snap Traps", "Despawn Time", 10f, "The time for snap traps to despawn after being triggered.");
*/
