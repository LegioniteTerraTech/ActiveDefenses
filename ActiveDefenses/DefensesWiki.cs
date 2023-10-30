using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerraTechETCUtil;

namespace ActiveDefenses
{
    public class DefensesWiki
    {

        internal static LoadingHintsExt.LoadingHint loadHint1 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ACTIVE DEFENSES HINT",
            AltUI.ObjectiveString("Active Defenses") + " are excellent against " + AltUI.EnemyString("Missiles") +
            " and " + AltUI.EnemyString("Aircraft") + ".\nJust don't expect to win a cost war with them - " +
            AltUI.HintString("they are expensive!"));
        internal static LoadingHintsExt.LoadingHint loadHint2 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ACTIVE DEFENSES HINT",
            AltUI.ObjectiveString("Active Defenses") + " prefer to stay still to track " + AltUI.EnemyString("Missiles") +
            "\nIf you intend on moving quickly, " + AltUI.ObjectiveString("Flares") + " would be the safer choice.");

        internal static LoadingHintsExt.LoadingHint loadHint3 = new LoadingHintsExt.LoadingHint(KickStart.ModID, "ACTIVE DEFENSES HINT",
            AltUI.ObjectiveString("Flares") + " distract " + AltUI.EnemyString("Missiles") +
            ", but can't do it all on their own.\n" + AltUI.HintString("Keep moving to help them out!"));



        internal static ExtUsageHint.UsageHint hintGun = new ExtUsageHint.UsageHint(KickStart.ModID, "ModulePointDefense.Gun",
            AltUI.HighlightString("Point Defenses") + " intercept incoming " + AltUI.HighlightString("Missiles") + ", and maybe even " + AltUI.HighlightString("Shells") + ".");
        internal static ExtUsageHint.UsageHint hintFlares = new ExtUsageHint.UsageHint(KickStart.ModID, "ModulePointDefense.Flares",
            AltUI.HighlightString("Flares") + " have a chance to distract incoming " + AltUI.HighlightString("Missiles") + ", and maybe even " + AltUI.HighlightString("Shells") + ".");

    }
}
