using Dusk;

namespace Rats
{
    public class RatsContentHandler : ContentHandler<RatsContentHandler>
    {
        public class RatNestAssets(DuskMod mod, string filePath) : AssetBundleLoader<RatNestAssets>(mod, filePath) { }
        public RatNestAssets? RatNest;

        public class GlueTrapAssets(DuskMod mod, string filePath) : AssetBundleLoader<GlueTrapAssets>(mod, filePath) { }
        public GlueTrapAssets? GlueTrap;

        public class BoxOfSnapTrapsAssets(DuskMod mod, string filePath) : AssetBundleLoader<BoxOfSnapTrapsAssets>(mod, filePath) { }
        public BoxOfSnapTrapsAssets? BoxOfSnapTraps;

        public class RatPoisonAssets(DuskMod mod, string filePath) : AssetBundleLoader<RatPoisonAssets>(mod, filePath) { }
        public RatPoisonAssets? RatPoison;

        public RatsContentHandler(DuskMod mod) : base(mod)
        {
            RegisterContent("rat_nest", out RatNest);
            RegisterContent("glue_trap", out GlueTrap);
            RegisterContent("box_of_snap_traps", out BoxOfSnapTraps);
            RegisterContent("rat_poison", out RatPoison);
        }
    }

}

/*public class ScrapDroneAssets(DuskMod mod, string filePath) : AssetBundleLoader<ScrapDroneAssets>(mod, filePath)
{
    [LoadFromBundle("ScrapDrone.prefab")]
    public GameObject ScrapDronePrefab { get; private set; } = null!;
}*/