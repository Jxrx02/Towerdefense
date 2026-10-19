using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using TowerDefense.GridMovement;

namespace TowerDefense
{
    public class Wall : Tower
    {
        protected bool isBuilt = false;
        public int HP => statHealthPoints;
        
        protected override void Awake()
        {
            base.Awake();
            
            isBuilt = false;
            
            
            if (!TryGetComponent(out vitals))
                vitals = gameObject.AddComponent<Vitals>();

            if (vitals != null)
            {
                vitals.SetMaxHealth(statHealthPoints, true);
                vitals.SetRegen(statHealthRegenPerSecond);
                vitals.BaseArmor += baseArmor;
                vitals.BaseMagicResist += baseMagicResist;

                vitals.Damaged += _ => SyncHealth();
                vitals.Healed += _ => SyncHealth();
                vitals.Died += _ => SyncHealth();
            }

        }

        public override void Attack((GameObject, int) _target)
        {
        }
        
    }
}