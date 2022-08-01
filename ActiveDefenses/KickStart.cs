using System;
using System.Collections.Generic;
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
#if STEAM
        private static bool OfficialEarlyInited = false;
        public static void OfficialEarlyInit()
        {
            //Where the fun begins

            //Initiate the madness
            if (LookForMod("RandomAdditions"))
            {
                Debug.Log("ActiveDefenses: Found RandomAdditions!  Hooking up!");
                isRandAdditionsPresent = true;
            }
            else
            {
                DebugActDef.Assert(true, "-----------------------------\nRandomAdditions IS NOT INSTALLED!!!\n-----------------------------");
                return;
            }
            DebugActDef.Log("ActiveDefenses: OfficialEarlyInit");
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


            if (LookForMod("NuterraSteam"))
            {
                DebugActDef.Log("ActiveDefenses: Found NuterraSteam!  Making sure blocks work!");
                isNuterraSteamPresent = true;
            }
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

            //Initiate the madness
            if (LookForMod("RandomAdditions"))
            {
                Debug.Log("ActiveDefenses: Found RandomAdditions!  Hooking up!");
                isRandAdditionsPresent = true;
            }
            else
            {
                DebugActDef.Assert(true, "-----------------------------\nRandomAdditions IS NOT INSTALLED!!!\n-----------------------------");
                return;
            }
            Harmony harmonyInstance = new Harmony("legionite.activedefenses");
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

            if (LookForMod("NuterraSteam"))
            {
                Debug.Log("ActiveDefenses: Found NuterraSteam!  Making sure blocks work!");
                isNuterraSteamPresent = true;
            }


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
                foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.FullName.StartsWith(name))
                    {
                        if (assembly.GetType("KickStart") != null)
                            return true;
                    }
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

    public class KickStartOptions
    {
        internal static ModConfig config;

        // NativeOptions Parameters
        public static OptionToggle unused;
        public static OptionRange unused2;

        private static bool launched = false;

        public static void TryInitOptionAndConfig()
        {
            if (launched)
                return;
            launched = true;
            //Initiate the madness
            try
            {
                ModConfig thisModConfig = new ModConfig();
                thisModConfig.BindConfig<KickStart>(null, "DebugPopups");
                NativeOptionsMod.onOptionsSaved.AddListener(() => { config.WriteConfigJsonFile(); });
            }
            catch (Exception e)
            {
                DebugActDef.Log("ActiveDefenses: Error on Option & Config setup");
                DebugActDef.Log(e);
            }

        }
    }
}
