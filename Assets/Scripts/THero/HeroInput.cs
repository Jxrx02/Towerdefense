using System;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>Eingaben eines Frames.</summary>
    public struct HeroInputState
    {
        public Vector2 move;
        public bool sprint;
        public bool walk;

        public bool lightPressed, lightHeld;
        public bool heavyPressed, heavyHeld;
        public bool blockHeld;

        public bool dashPressed;
        public bool dodgePressed;
        public bool jumpPressed;
    }

    /// <summary>Tastenbelegung des Helden. Leertaste (Interaktion) und E (Upgrade) sind bereits belegt.</summary>
    [Serializable]
    public class HeroInputSettings
    {
        public KeyCode lightAttack = KeyCode.J;
        public KeyCode heavyAttack = KeyCode.K;
        public KeyCode block = KeyCode.L;
        public KeyCode sprint = KeyCode.LeftShift;
        [Tooltip("Gedrückt halten für langsames Gehen")]
        public KeyCode walk = KeyCode.LeftAlt;
        public KeyCode dash = KeyCode.Q;
        public KeyCode dodge = KeyCode.LeftControl;
        public KeyCode jump = KeyCode.F;

        public HeroInputState Read()
        {
            Vector2 move = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

            // Diagonale Bewegung ist nicht schneller
            move = move.normalized;

            return new HeroInputState
            {
                move = move,
                sprint = Input.GetKey(sprint),
                walk = Input.GetKey(walk),

                lightPressed = Input.GetKeyDown(lightAttack),
                lightHeld = Input.GetKey(lightAttack),
                heavyPressed = Input.GetKeyDown(heavyAttack),
                heavyHeld = Input.GetKey(heavyAttack),
                blockHeld = Input.GetKey(block),

                dashPressed = Input.GetKeyDown(dash),
                dodgePressed = Input.GetKeyDown(dodge),
                jumpPressed = Input.GetKeyDown(jump)
            };
        }
    }
}