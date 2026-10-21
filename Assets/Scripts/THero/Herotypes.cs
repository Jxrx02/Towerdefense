using UnityEngine;

namespace TowerDefense
{
    public enum HeroWeaponType
    {
        OneHand,
        TwoHand,
        Shield,
        Bow
    }

    public enum HeroState
    {
        Free,
        Combat,       // Angriff, Aufladen oder Erholung nach dem Angriff
        Blocking,
        MoveAction,   // Dash, Ausweichen, Sprung
        Hit,
        Dead
    }

    public enum HeroAttackKind
    {
        Light,
        Heavy
    }

    public enum HeroAnim
    {
        // Ganzkörper
        Idle, CombatIdle, Block, Hit, Dead, Jump,
        Walk, Run, Sprint, WalkBack,

        // Bewegungsaktionen
        Dash, Dodge, DodgeBackwards,

        // Nahkampf (Einhänder, Zweihänder, Schild)
        Attack1, Attack2, Attack3,
        HeavyAttack1, HeavyAttack2, HeavyAttack3,
        ChargedAttack1, ChargedAttack2,
        ChargedHeavyAttack1, ChargedHeavyAttack2,

        // Bogen
        RangedAttack1, RangedAttack2, RangedAttack3,
        UpAttack,
        ChargedAttack, ChargedUpAttack
    }

    public static class HeroDirectionExtensions
    {
        public static Vector2 ToVector(this FacingDirection direction)
        {
            switch (direction)
            {
                case FacingDirection.Up: return Vector2.up;
                case FacingDirection.UpRight: return new Vector2(1f, 1f).normalized;
                case FacingDirection.Right: return Vector2.right;
                case FacingDirection.DownRight: return new Vector2(1f, -1f).normalized;
                case FacingDirection.Down: return Vector2.down;
                case FacingDirection.DownLeft: return new Vector2(-1f, -1f).normalized;
                case FacingDirection.Left: return Vector2.left;
                default: return new Vector2(-1f, 1f).normalized;   // UpLeft
            }
        }

        public static bool IsUpward(this FacingDirection direction)
        {
            return direction == FacingDirection.Up ||
                   direction == FacingDirection.UpLeft ||
                   direction == FacingDirection.UpRight;
        }

        /// <summary>
        /// Sprites der Richtung. Fehlt sie, wird die nächstliegende befüllte Richtung benutzt,
        /// damit ein nur teilweise befülltes Animationsset schon benutzbar ist.
        /// </summary>
        public static Sprite[] GetWithFallback(this DirectionalFrames frames, FacingDirection direction)
        {
            if (frames == null)
                return null;

            Sprite[] exact = frames.Get(direction);
            if (exact != null && exact.Length > 0)
                return exact;

            int index = (int)direction;

            for (int offset = 1; offset <= 4; offset++)
            {
                Sprite[] a = frames.Get((FacingDirection)((index + offset) % 8));
                if (a != null && a.Length > 0) return a;

                Sprite[] b = frames.Get((FacingDirection)((index - offset + 8) % 8));
                if (b != null && b.Length > 0) return b;
            }

            return null;
        }
    }
}