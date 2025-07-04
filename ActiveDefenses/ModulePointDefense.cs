﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

[RequireComponent(typeof(ModuleEnergy))]
public class ModulePointDefense : ActiveDefenses.ModulePointDefense { };

namespace RandomAdditions
{
    public class ModulePointDefense : ActiveDefenses.ModulePointDefense { };
}

namespace ActiveDefenses
{
    // A block module that shoots beams or projectiles that hit hostile projectiles
    //   If ModuleWeaponGun is present, this will override that when a MissileProjectile is present 
    /*
    "RandomAdditions.ModulePointDefense": { // A block module that shoots beams that hit hostile projectiles
        "DefendOnly": false,        // Do not fire on spacebar
        "CanInterceptFast": false,  // Can this also shoot fast projectiles?
        "SmartManageTargets": false,// Can this smartly manage targets? Better for clusters of same projectiles
                                // but suffers against diverse projectile salvos
        "ForcePulse": false,        // Force the hitscan pulse effect
        "SpoolOnEnemy": true,       // Spin the barrels when an enemy is in range
        "LockOnDelay": 8,           // Frames this will not track for - Set to 0 to maximize scanning rate
            // WARNING - May negatively impact performance under 8!
        "LockOnStrength": 15,       // Will to keep lock on a projectile that's fast and/or far
            // WARNING - May negatively impact performance under 10!
        "LockOnTooFastSpeed": 1.00,   //If the projectile is this percent above speed, then we aim direct
        "DefenseCooldown": 1,       // How long until it fires the next intercept
        "DefenseEnergyCost": 0,     // How much it takes to maintain passive defense
        "DefendRange": 50,          // The range of which this can find and track projectiles
        "RotateRate": 50,           // How fast we should rotate the turret when dealing with a projectile
        "ShareFireSFX": true,       // Share the firing noise with ModuleWeapon 
        // - Note this is almost always needed for guns with looping audio (guns with visible spinning parts like the HE Autocannon or BF Gatling Laser)
        "FireSFXType": 2,           // Same as ModuleWeapon but for Pulse. Ignored when ShareFireSFX is true

        // Pulse Beam effect (hitscan mode)
        "PulseAimCone": 15,        // The max aiming rotation: Input Value [1-100] ~ Degrees(5-360)
        "AllAtOnce": true,         // Will this fire all lasers at once
        "HitChance": 45,           // Out of 100
        "PointDefenseDamage": 1,   // How much damage to deal to the target projectile
        "PulseEnergyCost": 0,      // How much it takes to fire a pulse
        "ExplodeOnHit": true,         // Make the target projectile explode on death (without dealing damage)
        "PulseSizeStart": 0.5,     // Size of the beam at the launch point
        "PulseSizeEnd": 0.2,       // Size of the beam at the end point
        "PulseLifetime": 0,        // How long the pulse VISUAL persists - leave at zero for one frame
        "OverrideMaterial": null,  // If you want to use custom textures for your beam
        "DefenseColorStart": {"r": 0.05, "g": 1, "b": 0.3,"a": 0.8},
        "DefenseColorEnd": {"r": 0.05, "g": 1, "b": 0.3, "a": 0.8},
    
        // SeperateFromGun set to true or Without ModuleWeaponGun attachment
        "MaxPulseTargets": 1,       // The number of projectiles this can deal with when firing

        // ModuleWeaponGun attachment
        "SeperateFromGun": false,        // Handle this seperately - Will also set ForcePulse to true
        "OverrideEnemyAiming": false,    // Will this prioritize projectiles over the enemy? - Also allow firing when spacebar is pressed

        // ChildModuleWeapon
        "UseChildModuleWeapon": false,   // Use the FIRST ChildModuleWeapon in hierachy instead
    },
     */

    [RequireComponent(typeof(ModuleEnergy))]
    public class ModulePointDefense : ExtModule, TechAudio.IModuleAudioProvider
    {
        // General parameters
        public bool DefendOnly = false;
        /// <summary>
        /// Can shoot any kind of projectile
        /// </summary>
        public bool CanInterceptFast = false;       // Can this also shoot fast projectiles?
        public bool AllAtOnce = true;
        public bool ForcePulse = false;
        public bool SpoolOnEnemy = true;
        public int LockOnDelay = 8;
        public float LockOnStrength = 15;
        public float LockOnTooFastSpeed = 1f;    //If the projectile is this percent above speed, then we aim direct
        public float RotateRate = 50;
        public float DefendRange = 50;
        public float DefenseCooldown = 0.5f;   // was 1, lowered to 0.5f
        public float DefenseEnergyCost = 0;
        public bool ExplodeOnHit = false;
        public bool ShareFireSFX = false;
        public TechAudio.SFXType FireSFXType = TechAudio.SFXType.LightMachineGun;

