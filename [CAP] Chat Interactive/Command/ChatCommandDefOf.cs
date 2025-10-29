using RimWorld;

namespace CAP_ChatInteractive
{
    [DefOf]
    public static class ChatCommandDefOf
    {
        //public static ChatCommandDef Help;
        //public static ChatCommandDef Points;
        //public static ChatCommandDef Buy;
        //public static ChatCommandDef Use;
        //public static ChatCommandDef Equip;
        //public static ChatCommandDef Wear;
        //public static ChatCommandDef Backpack;
        //public static ChatCommandDef Event;
        //public static ChatCommandDef Balance;

        static ChatCommandDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ChatCommandDefOf));
        }
    }
}