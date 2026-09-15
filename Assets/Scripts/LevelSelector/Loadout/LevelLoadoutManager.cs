using LevelSelector.Definitions;
using UnityEngine;

namespace LevelSelector
{
    public class LevelLoadoutManager : MonoBehaviour
    {
        public static LevelLoadoutManager Instance { get; private set; }

        public LevelLoadout CurrentLoadout { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void CreateLoadout(LevelDefinition level)
        {
            CurrentLoadout = new LevelLoadout
            {
                levelIndex = level.levelIndex,
                levelDefinition = level
            };
        }

        public void SetWeapon(WeaponDefinition weapon)
        {
            if (CurrentLoadout == null)
                return;

            CurrentLoadout.weapon = weapon;
        }

        public bool AddPerk(PerkDefinition perk)
        {
            if (CurrentLoadout == null)
                return false;

            if (CurrentLoadout.perks.Contains(perk))
                return false;

            if (CurrentLoadout.perks.Count >= 5)
                return false;

            CurrentLoadout.perks.Add(perk);

            return true;
        }

        public bool RemovePerk(PerkDefinition perk)
        {
            if (CurrentLoadout == null)
                return false;

            return CurrentLoadout.perks.Remove(perk);
        }

        public bool AddMutator(MutatorDefinition mutator)
        {
            if (CurrentLoadout == null)
                return false;

            if (CurrentLoadout.mutators.Contains(mutator))
                return false;

            if (CurrentLoadout.mutators.Count >= 3)
                return false;

            CurrentLoadout.mutators.Add(mutator);

            return true;
        }

        public bool RemoveMutator(MutatorDefinition mutator)
        {
            if (CurrentLoadout == null)
                return false;

            return CurrentLoadout.mutators.Remove(mutator);
        }

        public void ApplyLoadout()
        {
            if (CurrentLoadout == null)
                return;

            CurrentLoadout.damageMultiplier = 1f;
            CurrentLoadout.attackSpeedMultiplier = 1f;
            CurrentLoadout.enemyCountMultiplier = 1f;
            CurrentLoadout.healingEnabled = true;

            foreach (var perk in CurrentLoadout.perks)
            {
                perk.Apply(CurrentLoadout);
            }

            foreach (var mutator in CurrentLoadout.mutators)
            {
                mutator.Apply(CurrentLoadout);
            }
        }
    }
}