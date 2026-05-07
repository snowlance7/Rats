using Dusk;

namespace Rats
{
    public class RatsContentHandler : ContentHandler<RatsContentHandler>
    {
        public class RatAssets(DuskMod mod, string filePath) : AssetBundleLoader<RatAssets>(mod, filePath) { }
        public RatAssets? Rat;

        public class RatKingAssets(DuskMod mod, string filePath) : AssetBundleLoader<RatKingAssets>(mod, filePath) { }
        public RatKingAssets? RatKing;

        public class RatNestAssets(DuskMod mod, string filePath) : AssetBundleLoader<RatNestAssets>(mod, filePath) { }
        public RatNestAssets? RatNest;

        public class GlueBoardAssets(DuskMod mod, string filePath) : AssetBundleLoader<GlueBoardAssets>(mod, filePath) { }
        public GlueBoardAssets? GlueBoard;

        public class GlueTrapAssets(DuskMod mod, string filePath) : AssetBundleLoader<GlueTrapAssets>(mod, filePath) { }
        public GlueTrapAssets? GlueTrap;

        public class BoxOfSnapTrapsAssets(DuskMod mod, string filePath) : AssetBundleLoader<BoxOfSnapTrapsAssets>(mod, filePath) { }
        public BoxOfSnapTrapsAssets? BoxOfSnapTraps;

        public class RatCrownAssets(DuskMod mod, string filePath) : AssetBundleLoader<RatCrownAssets>(mod, filePath) { }
        public RatCrownAssets? RatCrown;

        public class RatPoisonAssets(DuskMod mod, string filePath) : AssetBundleLoader<RatPoisonAssets>(mod, filePath) { }
        public RatPoisonAssets? RatPoison;

        public RatsContentHandler(DuskMod mod) : base(mod)
        {
            RegisterContent("rat", out Rat);
            RegisterContent("rat_king", out Rat);
            RegisterContent("rat_nest", out Rat);
            RegisterContent("glue_board", out Rat);
            RegisterContent("glue_trap", out Rat);
            RegisterContent("box_of_snap_traps", out Rat);
            RegisterContent("rat_crown", out Rat);
            RegisterContent("rat_poison", out Rat);
        }
    }

}

/*public class ScrapDroneAssets(DuskMod mod, string filePath) : AssetBundleLoader<ScrapDroneAssets>(mod, filePath)
{
    [LoadFromBundle("ScrapDrone.prefab")]
    public GameObject ScrapDronePrefab { get; private set; } = null!;
}*/