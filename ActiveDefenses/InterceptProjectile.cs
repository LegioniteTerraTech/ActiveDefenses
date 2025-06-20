﻿using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

public class InterceptProjectile : ActiveDefenses.InterceptProjectile { };
namespace RandomAdditions
{
    public class InterceptProjectile : ActiveDefenses.InterceptProjectile { };
}

namespace ActiveDefenses 
{
    //Allows a projectile to collide with Projectiles and home in on MissileProjectile
    /*
        "InterceptProjectile":{ // Add a special movement effect to your projectile
            // General
            "InterceptRange": 2, // The range when this projectile applies it's damage
            "InterceptedExplode": true, // Any projectile intercepted by this will explode (no damage)
            "StartDelay": 3,     // The frame delay before the projectile starts intercepting [3 - 255]
            "PointDefDamage":   0, // How much damage to apply when target projectile is hit 
                                   // - leave at zero to use the WeaponRound damage
            
            // Jammers / Flares
            "IsFlare": false,           // Will fool MissileProjectiles into heading after itself
            "DistractsMoreThanOne": true,  // Distract more than one projectile
            "ConstantDistract": false,  // Keep trying to fool projectiles after launch
            "DistractChance": 10,        // The chance to fool the target projectile (out of 100)

            // SeekingProjectile
            "ForcedAiming": false, // If there's a projectile, this will always aim at it first
            "Aiming":       true,  // If there's a projectile, this will aim at it if there's no enemy in range
            "OnlyDefend":   false, // will not home in on enemies
            "InterceptMultiplier": 3,// How much to multiply the aiming strength if targeting a missile
        },
    */
    public class InterceptProjectile : ExtProj
    {
        public bool IsFlare = false;
        public bool ConstantDistract = false;
        public bool DistractsMoreThanOne = true;
        public float DistractChance = 10;

        public bool ForcedAiming = false;
        public bool Aiming = false;
        public bool OnlyDefend = false;
        public int StartDelay = 3;
        public float InterceptMultiplier = 3;
        public float PointDefDamage = 0;
        public bool InterceptedExplode = true;
        public float InterceptRange = 2;

