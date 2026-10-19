using System.Collections;
using UnityEngine;

namespace TowerDefense
{
    public class CannonTower : Tower
    {
        [SerializeField] private bool flipTowerWhenAttack; 
        public override void Attack((GameObject, int) _target)
        {
            if (isAttacking) return;

            target = _target;
            if (target.Item1 == null) return;

            if (IsObjectInRange(target.Item1))
            {
                isAttacking = true;
                StartCoroutine(BaseAttackCoroutine(AnimationState.Attack_Animation, Shoot));
            }
        } 
        
        protected override void UpdateLookDirection(Vector3 targetPos)
        {
            Quaternion originalCanvasRotation = GetComponentInChildren<Canvas>().transform.rotation;

            bool targetIsRight = targetPos.x > transform.position.x;
            bool faceFlipped = targetIsRight == flipTowerWhenAttack;

            transform.rotation = faceFlipped
                ? Quaternion.Euler(0, 180, 0)
                : Quaternion.identity;
            GetComponentInChildren<Canvas>().transform.rotation = originalCanvasRotation;

        }


    }
}