        // Pulse Parameters
        public bool SmartManageTargets = false;
        public float PulseAimCone = 15;             // out of 100
        public float HitChance = 45;                // out of 100
        public float PulseEnergyCost = 0;
        public float PointDefenseDamage = 1;
        public float PulseSizeStart = 0.5f;
        public float PulseSizeEnd = 0.2f;
        public float PulseLifetime = 0;
        public Material OverrideMaterial = null;
        public Color DefenseColorStart = new Color(0.05f, 1f, 0.3f, 0.8f);
        public Color DefenseColorEnd = new Color(0.05f, 1f, 0.3f, 0.8f);
        public int MaxPulseTargets = 1;

        // ModuleWeaponGun attachment
        public bool SeperateFromGun = false;    // Use it seperate
        public bool OverrideEnemyAiming = false;    // Will this prioritize projectiles over the enemy?

        // ChildModuleWeapon attachment
        public bool UseChildModuleWeapon = false;   // If SeperateFromGun is not true and ChildModuleWeapon 
        //  is present somewhere, we can use that instead of ModuleWeaponGun.

        // Handled
        public bool ThisControllingWeaponGun = false;
        public bool FireControlUsingWeaponGun = false;

        private bool cacheDisabled = false;

        private int timer = 0;
        private float cooldown = 0;
        private int barrelsFired = 0;
        private int barrelStep = 0;
        private int barrelC = 0;
        private bool energyUser = false;
        private bool firing = false;
        private bool firingCache = false;
        private bool spooling = false;
        private float pulseAimAnglef;

        internal TankPointDefense def;
        private Transform fireTrans;
        private ModuleWeapon gunSFX;
        private IModuleWeapon gunBase;
        private ModuleEnergy energy;
        private List<GimbalAimer> aimers;
        private TargetAimer aimerMain;
        //private List<CannonBarrel> barrels;
        private Rigidbody LockedTarget;

        public TechAudio.SFXType SFXType => FireSFXType;
        public event Action<TechAudio.AudioTickData, FMODEvent.FMODParams> OnAudioTickUpdate;
        public Rigidbody Target => LockedTarget;

        private float energyToTax = 0;

        internal bool AimingDefense => (UseChildModuleWeapon && gunBase != null) || (aimers != null && !SeperateFromGun);

