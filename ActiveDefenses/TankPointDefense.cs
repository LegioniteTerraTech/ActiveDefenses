using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using TerraTechETCUtil;

namespace ActiveDefenses
{
    internal class TankPointDefense : MonoBehaviour
    {
        public static bool HasPointDefenseActive => hasPointDefenseActive;

        private static bool hasPointDefenseActive = false;
        internal static HashSet<TankPointDefense> pDTs = new HashSet<TankPointDefense>();
        private static bool needsReset = false;


        internal Tank tank;
        private HashSet<ModulePointDefense> dTMs = new HashSet<ModulePointDefense>();
        private HashSet<ModulePointDefense> dTs = new HashSet<ModulePointDefense>();

        /// <summary>
        /// Frame-by-frame basis
        /// </summary>
        private bool fetchedTargets = false;
        /// <summary>
        /// Frame-by-frame basis
        /// </summary>
        private bool enemyInRange = false;
        private bool needsBiasCheck = false;
        private List<Rigidbody> fetchedMissiles = new List<Rigidbody>();
        private List<Rigidbody> fetchedAll = new List<Rigidbody>();
        internal float bestTargetDist = 0;
        internal float bestTargetDistAll = 0;

        internal Vector3 BiasDefendCenter = Vector3.zero;
        internal float BiasDefendRange = 0;
        internal float DefenseRadius => BiasDefendRange / (1 + (TechSpeed() / 33));

        private TechEnergy reg;
        private float lastEnergy = 0;
        private float energyTax = 0;

        public static void HandleAddition(Tank tank, ModulePointDefense dTurret)
        {
            if (tank.IsNull())
            {
                DebugActDef.Log("ActiveDefenses: TankPointDefense(HandleAddition) - TANK IS NULL");
                return;
            }
            var def = tank.GetComponent<TankPointDefense>();
            if (!(bool)def)
            {
                def = tank.gameObject.AddComponent<TankPointDefense>();
                def.tank = tank;
                def.reg = tank.EnergyRegulator;
                pDTs.Add(def);
                hasPointDefenseActive = true;
                ProjectileManager.ToggleActive(true);
            }

            if (dTurret.CanInterceptFast)
            {
                if (!def.dTs.Contains(dTurret))
                    def.dTs.Add(dTurret);
                else
                    DebugActDef.Log("ActiveDefenses: TankPointDefense - ModulePointDefense of " + dTurret.name + " was already added to " + tank.name + " but an add request was given?!?");
            }
            else
            {
                if (!def.dTMs.Contains(dTurret))
                    def.dTMs.Add(dTurret);
                else
                    DebugActDef.Log("ActiveDefenses: TankPointDefense - ModulePointDefense of " + dTurret.name + " was already added to " + tank.name + " but an add request was given?!?");
            }
            dTurret.def = def;
            def.needsBiasCheck = true;
            needsReset = true;
        }
        public static void HandleRemoval(Tank tank, ModulePointDefense dTurret)
        {
            if (tank.IsNull())
            {
                DebugActDef.Log("ActiveDefenses: TankPointDefense(HandleRemoval) - TANK IS NULL");
                return;
            }

            var def = tank.GetComponent<TankPointDefense>();
            if (!(bool)def)
            {
                DebugActDef.Log("ActiveDefenses: TankPointDefense - Got request to remove for tech " + tank.name + " but there's no TankPointDefense assigned?!?");
                return;
            }
            if (dTurret.CanInterceptFast)
            {
                if (!def.dTs.Remove(dTurret))
                    DebugActDef.Log("ActiveDefenses: TankPointDefense - ModulePointDefense of " + dTurret.name + " requested removal from " + tank.name + " but no such ModulePointDefense is assigned.");
            }
            else
            {
                if (!def.dTMs.Remove(dTurret))
                    DebugActDef.Log("ActiveDefenses: TankPointDefense - ModulePointDefense of " + dTurret.name + " requested removal from " + tank.name + " but no such ModulePointDefense is assigned.");
            }
            dTurret.def = null;
            def.needsBiasCheck = true;

            if (def.dTs.Count() == 0 && def.dTMs.Count() == 0)
            {
                pDTs.Remove(def);
                if (pDTs.Count == 0)
                {
                    hasPointDefenseActive = false;
                    ProjectileManager.ToggleActive(false);
                }
                Destroy(def);
            }
        }

