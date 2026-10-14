using System.Collections.Generic;
using HarmonyLib;
using VanillaQuestsExpandedAncients;
using Verse;

namespace ArchogenInjectorWhitelist
{
    // The default selection: genes the archogen injector may roll unless the player turns them off.
    // Any number of these defs may be loaded (another mod can add its own, or patch this mod's); their lists are merged.
    public class InjectionWhitelistDef : Def
    {
        // defNames, not def references: an entry whose gene is not loaded is skipped without an error,
        // and MayRequire works on each entry.
        public List<string> genes = new List<string>();
    }

    // Ancients asks Utils.IsValidGeneForInjection about every gene before the archogen injector (reward and side effect)
    // or an experiment pawn's generation may roll it. This postfixes that one check and answers from the whitelist
    // instead, so how genes are picked, offered and applied stays Ancients' code. It only runs when a pawn is injected or
    // an experiment pawn is generated.
    [HarmonyPatch(typeof(Utils), nameof(Utils.IsValidGeneForInjection))]
    public static class InjectionFilter
    {
        // Set while the settings menu asks Ancients what its own rules say.
        private static bool bypass;

        // Rebuilt after any settings change, the next time it is needed.
        public static bool dirty = true;
        private static readonly HashSet<GeneDef> allowed = new HashSet<GeneDef>();
        private static readonly HashSet<string> defaults = new HashSet<string>();

        [HarmonyPostfix]
        public static void AnswerFromWhitelist(GeneDef geneDef, ref bool __result)
        {
            if (!bypass && geneDef != null)
            {
                __result = IsAllowed(geneDef);
            }
        }

        public static bool IsAllowed(GeneDef gene)
        {
            if (dirty)
            {
                Rebuild();
            }
            return allowed.Contains(gene);
        }

        // Ancients' own verdict (its blacklist def, android and morph genes, extraction-disabled genes), without this mod.
        public static bool AncientsAccepts(GeneDef gene)
        {
            bypass = true;
            try
            {
                return Utils.IsValidGeneForInjection(gene);
            }
            finally
            {
                bypass = false;
            }
        }

        public static bool InDefault(GeneDef gene)
        {
            return defaults.Contains(gene.defName);
        }

        public static void Rebuild()
        {
            dirty = false;
            defaults.Clear();
            foreach (InjectionWhitelistDef def in DefDatabase<InjectionWhitelistDef>.AllDefsListForReading)
            {
                if (def.genes != null)
                {
                    defaults.UnionWith(def.genes);
                }
            }
            allowed.Clear();
            WhitelistSettings settings = WhitelistMod.settings;
            foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                if (settings.Enabled(gene.defName, defaults.Contains(gene.defName)))
                {
                    allowed.Add(gene);
                }
            }
        }
    }
}