        protected override void Pool()
        {
            fireTrans = transform.HeavyTransformSearch("_fireTrans");
            if (fireTrans == null)
                fireTrans = gameObject.transform;

            gunSFX = GetComponent<ModuleWeapon>();
            if (UseChildModuleWeapon)
                gunBase = GetComponentInChildren<IChildModuleWeapon>();
            else
            {
                gunBase = GetComponent<ModuleWeaponGun>();
                aimerMain = GetComponent<TargetAimer>();
                aimers = GetComponentsInChildren<GimbalAimer>().ToList();
            }
            energy = GetComponent<ModuleEnergy>();
            if ((bool)energy)
                energy.UpdateConsumeEvent.Subscribe(OnDrain);
            if (gunBase != null)
            {
                if (gunBase is ModuleWeaponGun MWG)
                    barrelC = MWG.GetNumCannonBarrels();
                else if (gunBase is IChildModuleWeapon CMW)
                    barrelC = CMW.GetBarrelsMainCount();
            }
            else
                SeperateFromGun = true;
            if (SeperateFromGun)
                ForcePulse = true;
            if ((bool)gunSFX)
            {
                if (ShareFireSFX)
                    FireSFXType = gunSFX.m_FireSFXType;
            }
            else
                ShareFireSFX = false;
            barrelStep = 0;
            if (PulseAimCone > 100 || PulseAimCone < 1)
            {
                PulseAimCone = 15;
                BlockDebug.ThrowWarning(false, "ModulePointDefense: Turret " + block.name + " has a PulseAimCone out of range!  Make sure it's a value within or including Input Value [1-100] ~ Degrees(5-360)");
            }
            pulseAimAnglef = 1 - (PulseAimCone / 50);
            if (!ForcePulse)
            {
                if ((bool)GetComponent<FireData>()?.m_BulletPrefab?.GetComponent<InterceptProjectile>())
                {
                    var IProj = GetComponent<FireData>().m_BulletPrefab.GetComponent<InterceptProjectile>();
                    if (PointDefenseDamage == 1)
                        PointDefenseDamage = IProj.PointDefDamage;
                    energyUser = PulseEnergyCost > 0;
                    return;
                }
                BlockDebug.ThrowWarning(false, "ModulePointDefense: Turret " + block.name + "'s FireData.m_BulletPrefab needs InterceptProjectile to work properly!");
            }
            else
            {
                if (!(bool)OverrideMaterial)
                    OverrideMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            energyUser = PulseEnergyCost > 0;
            //DebugRandAddi.Log("ActiveDefenses: ModulePointDefense - Registered on block " + TankBlock.name + " ModuleWeaponGun: " + (bool)gunBase);
        }
        public void OnDrain()
        {
            if (energyToTax > 0)
            {
                energy.ConsumeUpToMax(TechEnergy.EnergyType.Electric, energyToTax);
                energyToTax = 0;
            }
        }

        public override void OnGrabbed()
        {
            InterceptProjectile IP = gunSFX?.GetComponent<FireData>()?.m_BulletPrefab?.GetComponent<InterceptProjectile>();
            if (IP && IP.IsFlare)
                DefensesWiki.hintFlares.Show();
            else
                DefensesWiki.hintGun.Show();
        }
        public override void OnAttach()
        {
            barrelStep = 0;
            block.tank.TechAudio.AddModule(this);
            TankPointDefense.HandleAddition(tank, this);
        }
        public override void OnDetach()
        {
            barrelStep = 0;
            block.tank.TechAudio.RemoveModule(this);
            TankPointDefense.HandleRemoval(tank, this);
        }
        FieldInfo recoiled = typeof(CannonBarrel).GetField("recoiling", BindingFlags.NonPublic | BindingFlags.Instance);
        private void UpdateLockOn(out bool targDestroyed)
        {
            targDestroyed = false;
            barrelsFired = 0;
            if (LockedTarget == null)
            {
                ThisControllingWeaponGun = false;
                spooling = false;

                if (cacheDisabled == ThisControllingWeaponGun)
                {
                    FireControlUsingWeaponGun = ThisControllingWeaponGun;
                }
                else
                {
                    LockOnFireSFXHalt();
                }
                return;
            }
            if (AimingDefense)
            {
                Vector3 posAim = GetTargetHeading();
                if (aimerMain != null)
                {   // Try correct scale errors
                    posAim.Scale(transform.lossyScale);
                    aimerMain.AimAtWorldPos(posAim, RotateRate);
                }
                else
                {
                    if (UseChildModuleWeapon && gunBase is IChildModuleWeapon CMW)
                    {
                        barrelC = CMW.GetBarrelsMainCount();
                        posAim.Scale(transform.lossyScale);
                        CMW.OverrideAndAimAt(posAim, !ForcePulse);
                    }
                    else
                    {
                        foreach (GimbalAimer aim in aimers)
                        {
                            aim.Aim(posAim, RotateRate);
                        }
                    }
                }

                if (gunBase != null)
                {
                    if (!SpoolOnEnemy)
                        gunBase.UpdateDeployment(true);
                    spooling = true;
                    if (gunBase.PrepareFiring(true))
                    {
                        if (!gunBase.FiringObstructed())
                        {
                            // Proceed to firing
                            firing = LockOnFire(out targDestroyed);
                        }
                    }
                }
            }
            else // Just use a centralized transform
            {
                ThisControllingWeaponGun = false;
                firing = LockOnFireSimple();
                targDestroyed = true;
                spooling = true;
            }
            if (cacheDisabled == ThisControllingWeaponGun)
            {
                LockOnFireSFX();
                FireControlUsingWeaponGun = ThisControllingWeaponGun;
            }
            else
            {
                LockOnFireSFXHalt();
            }
        }
        private void UpdateLockOnImmedeate(out bool killed)
        {
            killed = false;
            
            if (cooldown > 0)
            {
                //DebugRandAddi.Log("ActiveDefenses: " + def.name + " - Recharging");
                barrelsFired = 0;
                return;
            }
            if (LockedTarget == null)
            {
                ThisControllingWeaponGun = false;
                spooling = false;

                if (cacheDisabled == ThisControllingWeaponGun)
                {
                    FireControlUsingWeaponGun = ThisControllingWeaponGun;
                }
                else
                {
                    LockOnFireSFXHalt();
                }
                return;
            }
            ThisControllingWeaponGun = false;
            //DebugRandAddi.Log("ActiveDefenses: " + def.name + " - AIMING AT PROJECTILE");
            if (Vector3.Dot((LockedTarget.position - fireTrans.position).normalized, fireTrans.forward) >= pulseAimAnglef)
            {
                //DebugRandAddi.Log("ActiveDefenses: " + def.name + " - FIRING AT PROJECTILE");
                if (FirePulseBeam(fireTrans, LockedTarget))
                {
                    //DebugRandAddi.Log("ActiveDefenses: " + def.name + " - PROJECTILE DESTROYED");
                    killed = true;
                }
                cooldown = DefenseCooldown;
                barrelsFired++;
            }
            spooling = true;

            if (cacheDisabled == ThisControllingWeaponGun)
            {
                LockOnFireSFX();
                FireControlUsingWeaponGun = ThisControllingWeaponGun;
            }
            else
            {
                LockOnFireSFXHalt();
            }
        }

        private bool LockOnFire(out bool targDestroyed)
        {
            targDestroyed = false;
            if (cooldown <= 0)
                cooldown = DefenseCooldown;
            else
                return false;
            if (LockedTarget == null)
                return false;
            if (!ForcePulse)
            { // fire like normal
                barrelsFired = gunBase.ProcessFiring(true);
                if (barrelsFired > 0)
                {
                    if (LockedTarget.GetComponent<ProjectileHealth>())
                        if (LockedTarget.GetComponent<ProjectileHealth>().WillDestroy(PointDefenseDamage))
                            targDestroyed = true;
                    return true;
                }
                return false;
            }
            try
            {
                // We assume that we want to use the laser instead
                Vector3 aimPoint = LockedTarget.position;
                bool fired = false;
                if (AllAtOnce)
                {
                    for (int step = 0; step < barrelC; step++)
                    {
                        if (LockOnFireQueueBarrel(aimPoint, step, out targDestroyed))
                            fired = true;
                    }
                }
                else
                {
                    fired = LockOnFireQueueBarrel(aimPoint, barrelStep, out targDestroyed);
                    if (barrelStep == barrelC - 1)
                        barrelStep = 0;
                    else
                        barrelStep++;
                }
                return fired;
            }
            catch
            {
                DebugActDef.Log("ActiveDefenses: ModulePointDefense - LockOnFire target is valid but position is illegally null");
                return false;
            }
        }
        private bool LockOnFireQueueBarrel(Vector3 aimPoint, int barrelNum, out bool targDestroyed)
        {
            if (gunBase is ModuleWeaponGun MWG)
            {
                CannonBarrel barry = MWG.FindCannonBarrelFromIndex(barrelNum);
                if (Vector3.Dot((aimPoint - barry.projectileSpawnPoint.position).normalized, barry.projectileSpawnPoint.forward) > pulseAimAnglef)
                {
                    if ((bool)barry.muzzleFlash)
                        barry.muzzleFlash.Fire();
                    if ((bool)barry.recoiler)
                    {
                        var anim = barry.recoiler.GetComponentsInChildren<Animation>(true).FirstOrDefault();
                        if ((bool)anim)
                        {
                            recoiled.SetValue(barry, true);
                            if (anim.isPlaying)
                            {
                                anim.Rewind();
                            }
                            else
                            {
                                anim.Play();
                            }
                        }
                    }

                    targDestroyed = FirePulseBeam(barry.projectileSpawnPoint, aimPoint);
                    barrelsFired++;
                    return true;
                }
            }
            else if (gunBase is IChildModuleWeapon CMW)
            {
                IChildWeapBarrel barry = CMW.GetBarrel(barrelNum);
                if (Vector3.Dot((aimPoint - barry.GetBulletTrans().position).normalized, barry.GetBulletTrans().forward) > pulseAimAnglef)
                {
                    if ((bool)barry.GetFlashTrans())
                        barry.GetFlashTrans().Fire();
                    if ((bool)barry.GetRecoilTrans())
                    {
                        var anim = barry.GetRecoilTrans().GetComponentsInChildren<Animation>(true).FirstOrDefault();
                        if ((bool)anim)
                        {
                            recoiled.SetValue(barry, true);
                            if (anim.isPlaying)
                            {
                                anim.Rewind();
                            }
                            else
                            {
                                anim.Play();
                            }
                        }
                    }

                    targDestroyed = FirePulseBeam(barry.GetBulletTrans(), aimPoint);
                    barrelsFired++;
                    return true;
                }
            }
            targDestroyed = false;
            return false;
        }
        private bool LockOnFireSimple()
        {
            if (cooldown <= 0)
                cooldown = DefenseCooldown;
            else
                return false;
            bool fired = false;
            if (def.GetFetchedTargets(-1, out List<Rigidbody> rbodys, !CanInterceptFast))
            {
                int tokens = MaxPulseTargets;
                if (tokens == 0)
                    return false;

                foreach (Rigidbody rbody in rbodys)
                {
                    if (tokens <= 0)
                        return fired;
                    if (!rbody.position.Approximately(Vector3.zero))
                    {
                        if (Vector3.Dot((rbody.position - fireTrans.position).normalized, fireTrans.forward) < pulseAimAnglef)
                            continue;
                        FirePulseBeam(fireTrans, rbody);
                        barrelsFired++;
                        fired = true;
                        tokens--;
                    }
                }
            }
            return fired;
        }
        private void LockOnFireSFX()
        {
            try
            {

                if ((!ThisControllingWeaponGun && ShareFireSFX)) //|| (firingCache != firing))
                {
                    LockOnFireSFXHalt();
                    return;
                }
                if (OnAudioTickUpdate != null)
                {
                    TechAudio.AudioTickData audioTickData = default;
                    if (ShareFireSFX)
                    {
                        audioTickData.block = block;
                        audioTickData.provider = gunSFX;
                    }
                    else
                    {
                        audioTickData.block = block; // only need pos
                        audioTickData.provider = this;
                    }
                    audioTickData.sfxType = FireSFXType;
                    audioTickData.numTriggered = barrelsFired;
                    audioTickData.triggerCooldown = DefenseCooldown;
                    audioTickData.isNoteOn = spooling;
                    audioTickData.adsrTime01 = firing ? 1 : 0;
                    TechAudio.AudioTickData value = audioTickData;
                    OnAudioTickUpdate.Send(value, FMODEvent.FMODParams.empty);
                }
            }
            catch { }
        }
        private void LockOnFireSFXHalt()
        {
            try
            {
                if (OnAudioTickUpdate != null)
                {
                    TechAudio.AudioTickData audioTickData = default;
                    if (ShareFireSFX)
                    {
                        audioTickData.block = block;
                        audioTickData.provider = gunSFX;
                    }
                    else
                    {
                        audioTickData.block = block; // only need pos
                        audioTickData.provider = this;
                    }
                    audioTickData.sfxType = FireSFXType;
                    audioTickData.numTriggered = 0;
                    audioTickData.triggerCooldown = DefenseCooldown;
                    audioTickData.isNoteOn = false;
                    audioTickData.adsrTime01 = 0;
                    TechAudio.AudioTickData value = audioTickData;
                    OnAudioTickUpdate.Send(value, FMODEvent.FMODParams.empty);
                }
            }
            catch { }
        }


        public void Update()
        {
            if (block.tank && cooldown > 0)
                cooldown -= Time.deltaTime;
        }

        public bool TryInterceptProjectile(bool enemyNear, ref int index, ref bool noTargetsLeft, out bool targDestroyed)
        {
            targDestroyed = false;

            firingCache = firing;
            firing = false;

            cacheDisabled = ThisControllingWeaponGun;
            ThisControllingWeaponGun = false;

            if (!(bool)block.tank || noTargetsLeft && energyUser)
                return false;
            if (gunBase != null)
            {
                if (SpoolOnEnemy && enemyNear)
                {
                    if (!UseChildModuleWeapon)
                        ThisControllingWeaponGun = true;
                    gunBase.UpdateDeployment(true);
                    spooling = true;
                    gunBase.PrepareFiring(true);
                }
                else
                    spooling = false;
                if (OverrideEnemyAiming)
                {
                    if (GetProjectile(ref index, ref noTargetsLeft))
                    {
                        if (!SeperateFromGun && !UseChildModuleWeapon)
                            ThisControllingWeaponGun = true;
                        UpdateLockOn(out targDestroyed);
                        return true;
                    }
                    else if (block.tank.control.FireControl)
                    {
                        firing = false;
                        ThisControllingWeaponGun = false;
                    }
                    else
                        firing = false;
                }
                else
                {
                    if (block.tank.control.FireControl)
                    {
                        firing = false;
                        ThisControllingWeaponGun = false;
                    }
                    else if (GetProjectile(index))
                    {
                        if (!SeperateFromGun && !UseChildModuleWeapon)
                            ThisControllingWeaponGun = true;
                        UpdateLockOn(out targDestroyed);
                        return true;
                    }
                    else
                        firing = false;
                }
            }
            else
            {
                if (SpoolOnEnemy)
                {
                    spooling = true;
                }
                else
                    spooling = false;
                if (OverrideEnemyAiming)
                {
                    if (GetProjectile(index))
                    {
                        if (!SeperateFromGun)
                            ThisControllingWeaponGun = true;
                        UpdateLockOn(out targDestroyed);
                        return true;
                    }
                    else if (block.tank.control.FireControl)
                    {
                        firing = false;
                        ThisControllingWeaponGun = false;
                    }
                    else
                        firing = false;
                }
                else
                {
                    if (block.tank.control.FireControl)
                    {
                        firing = false;
                        ThisControllingWeaponGun = false;
                    }
                    else if (GetProjectile(index))
                    {
                        if (!SeperateFromGun)
                            ThisControllingWeaponGun = true;
                        UpdateLockOn(out targDestroyed);
                        return true;
                    }
                    else
                        firing = false;
                }
            }
            if (DefendOnly)
                ThisControllingWeaponGun = true;

            if (cacheDisabled == ThisControllingWeaponGun)
            {
                if (firingCache != firing)
                {
                    LockOnFireSFXHalt();
                }
                FireControlUsingWeaponGun = ThisControllingWeaponGun;
            }
            else
            {
                LockOnFireSFXHalt();
            }
            return false;
        }

        public bool TryInterceptImmedeate(out bool killed)
        {
            killed = false;
            cacheDisabled = ThisControllingWeaponGun;
            firingCache = firing;
            ThisControllingWeaponGun = false;
            firing = false;
            if (!(bool)block.tank)
                return false;
            if (SpoolOnEnemy)
            {
                spooling = true;
            }
            else
                spooling = false;
            if (OverrideEnemyAiming)
            {
                if (LockedTarget)
                {
                    if (!SeperateFromGun)
                        ThisControllingWeaponGun = true;
                    UpdateLockOnImmedeate(out killed);
                    return true;
                }
                else if (block.tank.control.FireControl)
                {
                    firing = false;
                    ThisControllingWeaponGun = false;
                }
                else
                    firing = false;
            }
            else
            {
                if (block.tank.control.FireControl)
                {
                    firing = false;
                    ThisControllingWeaponGun = false;
                }
                else if (LockedTarget)
                {
                    if (!SeperateFromGun)
                        ThisControllingWeaponGun = true;
                    UpdateLockOnImmedeate(out killed);
                    return true;
                }
                else
                    firing = false;
            }

            if (DefendOnly)
                ThisControllingWeaponGun = true;

            if (cacheDisabled == ThisControllingWeaponGun)
            {
                if (firingCache != firing)
                {
                    LockOnFireSFXHalt();
                }
                FireControlUsingWeaponGun = ThisControllingWeaponGun;
            }
            else
            {
                LockOnFireSFXHalt();
            }
            return false;
        }

        private Vector3 GetTargetHeading()
        {   // The projectile intercept coding is too expensive on terratech's gun spam levels 
            //  - will have to find a cheaper, less accurate but functional alternative

            if (ForcePulse) //Pulse hits instantly - cheapest option.
                return LockedTarget.position;

            Vector3 tankVelo = Vector3.zero;
            if ((bool)tank.rbody)
                tankVelo = tank.rbody.velocity;
            float velo = gunBase.GetVelocity();
            if (velo < 1)
                velo = 1;
            //DebugRandAddi.Log("TweakTech: RoughPredictAim - " + GravSpeedModifier);
            Vector3 targPos = LockedTarget.position;
            Vector3 VeloDiff = LockedTarget.velocity - tankVelo;
            if (!gunBase.AimWithTrajectory())
            {
                Vector3 posVec = targPos - gunBase.GetFireTransform().position;
                float roughDist = 0;
                float projSpeedDiff = VeloDiff.magnitude / velo;
                if (projSpeedDiff > LockOnStrength)
                {   // It's FAR too fast
                    if (def.GetNewTarget(this, out Rigidbody fetched, !CanInterceptFast))
                    {
                        if (LockedTarget != fetched)
                        {
                            LockedTarget = fetched;
                            targPos = LockedTarget.position;
                            VeloDiff = LockedTarget.velocity - tankVelo;
                            roughDist = posVec.magnitude / velo;
                        }
                    }
                }
                else if (projSpeedDiff > LockOnTooFastSpeed)
                {   // It's too fast
                    // Aim at it DIRECTLY, AND SPRAY & PRAY
                    return LockedTarget.position;
                }
                else
                {
                    roughDist = posVec.magnitude / velo;
                }
                return targPos + (VeloDiff * roughDist);
            }
            else
            {
                float grav = -Physics.gravity.y;
                float projSpeedDiff = VeloDiff.magnitude / velo;
                if (projSpeedDiff > LockOnStrength)
                {   // It's FAR too fast
                    if (def.GetNewTarget(this, out Rigidbody fetched, !CanInterceptFast))
                    {
                        if (LockedTarget != fetched)
                        {
                            float MaxRangeVelo = velo * 0.7071f;
                            float MaxTime = MaxRangeVelo / grav;
                            float MaxDist = MaxTime * MaxRangeVelo;

                            LockedTarget = fetched;
                            targPos = LockedTarget.position;
                            Vector3 posVec = targPos - gunBase.GetFireTransform().position;
                            VeloDiff = LockedTarget.velocity - tankVelo;

                            float veloVecMag = posVec.magnitude;
                            float distDynamic = veloVecMag / MaxDist;
                            if (distDynamic > 1)
                                distDynamic = 1;
                            float roughTime = veloVecMag / (velo * (0.7071f + ((1 - distDynamic) * 0.2929f)));
                            // this works I don't even know how
                            Vector3 VeloDiffCorrected = VeloDiff;
                            VeloDiffCorrected.y = 0;
                            // The power of cos at 45 degrees compels thee
                            VeloDiffCorrected = VeloDiffCorrected.magnitude * 0.7071f * VeloDiffCorrected.normalized;
                            VeloDiffCorrected.y = VeloDiff.y;
                            targPos = targPos + (VeloDiffCorrected * roughTime);
                            //float roughDist = VeloDiff.magnitude / velo;
                        }
                    }
                }
                else if (projSpeedDiff > LockOnTooFastSpeed)
                {   // It's too fast
                    // Aim at it DIRECTLY, AND SPRAY & PRAY
                    targPos = LockedTarget.position;
                }
                else
                {   // Calc for Target Leading
                    Vector3 posVec = targPos - gunBase.GetFireTransform().position;
                    float MaxRangeVelo = velo * 0.7071f;
                    float MaxTime = MaxRangeVelo / grav;
                    float MaxDist = MaxTime * MaxRangeVelo;

                    float veloVecMag = posVec.magnitude;
                    float distDynamic = veloVecMag / MaxDist;
                    if (distDynamic > 1)
                        distDynamic = 1;
                    float roughTime = veloVecMag / (velo * (0.7071f + ((1 - distDynamic) * 0.2929f)));
                    // this works I don't even know how
                    Vector3 VeloDiffCorrected = VeloDiff;
                    VeloDiffCorrected.y = 0;
                    // The power of cos/sin at 45 degrees compels thee
                    VeloDiffCorrected = VeloDiffCorrected.magnitude * 0.7071f * VeloDiffCorrected.normalized;
                    VeloDiffCorrected.y = VeloDiff.y;
                    targPos = targPos + (VeloDiffCorrected * roughTime);
                }

                // Aim with rough predictive trajectory
                velo *= velo;
                Vector3 direct = targPos - gunBase.GetFireTransform().position;
                Vector3 directFlat = direct;
                directFlat.y = 0;
                float distFlat = directFlat.sqrMagnitude;
                float height = direct.y + direct.y;

                float vertOffset = (velo * velo) - grav * (grav * distFlat + (height * velo));
                if (vertOffset < 0)
                    targPos.y += (velo / grav) - direct.y;
                else
                    targPos.y += ((velo - Mathf.Sqrt(vertOffset)) / grav) - direct.y;
                return targPos;
            }
        }

        private bool GetProjectile(int index)
        {
            bool noTargetsLeft = false;
            return GetProjectile(ref index, ref noTargetsLeft);
        }
        private bool GetProjectile(ref int index, ref bool noTargetsLeft)
        {
            bool getProj = false;

            if (timer <= 0)
            {
                getProj = true;
                timer = LockOnDelay;
            }
            timer--;
            if ((bool)LockedTarget)
            {
                try
                {
                    if (!LockedTarget.IsSleeping())
                    {
                        var targ = LockedTarget.GetComponent<ProjectileHealth>();
                        if (targ && targ.proj?.Shooter != null && targ.proj.Shooter.IsEnemy(tank.Team))
                        {
                            float targDist = (LockedTarget.position - block.transform.position).sqrMagnitude;
                            if (targDist > DefendRange * DefendRange)
                            {
                                ThisControllingWeaponGun = false;
                                LockedTarget = null;
                            }
                            else if (!SmartManageTargets)
                            {
                                if (getProj)
                                {
                                    if (def.GetFetchedTargets(DefenseEnergyCost, out List<Rigidbody> rbodyCatch, !CanInterceptFast))
                                    {
                                        if ((CanInterceptFast ? def.bestTargetDistAll : def.bestTargetDist) >= targDist)
                                            return true;
                                        else
                                            LockedTarget = rbodyCatch.FirstOrDefault();
                                    }
                                }
                            }
                            else
                                return true;
                        }
                    }
                }
                catch
                {
                    DebugActDef.Log("ActiveDefenses: ModulePointDefense - LockedTarget was found, but POSITION IS NULL!!!");
                    return false;
                }
            }

            if (getProj)
            {
                if (def.GetFetchedTargets(DefenseEnergyCost, out List<Rigidbody> rbodyCatch, !CanInterceptFast))
                {
                    //DebugRandAddi.Log("ActiveDefenses: ModulePointDefense - Fetched " + rbodyCatch.Count() + " targets.");
                    if (!SmartManageTargets)
                    {
                        LockedTarget = rbodyCatch.FirstOrDefault();
                    }
                    else
                    {
                        if (rbodyCatch.Count > index)
                            LockedTarget = rbodyCatch[index];
                        else
                        {
                            LockedTarget = rbodyCatch.FirstOrDefault();
                        }
                    }
                    /*
                    if ((LockedTarget.position - block.transform.position).sqrMagnitude > DefendRange * DefendRange)
                    {
                        DebugRandAddi.Log("ActiveDefenses: ModulePointDefense - LockedTarget was found, but it's out of range!?");
                    }
                    */
                    //DebugRandAddi.Log("ActiveDefenses: ModulePointDefense - LOCK");
                    return true;
                }
            }
            return false;
        }

        internal void RemoteSetTarget(Projectile toSetTo)
        {
            LockedTarget = toSetTo.rbody;
            timer = LockOnDelay;
        }

        internal void ResetTiming()
        {
            timer = LockOnDelay;
        }

        // "projectile"
        private static float RANDF()
        {
            return UnityEngine.Random.Range(-2.5f, 2.5f);
        }
        private bool FirePulseBeam(Transform trans, Vector3 endPosGlobal)
        {
            if (!def.TryTaxReserves(PulseEnergyCost))
            {
                return false;
            }
            GameObject gO;
            //var line = trans.Find("ShotLine");
            //if (!(bool)line)
            //{
            gO = Instantiate(new GameObject("ShotLine"), trans, false);
            //}
            //else
            //    gO = line.gameObject;

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                lr.material = OverrideMaterial;
                lr.positionCount = 2;
                lr.startWidth = PulseSizeStart;
                lr.endWidth = PulseSizeEnd;
                lr.useWorldSpace = true;
            }
            lr.startColor = DefenseColorStart;
            lr.endColor = DefenseColorEnd;
            Vector3 pos = trans.position;
            bool hit = false;
            Vector3 shotheading;
            if (UnityEngine.Random.Range(0, 100) < HitChance)
            {
                hit = true;
                shotheading = endPosGlobal;
            }
            else
            {
                shotheading = endPosGlobal + new Vector3(RANDF(), RANDF(), RANDF());
            }

            lr.SetPositions(new Vector3[2] { pos, shotheading });
            Destroy(gO, Mathf.Max(PulseLifetime, Time.deltaTime));
            if (!hit)
                return false;
            try
            {
                if (LockedTarget.IsNotNull())
                {
                    var targ = LockedTarget.GetComponent<ProjectileHealth>();
                    if (!(bool)targ)
                    {
                        targ = LockedTarget.gameObject.AddComponent<ProjectileHealth>();
                        targ.SetupHealth();
                    }
                    return targ.TakeDamage(PointDefenseDamage, ExplodeOnHit);
                }
            }
            catch
            {
                DebugActDef.Log("ActiveDefenses: ModulePointDefense - Target found but has no ProjectileHealth!?");
            }
            return false;
        }
        private bool FirePulseBeam(Transform trans, Rigidbody rbody)
        {
            if (!def.TryTaxReserves(PulseEnergyCost))
            {
                return false;
            }
            Vector3 endPosGlobal = rbody.position;
            GameObject gO;
            //var line = trans.Find("ShotLine");
            //if (!(bool)line)
            //{
            gO = Instantiate(new GameObject("ShotLine"), trans, false);
            //}
            //else
            //    gO = line.gameObject;

            var lr = gO.GetComponent<LineRenderer>();
            if (!(bool)lr)
            {
                lr = gO.AddComponent<LineRenderer>();
                lr.material = OverrideMaterial;
                lr.positionCount = 2;
                lr.startWidth = PulseSizeStart;
                lr.endWidth = PulseSizeEnd;
                lr.useWorldSpace = true;
            }
            lr.startColor = DefenseColorStart;
            lr.endColor = DefenseColorEnd;
            Vector3 pos = trans.position;
            bool hit = false;
            Vector3 shotheading;
            if (UnityEngine.Random.Range(0, 100) < HitChance)
            {
                hit = true;
                shotheading = endPosGlobal;
            }
            else
            {
                shotheading = endPosGlobal + new Vector3(RANDF(), RANDF(), RANDF());
            }

            lr.SetPositions(new Vector3[2] { pos, shotheading });
            Destroy(gO, Mathf.Max(PulseLifetime, Time.deltaTime));
            if (!hit)
                return false;
            try
            {
                if (rbody.IsNotNull())
                {
                    var targ = rbody.GetComponent<ProjectileHealth>();
                    if (!(bool)targ)
                    {
                        targ = rbody.gameObject.AddComponent<ProjectileHealth>();
                        targ.SetupHealth();
                    }
                    return targ.TakeDamage(PointDefenseDamage, ExplodeOnHit);
                }
            }
            catch
            {
                DebugActDef.Log("ActiveDefenses: ModulePointDefense - Target found but has no ProjectileHealth!?");
            }
            return false;
        }
        public void TaxReserves(float tax)
        {
            energyToTax = tax;
        }
    }
}
