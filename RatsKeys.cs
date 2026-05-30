using Dawn;

namespace Rats
{
    public static class RatsKeys
    {
        public static readonly NamespacedKey<DawnItemInfo> GlueBoard = NamespacedKey<DawnItemInfo>.From("rats", "glue_board");
        public static readonly NamespacedKey<DawnItemInfo> GlueTrap = NamespacedKey<DawnItemInfo>.From("rats", "glue_trap");
        public static readonly NamespacedKey<DawnItemInfo> RatPoison = NamespacedKey<DawnItemInfo>.From("rats", "rat_poison");
        public static readonly NamespacedKey<DawnItemInfo> BoxOfSnapTraps = NamespacedKey<DawnItemInfo>.From("rats", "box_of_snap_traps");
        public static readonly NamespacedKey<DawnMapObjectInfo> RatNest = NamespacedKey<DawnMapObjectInfo>.From("rats", "rat_nest");
    }
}