        private static FieldInfo rotRate = typeof(SeekingProjectile).GetField("m_TurnSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo aimAtTarg = typeof(SeekingProjectile).GetField("m_ApplyRotationTowardsTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo range = typeof(SeekingProjectile).GetField("m_VisionRange", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static FieldInfo deals = typeof(WeaponRound).GetField("m_Damage", BindingFlags.NonPublic | BindingFlags.Instance);

        public Transform trans;
        public int team = -1;
        public ModulePointDefense MPD = null;
        private bool init = false;
        private bool hitFast = false;
        private byte startDelay = 3;
        private byte timer = 3;
        private bool AimAtTarg = false;
        private float Range = 50;
        private float RotRate = 50;

        private Rigidbody LockedTarget;
        public override void Pool()
        {
            GrabValues();
        }

        public override void Fire(FireData fireData)
        {
            var pd = fireData.GetComponent<ModulePointDefense>();
            MPD = pd;
            if ((bool)pd)
                Reset(pd.Target, pd.CanInterceptFast);
            else
                Reset();
        }

        public void Reset(Rigidbody target = null, bool hitFast = false)
        {
            this.hitFast = hitFast;
            LockedTarget = target;
            timer = startDelay;
            init = false;
            var teamM = PB.project.Shooter;
            if (teamM)
                team = teamM.Team;
        }
        public void GrabValues()
        {
            if (init)
                return;
            if (StartDelay > byte.MaxValue || startDelay < 3)
            {
                BlockDebug.ThrowWarning(true, "ActiveDefenses: \nInterceptProjectile StartDelay must be within [3 - 255]\nCause of error - Projectile " + gameObject.name);
            }
            else
                startDelay = (byte)StartDelay;
            trans = gameObject.transform;
            if (PointDefDamage <= 0)
            {
                var dmg = gameObject.GetComponent<WeaponRound>();
                if (dmg)
                    PointDefDamage = (int)deals.GetValue(dmg);
            }
            var seeking = gameObject.GetComponent<SeekingProjectile>();
            if (!gameObject.GetComponent<SeekingProjectile>())
            {
                enabled = true;
                return;
            }
            else if (!gameObject.GetComponent<SeekingProjectile>().isActiveAndEnabled)
            {
                enabled = true;
                return;
            }
            enabled = false;
            RotRate = (float)rotRate.GetValue(seeking);
            AimAtTarg = (bool)aimAtTarg.GetValue(seeking);
            Range = (float)range.GetValue(seeking);
            init = true;
            //Debug.Log("ActiveDefenses: Launched InterceptProjectile on " + gameObject.name);
        }
        public bool OverrideAiming(SeekingProjectile seeking)
        {
            if (!init)
                GrabValues();
            if (!FindAndHome(out Vector3 posOut))
                return false;
            Vector3 vec = posOut - trans.position;
            Vector3 directed = Vector3.Cross(PB.rbody.velocity, vec).normalized;
            float b = Vector3.Angle(seeking.transform.forward, vec);
            Quaternion quat = Quaternion.AngleAxis(Mathf.Min(RotRate * InterceptMultiplier * Time.deltaTime, b), directed);
            PB.rbody.velocity = quat * PB.rbody.velocity;
            if (AimAtTarg)
            {
                Quaternion rot = quat * PB.rbody.rotation;
                PB.rbody.MoveRotation(rot);
            }
            return true;
        }
        public bool FindAndHome(out Vector3 posOut)
        {
            posOut = Vector3.zero;
            try
            {
                if (!GetOrTrack(out Rigidbody rbodyT))
                    return false;

                float sqrDist = (rbodyT.position - PB.rbody.position).sqrMagnitude;
                if (sqrDist < InterceptRange * InterceptRange)
                {
                    try
                    {
                        var targ = LockedTarget.GetComponent<ProjectileHealth>();
                        if (!(bool)targ)
                            targ = LockedTarget.gameObject.AddComponent<ProjectileHealth>();
                        targ.TakeDamage(PointDefDamage, InterceptedExplode);
                        ProjBase.ExplodeNoDamage(PB.project); //Blows up THE InterceptProjectile
                        Recycle();
                    }
                    catch
                    {
                        DebugActDef.Log("ActiveDefenses: InterceptProjectile - Target found but has no ProjectileHealth!?");
                    }
                }
                if (sqrDist < Range / 4)
                {
                    posOut = rbodyT.position;
                }
                else
                    posOut = rbodyT.position + (rbodyT.velocity * Time.deltaTime);
                //Debug.Log("ActiveDefenses: InterceptProjectile - Homing at " + posOut);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public bool GetOrTrack(out Rigidbody rbodyT)
        {
            bool update = false;
            bool updateBullet = false;
            if ((bool)LockedTarget)
            {
                if (timer <= 3)
                {
                    if (IsFlare && ConstantDistract)
                    {
                        if (UnityEngine.Random.Range(1, 100) <= DistractChance)
                            DistractedProjectile.Distract(LockedTarget, PB.rbody);
                    }
                    update = IsFlare || !enabled;
                    updateBullet = true;
                    timer = 7;
                }
                timer--;
                if (!LockedTarget.IsSleeping())
                {
                    if ((LockedTarget.position - PB.rbody.position).sqrMagnitude > Range * Range)
                    {
                        LockedTarget = null;
                    }
                    else
                    {
                        rbodyT = LockedTarget;
                        return true;
                    }
                }
            }
            else if (MPD != null && MPD.Target != null)
                LockedTarget = MPD.Target;

            if (updateBullet)
            {   // Get target from TankPointDefense
                if ((bool)PB.project?.Shooter)
                {
                    var PD = PB.project.Shooter.GetComponent<TankPointDefense>();
                    if ((bool)PD)
                    {
                        if (PD.GetFetchedTargetsNoScan(out List<Rigidbody> rbodys, !hitFast))
                        {
                            if (rbodys.Count > 2)
                            {
                                Rigidbody rbodyCatch = rbodys[1];
                                rbodyT = rbodyCatch;
                                LockedTarget = rbodyCatch;
                                //Debug.Log("ActiveDefenses: InterceptProjectile - LOCK");
                                if (IsFlare)
                                {
                                    if (UnityEngine.Random.Range(1, 100) <= DistractChance)
                                    {
                                        if (DistractsMoreThanOne)
                                            DistractedProjectile.DistractAll(rbodys, PB.rbody);
                                        else
                                            DistractedProjectile.Distract(rbodyCatch, PB.rbody);

                                    }
                                }
                                return true;
                            }
                        }
                    }
                }
            }
            else if (update)
            {   // Try get target from global projectiles
                if (ProjectileManager.GetClosestProjectile(this, Range, out Rigidbody rbodyCatch2, out List<Rigidbody> rbodys))
                {
                    rbodyT = rbodyCatch2;
                    LockedTarget = rbodyCatch2;
                    //Debug.Log("ActiveDefenses: InterceptProjectile - LOCK");
                    if (IsFlare)
                    {
                        if (UnityEngine.Random.Range(1, 100) <= DistractChance)
                        {
                            if (DistractsMoreThanOne)
                                DistractedProjectile.DistractAll(rbodys, PB.rbody);
                            else
                                DistractedProjectile.Distract(rbodyCatch2, PB.rbody);

                        }
                    }
                    return true;
                }
            }
            rbodyT = null;
            return false;
        }
        private void Update()
        {   // standalone update
            if (!init)
                GrabValues();
            FindAndHome(out _);
        }

    }
}