        public float TechSpeed()
        {
            var rbody = GetComponent<Rigidbody>();
            if ((bool)rbody)
                return rbody.velocity.magnitude;
            return 0;
        }

        /// <summary>
        /// Returns false if it can't afford the enemy tax
        /// </summary>
        /// <param name="energyCost"></param>
        /// <returns></returns>
        public bool GetTargetsRequest(float energyCost)
        {
            if (tank.beam.IsActive)
                return false;
            if (!fetchedTargets)
            {
                if (!ProjectileManager.GetListProjectiles(this, DefenseRadius, ref fetchedAll))
                    return false;
                var reg = this.reg.Energy(TechEnergy.EnergyType.Electric);
                lastEnergy = reg.storageTotal - reg.spareCapacity;
                fetchedMissiles.Clear();
                foreach (var cand in fetchedAll)
                {
                    if (cand.GetComponent<MissileProjectile>())
                        fetchedMissiles.Add(cand);
                }

                Vector3 pos = transform.TransformPoint(BiasDefendCenter);
                if (fetchedMissiles.Count > 0)
                    bestTargetDist = (fetchedMissiles.FirstOrDefault().position - pos).sqrMagnitude;
                if (fetchedAll.Count > 0)
                    bestTargetDistAll = (fetchedAll.FirstOrDefault().position - pos).sqrMagnitude;
                //if (fetchedAll.Count > 0)
                //    Debug.Log("ActiveDefenses: TankPointDefense(GetTargetsRequest) - Target " + fetchedAll.FirstOrDefault().name + " | " + fetchedAll.FirstOrDefault().position + " | " + fetchedAll.FirstOrDefault().velocity);
                fetchedTargets = true;
            }
            if (!TryTaxReserves(energyCost))
                return false;
            return true;
        }
        public void RefreshTargetCanidatesFromCache()
        {
            Vector3 pos = transform.TransformPoint(BiasDefendCenter);
            if (fetchedMissiles.Count > 0)
                bestTargetDist = (fetchedMissiles.FirstOrDefault().position - pos).sqrMagnitude;
            if (fetchedAll.Count > 0)
                bestTargetDistAll = (fetchedAll.FirstOrDefault().position - pos).sqrMagnitude;
        }
        private void HandleDefenses()
        {
            int index = 0;
            bool noTargetsLeft = false;
            bool DumbDefWasteTurn = false;
            bool targDestroyed = false;
            // For missile interceptors
            foreach (ModulePointDefense def in dTMs)
            {
                if (def.SmartManageTargets)
                {
                    if (!def.TryInterceptProjectile(enemyInRange, ref index, ref noTargetsLeft, out targDestroyed))
                    {
                        //def.DisabledWeapon = false;
                    }
                }
                else if (!DumbDefWasteTurn)
                {
                    if (!def.TryInterceptProjectile(enemyInRange, ref index, ref noTargetsLeft, out targDestroyed))
                    {
                        //def.DisabledWeapon = false;
                    }
                }
                if (targDestroyed)
                {
                    DumbDefWasteTurn |= true;
                    if (index > fetchedMissiles.Count)
                        noTargetsLeft = true;
                    if (fetchedMissiles.Count > 0)
                        index = (index + 1) % fetchedMissiles.Count;
                }
            }
            DumbDefWasteTurn = false;
            // for general interceptors
            foreach (ModulePointDefense def in dTs)
            {
                if (def.SmartManageTargets)
                {
                    if (!def.TryInterceptProjectile(enemyInRange, ref index, ref noTargetsLeft, out targDestroyed))
                    {
                        //def.DisabledWeapon = false;
                    }
                }
                else if (!DumbDefWasteTurn)
                {
                    if (!def.TryInterceptProjectile(enemyInRange, ref index, ref noTargetsLeft, out targDestroyed))
                    {
                        //def.DisabledWeapon = false;
                    }
                }
                if (targDestroyed)
                {
                    DumbDefWasteTurn |= true;
                    if (index > fetchedAll.Count)
                        noTargetsLeft = true;
                    if (fetchedAll.Count > 0)
                        index = (index + 1) % fetchedAll.Count;
                }
            }
            if (dTs.Any())
                dTs.FirstOrDefault().TaxReserves(energyTax);
            else
                dTMs.FirstOrDefault().TaxReserves(energyTax);
            energyTax = 0;
        }
        private static void ResyncDefenses()
        {
            if (!needsReset)
                return;
            foreach (TankPointDefense tech in pDTs)
            {
                foreach (ModulePointDefense def in tech.dTMs)
                {
                    def.ResetTiming();
                }
                foreach (ModulePointDefense def in tech.dTs)
                {
                    def.ResetTiming();
                }
            }
            needsReset = false;
        }

