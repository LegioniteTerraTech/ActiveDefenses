using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using RandomAdditions;
#if !STEAM
using ModHelper.Config;
#else
using ModHelper;
#endif
using Nuterra.NativeOptions;

namespace ActiveDefenses
{
    public class KickStart
    {
        internal const string ModName = "ActiveDefenses";
        internal static bool IsUnstable = false;
        public static void CheckIfUnstable()
        {
            IsUnstable = SKU.DisplayVersion.TakeWhile(x => x == '.').Count() > 2;
            Debug.Log("TACtical_AI: Is this in the Unstable? - " + IsUnstable);
        }

        internal static bool isNuterraSteamPresent = false;
        internal static bool isRandAdditionsPresent = false;

        public static GameObject logMan;

        public static bool InterceptedExplode = true;   // Projectiles intercepted will explode
        public static float ProjectileHealthMultiplier = 10;

        public static bool IsIngame { get { return !ManPauseGame.inst.IsPaused && !ManPointer.inst.IsInteractionBlocked; } }

        public static void ReleaseControl(int ID)
        {
            if (GUIUtility.hotControl == ID)
            {
                GUI.FocusControl(null);
                GUI.UnfocusWindow();
                GUIUtility.hotControl = 0;
            }
        }


        private static bool patched = false;
        static Harmony harmonyInstance = new Harmony("legionite.activedefenses");
        //private static bool patched = false;

        public static bool VALIDATE_MODS()
        {
#if STEAM
            if (!LookForMod("0ModManager"))
            {
                DebugActDef.FatalError("This mod NEEDS 0ModManager to function!  Please subscribe to it on the Steam Workshop and follow the instructions carefully.");
                return false;
            }
#endif
            if (!LookForMod("0Harmony"))
            {
                DebugActDef.FatalError("This mod NEEDS Harmony to function!  Please subscribe to it on the Steam Workshop.");
                return false;
            }

            if (LookForMod("RandomAdditions"))
            {
                Debug.Log("ActiveDefenses: Found RandomAdditions!  Hooking up!");
                isRandAdditionsPresent = true;
            }
            else
            {
                DebugActDef.Assert(true, "-----------------------------\nRandomAdditions IS NOT INSTALLED!!!\n-----------------------------");
                DebugActDef.FatalError("This mod NEEDS Random Additions to function!  Please subscribe to it on the Steam Workshop.");
                return false;
            }
            if (LookForMod("NuterraSteam"))
            {
                DebugActDef.Log("ActiveDefenses: Found NuterraSteam!  Making sure blocks work!");
                isNuterraSteamPresent = true;
            }
            return true;
        }
#if STEAM
        private static bool OfficialEarlyInited = false;
        public static void OfficialEarlyInit()
        {
            //Where the fun begins
            DebugActDef.Log("ActiveDefenses: MAIN (Steam Workshop Version) startup");
            if (!VALIDATE_MODS())
            {
                return;
            }

            //Initiate the madness
            try
            { // init changes
                harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                //EdgePatcher(true);
                DebugActDef.Log("ActiveDefenses: Patched");
                patched = true;
            }
            catch (Exception e)
            {
                DebugActDef.Log("ActiveDefenses: Error on patch");
                DebugActDef.Log(e);
            }
            ProjectileManager.Initiate();


            try
            {
                KickStartOptions.TryInitOptionAndConfig();
            }
            catch (Exception e)
            {
                DebugActDef.Log("ActiveDefenses: Error on Option & Config setup");
                DebugActDef.Log(e);
            }
            try
            {
                SafeSaves.ManSafeSaves.RegisterSaveSystem(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                DebugActDef.Log("ActiveDefenses: Error on RegisterSaveSystem");
                DebugActDef.Log(e);
            }
            OfficialEarlyInited = true;
        }


        public static void MainOfficialInit()
        {
            //Where the fun begins
            DebugActDef.Log("ActiveDefenses: MAIN (Steam Workshop Version) startup");
            if (!OfficialEarlyInited)
            {
                DebugActDef.Log("ActiveDefenses: MainOfficialInit was called before OfficialEarlyInit was finished?! Trying OfficialEarlyInit AGAIN");
                OfficialEarlyInit();
            }
            DebugActDef.Log("ActiveDefenses: MainOfficialInit");

            //Initiate the madness
            if (!patched)
            {
                int patchStep = 0;
                try
                {
                    harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                    patchStep++;
                    //EdgePatcher(true);
                    DebugActDef.Log("ActiveDefenses: Patched");
                    patched = true;
                }
                catch (Exception e)
                {
                    DebugActDef.Log("ActiveDefenses: Error on patch " + patchStep);
                    DebugActDef.Log(e);
                }
            }

        }
        public static void DeInitALL()
        {
            if (patched)
            {
                try
                {
                    harmonyInstance.UnpatchAll("legionite.activedefenses");
                    //EdgePatcher(false);
                    DebugActDef.Log("ActiveDefenses: UnPatched");
                    patched = false;
                }
                catch (Exception e)
                {
                    DebugActDef.Log("ActiveDefenses: Error on UnPatch");
                    DebugActDef.Log(e);
                }
            }
        }

        // UNOFFICIAL
#else
        public static void Main()
        {
            //Where the fun begins
            DebugActDef.Log("ActiveDefenses: MAIN (TTMM Version) startup");
            if (!VALIDATE_MODS())
            {
                return;
            }

            //Initiate the madness
            try
            {
                harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                Debug.Log("ActiveDefenses: Error on patch");
                Debug.Log(e);
            }
            try
            {
                SafeSaves.ManSafeSaves.RegisterSaveSystem(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                Debug.Log("ActiveDefenses: Error on RegisterSaveSystem");
                Debug.Log(e);
            }
            ProjectileManager.Initiate();

            try
            {
                KickStartOptions.TryInitOptionAndConfig();
            }
            catch (Exception e)
            {
                Debug.Log("ActiveDefenses: Error on Option & Config setup");
                Debug.Log(e);
            }
        }
#endif
        public static bool LookForMod(string name)
        {
            if (name == "RandomAdditions")
            {
                try
                {
                    bool _ = RandomAdditions.KickStart.InterceptedExplode;
                    return true;
                }
                catch
                { 
                }
            }
            else
            {
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.StartsWith(name))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

}
