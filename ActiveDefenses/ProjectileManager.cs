using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ActiveDefenses
{
    // Keeps track of all projectiles
    public class ProjectileManager : MonoBehaviour
    {
        internal static ProjectileManager inst;
        //private static Dictionary<int, List<Projectile>> TeamProj = new Dictionary<int, List<Projectile>>();
        //private static List<Projectile> Projectiles = new List<Projectile>();
        private static ProjectileCubeArray ProjOct = new ProjectileCubeArray();
        //private static OctreeProj ProjOct = new OctreeProj();

        internal static List<Projectile> cheaters = new List<Projectile>();


        private byte timer = 0;
        private byte delay = 12;

        public static void ToggleActive(bool active)
        {
            if (inst)
            {
                if (inst.enabled != active)
                {
                    if (!active)
                        ProjOct.PurgeAll();
                    inst.enabled = active;
                }
            }
        }

        public static void Initiate()
        {
            inst = new GameObject("ProjectileManager").AddComponent<ProjectileManager>();
            DebugActDef.Log("ActiveDefenses: Created ProjectileManager.");
            inst.Invoke("LateInit", 0.1f);
        }
        public void LateInit()
        {
            Singleton.Manager<ManWorldTreadmill>.inst.OnAfterWorldOriginMoved.Subscribe(OnWorldMovePost);
            ManGameMode.inst.ModeSwitchEvent.Subscribe(OnModeSwitch);
        }
        public static void OnModeSwitch()
        {
            ProjOct.PurgeAll();
        }
        internal void LateUpdate()
        {
            timer++;
            if (timer >= delay)
            {
                ProjOct.PostFramePrep();
                timer = 0;
            }
        }

        private static List<Projectile> watchman = new List<Projectile>(50);
        /// <summary>
        /// Projectile watchman
        /// </summary>
        private void FixedUpdate()
        {
            //Debug.Log("ActiveDefenses:  There are " + cheaters.Count + " - glitchyProj");
            if (cheaters.Count > 0)
            {
                watchman.AddRange(cheaters);
                foreach (var item in watchman)
                {
                    try
                    {
                        if (WarnAllLaserDefenses(item) || !item.gameObject.activeSelf)
                            cheaters.Remove(item);
                    }
                    catch
                    {
                        DebugActDef.Log("(FixedUpdate) Overspeed Projectile with illegal state ignored");
                        cheaters.Remove(item);
                    }
                }
                watchman.Clear();
            }
        }

        public static void OnWorldMovePost(IntVector3 moved)
        {
            DebugActDef.Log("ActiveDefenses: ProjectileManager - Moved " + moved);
            ProjOct.UpdateWorldPos(moved);
        }

        // for projectiles above 400
        public static void HandleCheaty(Projectile rbody)
        {
            //Debug.Log("ActiveDefenses: CHEATY PROJECTILE DETECTED " + rbody.name + " IS BEING FLAGGED TO DEFENSES");
            if (rbody.IsNotNull())
            {
                if (TankPointDefense.HasPointDefenseActive)
                {
                    try
                    {
                        if (!WarnAllLaserDefenses(rbody))
                        {
                            cheaters.Add(rbody);
                        }
                    }
                    catch
                    {
                        DebugActDef.Log("Overspeed Projectile with illegal state ignored");
                    }
                }
            }
        }
        public static bool WarnAllLaserDefenses(Projectile proj)
        {
            Vector3 projExpectedPos = (proj.rbody.velocity * Time.fixedDeltaTime) + proj.trans.position;
            foreach (var item in TankPointDefense.pDTs)
            {
                try
                {
                    if (proj.Shooter)
                    {
                        if (proj.Shooter.IsEnemy(item.tank.Team))
                            if (item.EmergencyTryFireAtProjectile(proj, projExpectedPos))
                                return true;
                    }
                    else
                    {
                        //Debug.Log("ActiveDefenses: - PROJECTILE HAS NO SHOOTER, NO QUARTER");
                        if (item.EmergencyTryFireAtProjectile(proj, projExpectedPos))
                            return true;
                    }
                }
                catch { }
            }
            return false;
        }



        public static void Add(Projectile rbody)
        {
            if (rbody.IsNotNull())
            {
                if (TankPointDefense.HasPointDefenseActive)
                    ProjOct.Add(rbody);
            }
        }
        public static void Remove(Projectile proj)
        {
            if (proj.IsNotNull())
            {
                if (ProjOct.Remove(proj))
                {
                    //Debug.Log("ActiveDefenses: ProjectileManager - Removed " + proj.name);
                }
            }
        }
        
        private static List<Rigidbody> rbodysSend = new List<Rigidbody>(25);
        public static bool GetClosestProjectile(InterceptProjectile iProject, float Range, out Rigidbody rbody, out List<Rigidbody> rbodys)
        {
            //Debug.Log("ActiveDefenses: GetClosestProjectile - Launched!");
            rbody = null;
            rbodys = null;
            Vector3 pos = iProject.trans.position;
            if (!ProjOct.NavigateOctree(pos, Range, out List<Projectile> Projectiles))
                return false;
            rbodysSend.Clear();
            float bestVal = Range * Range;
            float rangeMain = bestVal;
            int projC = Projectiles.Count;
            for (int step = 0; step < projC; step++)
            {
                Projectile project = Projectiles.ElementAt(step);
                try
                {
                    Rigidbody rbodyC = project.rbody;
                    //Debug.Log("ActiveDefenses: GetClosestProjectile - 3");
                    if (project.Shooter.IsEnemy(iProject.team))
                    {
                        float dist = (project.trans.position - pos).sqrMagnitude;
                        if (dist < rangeMain)
                        {
                            if (dist < bestVal)
                            {
                                rbodysSend.Add(rbodyC);
                                bestVal = dist;
                                rbody = rbodyC;
                            }
                        }
                    }
                    //Debug.Log("ActiveDefenses: GetClosestProjectile - 4");
                }
                catch
                {
                    DebugActDef.Log("ActiveDefenses: GetClosestProjectile - error");
                    //ProjOct.Remove(project);
                }
            }
            //Debug.Log("ActiveDefenses: GetClosestProjectile - 5");
            if (rbody.IsNull())
                return false;
            rbodys = rbodysSend;
            var pHP = rbody.gameObject.GetComponent<ProjectileHealth>();
            if (!(bool)pHP)
            {
                pHP = rbody.gameObject.AddComponent<ProjectileHealth>();
                pHP.SetupHealth();
            }
            return true;
        }

        internal static bool GetListProjectiles(TankPointDefense iDefend, float Range, ref List<Rigidbody> rbodys)
        {
            //Debug.Log("ActiveDefenses: GetListProjectiles - Launched!");
            Vector3 pos = iDefend.transform.TransformPoint(iDefend.BiasDefendCenter);
            if (!ProjOct.NavigateOctree(pos, Range, out List<Projectile> Projectiles))
                return false;
            float bestVal = Range * Range;
            rbodys.Clear();
            int projC = Projectiles.Count;
            for (int step = 0; step < projC;)
            {
                Projectile project = Projectiles.ElementAt(step);
                if (!(bool)project?.rbody || project.rbody.IsSleeping())
                {
                    //Debug.Log("ActiveDefenses: null projectile in output");
                    step++;
                    continue;
                }
                try
                {
                    Rigidbody rbodyC = project.rbody;
                    if (project.Shooter.IsEnemy(iDefend.tank.Team))
                    {
                        float dist = (project.trans.position - pos).sqrMagnitude;
                        if (dist < bestVal)
                        {
                            rbodys.Add(rbodyC);
                        }
                    }
                }
                catch (Exception e)
                {
                    DebugActDef.Log("ActiveDefenses: (ProjectileMan)GetListProjectiles - error " + e);
                    ProjOct.Remove(project);
                }
                step++;
            }
            if (rbodys.Count == 0)
                return false;
            foreach (Rigidbody rbody in rbodys)
            {
                var pHP = rbody.gameObject.GetComponent<ProjectileHealth>();
                if (!(bool)pHP)
                {
                    pHP = rbody.gameObject.AddComponent<ProjectileHealth>();
                    pHP.SetupHealth();
                }
            }

            rbodys = rbodys.OrderBy(t => t.position.sqrMagnitude).ToList();
            return true;
        }

    }

    public class ProjectileIndex : MonoBehaviour
    {
    }
}