        /// <summary>
        /// If the projectile speed overwhelms the game's maximum calculation rate, we try intercept 
        ///   it before it gets the chance to clip.
        /// </summary>
        /// <param name="proj"></param>
        internal bool EmergencyTryFireAtProjectile(Projectile proj, Vector3 projExpectedPosScene)
        {
            var reg = this.reg.Energy(TechEnergy.EnergyType.Electric);
            lastEnergy = reg.storageTotal - reg.spareCapacity;

            float distSqr = (projExpectedPosScene - tank.boundsCentreWorld).sqrMagnitude;
            // multiply range by 1.4 because they are glitchy
            float defRad = DefenseRadius * 1.4f;
            if (distSqr <= defRad * defRad)
            {
                fetchedAll.Add(proj.rbody);
                foreach (ModulePointDefense def in dTs)
                {
                    if (!def.AimingDefense)
                    {
                        //Debug.Log("ActiveDefenses: " + def.name + " - trying to destroy cheaty projectile");
                        def.RemoteSetTarget(proj);
                        def.TryInterceptImmedeate(out bool killed);
                        if (killed)
                            return true;
                    }
                }
            }
            return false;
        }


        private void Update()
        {
            enemyInRange = (bool)tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            ResyncDefenses();
            RecalcBiasDefend();
            HandleDefenses();
            fetchedTargets = false;
        }


        private void RecalcBiasDefend()
        {
            if (!needsBiasCheck)
                return;
            BiasDefendCenter = Vector3.zero;
            BiasDefendRange = 0;
            foreach (ModulePointDefense dT in dTMs)
                BiasDefendCenter += tank.transform.InverseTransformPoint(dT.block.centreOfMassWorld);
            foreach (ModulePointDefense dT in dTs)
                BiasDefendCenter += tank.transform.InverseTransformPoint(dT.block.centreOfMassWorld);
            BiasDefendCenter /= dTs.Count + dTMs.Count;
            foreach (ModulePointDefense dT in dTMs)
            {
                float maxRangeC = dT.transform.localPosition.magnitude + dT.DefendRange;
                if (maxRangeC > BiasDefendRange)
                    BiasDefendRange = maxRangeC;
            }
            foreach (ModulePointDefense dT in dTs)
            {
                float maxRangeC = dT.transform.localPosition.magnitude + dT.DefendRange;
                if (maxRangeC > BiasDefendRange)
                    BiasDefendRange = maxRangeC;
            }
            DebugActDef.Info("ActiveDefenses: TankPointDefense - BiasDefendCenter of " + tank.name + " changed to " + BiasDefendCenter);
            needsBiasCheck = false;
        }
        public bool GetFetchedTargets(float energyCost, out List<Rigidbody> fetchedProjPool, bool missileOnly = true)
        {
            fetchedProjPool = null;
            if (!GetTargetsRequest(energyCost))
                return false;
            if (missileOnly)
                fetchedProjPool = fetchedMissiles;
            else
                fetchedProjPool = fetchedAll;
            return fetchedProjPool != null && fetchedProjPool.Count() != 0;
        }
        public bool GetFetchedTargetsNoScan(out List<Rigidbody> fetchedProj, bool missileOnly = true)
        {
            if (missileOnly)
                fetchedProj = fetchedMissiles;
            else
                fetchedProj = fetchedAll;
            return fetchedProj != null && fetchedProj.Count() != 0;
        }
        public bool GetNewTarget(ModulePointDefense inst, out Rigidbody fetched, bool missileOnly = true)
        {
            fetched = null;
            List<Rigidbody> fetchedProj;
            if (missileOnly)
                fetchedProj = fetchedMissiles;
            else
                fetchedProj = fetchedAll;
            if (fetchedProj != null)
            {
                int index = fetchedProj.IndexOf(inst.Target) + 1;
                if (index != 0)
                {
                    fetched = fetchedProj[index];
                    return true;
                }
            }
            return false;
        }
        public bool TryTaxReserves(float energyCost)
        {
            if (energyCost > 0)
            {
                if (energyCost <= lastEnergy)
                {
                    energyTax += energyCost;
                    lastEnergy -= energyCost;
                    return true;
                }
                return false;
            }
            return true;
        }
    }
}
