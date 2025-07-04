﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

namespace ActiveDefenses
{
    // Gives projectiles health based on stats
    public class ProjectileHealth : MonoBehaviour
    {
        //public bool Fast = false;
        private float MaxHealth = 0;
        private float Health = 10;
        private bool exploded = false;
        internal Projectile proj;

        const float FastProjectileSpeed = 135;
        const float CheatingProjectileSpeed = 400;
        // Any projectiles that go above 400 bypass the game's physics engine limit and don't
        //  give the Point Defense System any chances to shoot them down.  We punish this by
        //  allowing the Point Defense System to shoot it before it leaves the barrel.

        public static bool IsFast(float speed)
        {
            return speed > FastProjectileSpeed;
        }
        public static bool IsCheaty(float speed)
        {
            return speed > CheatingProjectileSpeed;
        }
        public bool WillDestroy(float DamageDealt)
        {
            return DamageDealt > Health;
        }
        public void Reset()
        {
            exploded = false;
            Health = MaxHealth;
        }

        FieldInfo deals = typeof(WeaponRound).GetField("m_Damage", BindingFlags.NonPublic | BindingFlags.Instance);
        public void SetupHealth()
        {
            try
            {
                if (MaxHealth == 0)
                {
                    proj = GetComponent<Projectile>();
                    float solidHealth = (int)deals.GetValue(GetComponent<WeaponRound>());
                    if (solidHealth < 10)
                        solidHealth = 10;
                    float dmgMax = solidHealth + GetExplodeVal();
                    if (dmgMax < 0.1f)
                        dmgMax = 0.1f;
                    float health = solidHealth * (solidHealth / dmgMax) * KickStart.ProjectileHealthMultiplier;
                    if (health > 100)
                        MaxHealth = health;
                    else
                        MaxHealth = 100;
                }
                exploded = false;
                Health = MaxHealth;

                //Debug.Log("ActiveDefenses: ProjectileHealth - Init on " + gameObject.name + ", health " + Health);
            }
            catch (Exception e)
            {
                DebugActDef.Log("ActiveDefenses: ProjectileHealth - Error!  Could not find needed data!!! " + e);
            }// It has no WeaponRound!
        }

        static FieldInfo death = typeof(Projectile).GetField("m_ExplodeAfterLifetime", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo death2 = typeof(Projectile).GetField("m_LifeTime", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo explode = typeof(Projectile).GetField("m_Explosion", BindingFlags.NonPublic | BindingFlags.Instance);
        /// <summary>
        /// Returns true when destroyed
        /// </summary>
        /// <param name="damage"></param>
        /// <param name="doExplode"></param>
        /// <returns></returns>
        public bool TakeDamage(float damage, bool doExplode)
        {
            if (!(bool)proj)
            {
                SetupHealth();
                if (!(bool)proj)
                    throw new NullReferenceException("ActiveDefenses: error - TakeDamage() was called but no such Projectile instance was present");
            }
            float health = Health - damage;
            if (health <= 0)
            {
                //death.SetValue(proj, true);
                //death2.SetValue(proj, 0);
                if (!exploded && doExplode && KickStart.InterceptedExplode)
                {
                    Transform explodo = (Transform)explode.GetValue(proj);
                    if ((bool)explodo)
                    {
                        var boom = explodo.GetComponent<Explosion>();
                        if ((bool)boom)
                        {
                            ForceExplode(explodo, false);
                        }
                    }
                    exploded = true;
                }
                var i_explosion = GetComponent<IExplodeable>();
                if (i_explosion != null)
                {
                    i_explosion.Explode();
                }

                proj.Recycle(worldPosStays: true);
                return true;
                //Debug.Log("ActiveDefenses: Projectile destroyed!");
            }
            else
            {
                Health = health;
                //Debug.Log("ActiveDefenses: Projectile hit - HP: " + Health);
            }
            return false;
        }
        public void ForceExplode(Transform explodo, bool doDamage)
        {
            var boom = explodo.GetComponent<Explosion>();
            if ((bool)boom)
            {
                Explosion boom2 = explodo.UnpooledSpawnWithLocalTransform(null, proj.trans.position, Quaternion.identity).GetComponent<Explosion>();
                if (boom2 != null)
                {
                    boom2.gameObject.SetActive(true);
                    boom2.DoDamage = doDamage;
                    //boom2.SetDamageSource(Shooter);
                    //boom2.SetDirectHitTarget(directHitTarget);
                }
            }
        }

        public int GetExplodeVal()
        {
            int val = 0;
            Transform explodo = (Transform)explode.GetValue(proj);
            if ((bool)explodo)
            {
                var boom = explodo.GetComponent<Explosion>();
                if ((bool)boom)
                {
                    val = (int)boom.m_MaxDamageStrength;
                }
            }
            return val;
        }
    }
}
