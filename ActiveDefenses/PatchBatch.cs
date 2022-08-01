﻿using System;
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
            private static void Prefix(Projectile __instance)
            {
                ProjectileManager.Remove(__instance);
                var health = __instance.GetComponent<ProjectileHealth>();
                if (health)
                    health.Reset();
            }
        }


        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("Fire")]//On Fire
        private class PatchProjectileFire
        {
            private static void Postfix(Projectile __instance, ref FireData fireData, ref ModuleWeapon weapon, ref Tank shooter)
            {

                float projSped = fireData.m_MuzzleVelocity;
                if (ProjectileHealth.IsCheaty(projSped))
                    ProjectileManager.HandleCheaty(__instance);

                var ModuleCheck2 = __instance.GetComponent<LaserProjectile>();
                var ModuleCheck3 = __instance.GetComponent<MissileProjectile>();
                if (!ModuleCheck3 && !ModuleCheck2)
                {
                    var ModuleCheck4 = __instance.GetComponent<ProjectileHealth>();
                    if (ModuleCheck4 != null)
                    {

                        if (ProjectileHealth.IsFast(projSped))
                        {
                            ProjectileManager.Add(__instance);
                            //ModuleCheck3.GetHealth(true);
                        }
                        else
                        {
                            //Debug.Log("ActiveDefenses: ASSERT - Abberation in Projectile!  " + __instance.gameObject.name);
                            UnityEngine.Object.Destroy(ModuleCheck4);
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
                else
                {
                    ProjectileManager.Add(__instance);
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