using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ActiveDefenses
{
    class PatchBatch
    {
    }
#if STEAM
    public class KickStartActiveDefenses : ModBase
    {
        internal static KickStartActiveDefenses oInst = null;

        bool isInit = false;
        bool firstInit = false;

        /*
        public static Type[] EarlyLoadAfter()
        {
            try
            {
                return new Type[] { typeof(RandomAdditions.KickStartRandomAdditions) };
            }
            catch
            {
                DebugActDef.FatalError("This mod NEEDS Random Additions to function!  Please subscribe to it on the Steam Workshop.");
                return new Type[] { };
            }
        }

        public static Type[] LoadAfter()
        {
            try
            {
                return new Type[] { typeof(RandomAdditions.KickStartRandomAdditions) };
            }
            catch
            {
                DebugActDef.FatalError("This mod NEEDS Random Additions to function!  Please subscribe to it on the Steam Workshop.");
                return new Type[] { };
            }
        }*/


        public override bool HasEarlyInit()
        {
            DebugActDef.Log("ActiveDefenses: CALLED");
            return true;
        }

        // IDK what I should init here...
        public override void EarlyInit()
        {
            DebugActDef.Log("ActiveDefenses: CALLED EARLYINIT");
            if (oInst == null)
            {
                KickStart.OfficialEarlyInit();
                oInst = this;
            }
        }
        public override void Init()
        {
            DebugActDef.Log("ActiveDefenses: CALLED INIT");
            if (isInit)
                return;
            if (oInst == null)
                oInst = this;

            try
            {
                TerraTechETCUtil.ModStatusChecker.EncapsulateSafeInit("Active Defenses",
                    KickStart.MainOfficialInit, KickStart.DeInitALL);
            }
            catch { }
            isInit = true;
        }
        public override void DeInit()
        {
            if (!isInit)
                return;
            KickStart.DeInitALL();
            isInit = false;
        }
    }
#endif

    internal static class Patches
    {
        // Disable firing when intercepting
        [HarmonyPatch(typeof(ModuleWeapon))]
        [HarmonyPatch("Process")]
        private static class ProcessOverride
        {
            private static bool Prefix(ModuleWeapon __instance, ref int __result)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<ModulePointDefense>();
                if (ModuleCheck != null)
                {
                    if (ModuleCheck.FireControlUsingWeaponGun)
                    {
                        __result = 0;
                        return false;
                    }
                }
                return true;
            }
        }


        /*
        [HarmonyPatch(typeof(MissileProjectile))]
        [HarmonyPatch("OnSpawn")]//On Creation
        private class PatchProjectileSpawn
        {
            private static void Prefix(Projectile __instance)
            {
                ProjectileManager.Add(__instance);
            }
        }*/

        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("OnRecycle")]
        private class PatchProjectileRemove
        {
            private static void Postfix(Projectile __instance)
            {
                ProjectileManager.Remove(__instance);
                var health = __instance.GetComponent<ProjectileHealth>();
                if (health)
                    health.Reset();
            }
        }
        /*
        [HarmonyPatch(typeof(MissileProjectile))]
        [HarmonyPatch("OnRecycle")]
        private class PatchProjectileRemove2
        {
            private static void Prefix(MissileProjectile __instance)
            {
                ProjectileManager.Remove(__instance);
                var health = __instance.GetComponent<ProjectileHealth>();
                if (health)
                    health.Reset();
            }
        }
        */

        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("Fire")]//On Fire
        private class PatchProjectileFire
        {
            private static void Postfix(Projectile __instance, ref FireData fireData, ref ModuleWeapon weapon, ref Tank shooter)
            {

                float projSped = fireData.m_MuzzleVelocity;
                if (ProjectileHealth.IsCheaty(projSped))
                    ProjectileManager.HandleCheaty(__instance);

                if (__instance.GetComponent<MissileProjectile>())
                {
                    ProjectileManager.Add(__instance);
                }
                else if (!__instance.GetComponent<LaserProjectile>())
                {   // Cannot hit lasers dammit
                    var health = __instance.GetComponent<ProjectileHealth>();
                    if (health != null)
                    {
                        if (ProjectileHealth.IsFast(projSped))
                        {
                            ProjectileManager.Add(__instance);
                            //ModuleCheck3.GetHealth(true);
                        }
                        else
                        {
                            //Debug.Log("ActiveDefenses: ASSERT - Abberation in Projectile!  " + __instance.gameObject.name);
                            UnityEngine.Object.Destroy(health);
                        }
                    }
                    else
                    {
                        if (ProjectileHealth.IsFast(projSped))
                        {
                            ProjectileManager.Add(__instance);
                            //ModuleCheck3.GetHealth(true);
                        }
                    }
                }
            }
        }


        [HarmonyPatch(typeof(SeekingProjectile))]
        [HarmonyPatch("OnPool")]
        private class PatchPooling
        {
            private static void Postfix(SeekingProjectile __instance)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<InterceptProjectile>();
                if (ModuleCheck != null)
                {
                    ModuleCheck.GrabValues();
                }
            }
        }

        [HarmonyPatch(typeof(SeekingProjectile))]
        [HarmonyPatch("FixedUpdate")]
        private class PatchHomingForIntercept
        {
            private static bool Prefix(SeekingProjectile __instance)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<DistractedProjectile>();
                if (ModuleCheck != null)
                {
                    if (ModuleCheck.Distracted(__instance))
                        return false;
                }
                var ModuleCheck2 = __instance.gameObject.GetComponent<InterceptProjectile>();
                if (ModuleCheck2 != null)
                {
                    if (ModuleCheck2.Aiming)
                    {
                        if (ModuleCheck2.OverrideAiming(__instance))
                            if (ModuleCheck2.ForcedAiming)
                                return false;
                        if (ModuleCheck2.OnlyDefend)
                            return false;
                    }
                }
                return true;
            }
        }

    }
}
