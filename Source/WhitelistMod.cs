using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ArchogenInjectorWhitelist
{
    public class WhitelistSettings : ModSettings
    {
        // Only the player's departures from the default lists are stored, so a later change to a default list
        // still reaches them. defNames, so an entry survives its mod being temporarily unloaded.
        public HashSet<string> forcedOn = new HashSet<string>();
        public HashSet<string> forcedOff = new HashSet<string>();

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref forcedOn, "forcedOn", LookMode.Value);
            Scribe_Collections.Look(ref forcedOff, "forcedOff", LookMode.Value);
            if (forcedOn == null)
            {
                forcedOn = new HashSet<string>();
            }
            if (forcedOff == null)
            {
                forcedOff = new HashSet<string>();
            }
        }

        public bool Enabled(string gene, bool inDefault)
        {
            return forcedOn.Contains(gene) || (inDefault && !forcedOff.Contains(gene));
        }

        public void Set(string gene, bool inDefault, bool enabled)
        {
            forcedOn.Remove(gene);
            forcedOff.Remove(gene);
            if (enabled && !inDefault)
            {
                forcedOn.Add(gene);
            }
            else if (!enabled && inDefault)
            {
                forcedOff.Add(gene);
            }
        }
    }

    public class WhitelistMod : Mod
    {
        public static WhitelistSettings settings;

        private const float RowHeight = 30f;

        private class Row
        {
            public GeneDef gene;
            public bool archite;
            public string label;
            public string info;
            public string searchText;
            public string tip;
        }

        private enum Show
        {
            All,
            Allowed,
            Blocked
        }

        private enum Kind
        {
            All,
            Archite,
            SideEffects
        }

        private List<Row> rows;
        private readonly List<Row> filtered = new List<Row>();
        private string search = "";
        private string appliedSearch;
        private Show show;
        private Show appliedShow;
        private Kind kind;
        private Kind appliedKind;
        private Vector2 scroll;
        // Recounted only when a tick box changes.
        private bool countsDirty = true;
        private int architeAllowed;
        private int architeTotal;
        private int sideAllowed;
        private int sideTotal;

        public WhitelistMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<WhitelistSettings>();
            new Harmony("DanZinagri.VQEAncients.ArchogenInjectorWhitelist").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory()
        {
            return "VQE Ancients - Archogen Injector Whitelist";
        }

        private static bool IsEnabled(GeneDef gene)
        {
            return settings.Enabled(gene.defName, InjectionFilter.InDefault(gene));
        }

        // Takes effect the next time the injector or an experiment pawn rolls a gene.
        private void SetEnabled(GeneDef gene, bool enabled)
        {
            settings.Set(gene.defName, InjectionFilter.InDefault(gene), enabled);
            InjectionFilter.dirty = true;
            countsDirty = true;
        }

        // Every gene Ancients could roll without this mod: archite genes as the reward, and genes with a metabolism
        // value of zero or more as side effects, as long as Ancients' own rules accept them. Listed genes those rules
        // reject are shown too, since a list entry overrides them.
        private void BuildRows()
        {
            if (InjectionFilter.dirty)
            {
                InjectionFilter.Rebuild();
            }
            rows = new List<Row>();
            foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                bool inDefault = InjectionFilter.InDefault(gene);
                bool archite = gene.biostatArc > 0;
                bool rollable = archite || gene.biostatMet >= 0;
                bool accepted = InjectionFilter.AncientsAccepts(gene);
                if (!inDefault && !(rollable && accepted))
                {
                    continue;
                }
                string label = gene.LabelCap;
                string modName = gene.modContentPack?.Name ?? "?";
                string info = (archite ? "archite reward" : "side effect, metabolism " + gene.biostatMet.ToStringWithSign()) + "  -  " + modName;
                if (!accepted)
                {
                    info = "outside Ancients' own rules  -  " + info;
                }
                else if (!rollable)
                {
                    info = "never rolled (negative metabolism)  -  " + info;
                }
                if (inDefault)
                {
                    info = "default  -  " + info;
                }
                rows.Add(new Row
                {
                    gene = gene,
                    archite = archite,
                    label = label,
                    info = info,
                    searchText = (label + " " + gene.defName + " " + info).ToLowerInvariant(),
                    tip = label + " (" + gene.defName + ")\n\n" + gene.description
                });
            }
            rows.Sort((a, b) => string.CompareOrdinal(a.searchText, b.searchText));
        }

        // The list is rebuilt when the search text or a filter button changes, not on every tick box,
        // so a row does not vanish from under the cursor the moment it is toggled.
        private void ApplyFilter()
        {
            appliedSearch = search;
            appliedShow = show;
            appliedKind = kind;
            filtered.Clear();
            string needle = search.ToLowerInvariant();
            foreach (Row row in rows)
            {
                if (needle.Length != 0 && !row.searchText.Contains(needle))
                {
                    continue;
                }
                if (show != Show.All && IsEnabled(row.gene) != (show == Show.Allowed))
                {
                    continue;
                }
                if (kind != Kind.All && row.archite != (kind == Kind.Archite))
                {
                    continue;
                }
                filtered.Add(row);
            }
        }

        private void Recount()
        {
            countsDirty = false;
            architeAllowed = architeTotal = sideAllowed = sideTotal = 0;
            foreach (Row row in rows)
            {
                bool enabled = IsEnabled(row.gene);
                if (row.archite)
                {
                    architeTotal++;
                    if (enabled)
                    {
                        architeAllowed++;
                    }
                }
                else
                {
                    sideTotal++;
                    if (enabled)
                    {
                        sideAllowed++;
                    }
                }
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (rows == null)
            {
                BuildRows();
            }
            if (appliedSearch != search || appliedShow != show || appliedKind != kind)
            {
                ApplyFilter();
            }
            if (countsDirty)
            {
                Recount();
            }

            Text.Font = GameFont.Small;
            Rect header = new Rect(inRect.x, inRect.y, inRect.width, 66f);
            Widgets.Label(new Rect(header.x, header.y, header.width, 22f), "Only ticked genes can be given by the archogen injector or rolled on Ancients' experiment pawns. Changes apply straight away.");
            Widgets.Label(new Rect(header.x, header.y + 22f, header.width, 22f), "Archite rewards allowed: " + architeAllowed + " of " + architeTotal + ".   Side effects allowed: " + sideAllowed + " of " + sideTotal + ".");
            if (architeAllowed == 0 || sideAllowed == 0)
            {
                GUI.color = Color.yellow;
                Widgets.Label(new Rect(header.x, header.y + 44f, header.width, 22f), architeAllowed == 0 ? "No archite gene is allowed: the injector will give no reward." : "No side-effect gene is allowed: injections will have no drawback.");
                GUI.color = Color.white;
            }

            Rect bar = new Rect(inRect.x, header.yMax + 4f, inRect.width, 28f);
            search = Widgets.TextField(new Rect(bar.x, bar.y, 180f, bar.height), search);
            if (Widgets.ButtonText(new Rect(bar.x + 186f, bar.y, 120f, bar.height), "Show: " + show.ToString().ToLowerInvariant()))
            {
                show = (Show)(((int)show + 1) % 3);
            }
            if (Widgets.ButtonText(new Rect(bar.x + 312f, bar.y, 150f, bar.height), "Kind: " + (kind == Kind.All ? "all" : kind == Kind.Archite ? "archite" : "side effects")))
            {
                kind = (Kind)(((int)kind + 1) % 3);
            }
            if (Widgets.ButtonText(new Rect(bar.xMax - 370f, bar.y, 110f, bar.height), "Allow shown"))
            {
                foreach (Row row in filtered)
                {
                    SetEnabled(row.gene, true);
                }
            }
            if (Widgets.ButtonText(new Rect(bar.xMax - 256f, bar.y, 110f, bar.height), "Block shown"))
            {
                foreach (Row row in filtered)
                {
                    SetEnabled(row.gene, false);
                }
            }
            if (Widgets.ButtonText(new Rect(bar.xMax - 142f, bar.y, 142f, bar.height), "Reset to defaults"))
            {
                settings.forcedOn.Clear();
                settings.forcedOff.Clear();
                InjectionFilter.dirty = true;
                countsDirty = true;
                appliedSearch = null;
            }

            Rect outRect = new Rect(inRect.x, bar.yMax + 6f, inRect.width, inRect.yMax - bar.yMax - 6f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, filtered.Count * RowHeight);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            // Only the rows on screen are drawn.
            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
            int last = Mathf.Min(filtered.Count - 1, Mathf.CeilToInt((scroll.y + outRect.height) / RowHeight));
            for (int i = first; i <= last; i++)
            {
                Row row = filtered[i];
                Rect rect = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
                if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(rect);
                }
                Widgets.DrawHighlightIfMouseover(rect);

                bool enabled = IsEnabled(row.gene);
                Widgets.CheckboxDraw(rect.x + 4f, rect.y + 3f, enabled, false);
                Widgets.DefIcon(new Rect(rect.x + 34f, rect.y + 1f, RowHeight - 2f, RowHeight - 2f), row.gene);
                GUI.color = Color.white;

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rect.x + 70f, rect.y, 250f, RowHeight), row.label);
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x + 326f, rect.y, rect.width - 330f, RowHeight), row.info);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (Mouse.IsOver(rect))
                {
                    TooltipHandler.TipRegion(rect, row.tip);
                }
                // The whole row is the tick box.
                if (Widgets.ButtonInvisible(rect))
                {
                    SetEnabled(row.gene, !enabled);
                }
            }
            Widgets.EndScrollView();
        }
    }
}
