using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// -----------------------------------------------------------------------
// Legacy-Animationen bleiben für Shop-Icons erhalten:
//
// Idle_Animation
// Attack_Animation
// Walk_Animation
// Dead_Animation
//
// Hero:
// Idle
// Run
// LightAttack
// HeavyAttack
// BowRangedAttack
// BowUpAttack
// -----------------------------------------------------------------------

public enum AnimationState
{
    // Legacy / Shop
    Idle_Animation,
    Attack_Animation,
    Walk_Animation,
    Dead_Animation,

    // Hero
    Idle,
    Run,
    LightAttack,
    HeavyAttack,
    BowRangedAttack,
    BowUpAttack
}

public enum FacingDirection
{
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft
}

public static class DirectionUtility
{
    private static readonly FacingDirection[] Order =
    {
        FacingDirection.Right,
        FacingDirection.UpRight,
        FacingDirection.Up,
        FacingDirection.UpLeft,
        FacingDirection.Left,
        FacingDirection.DownLeft,
        FacingDirection.Down,
        FacingDirection.DownRight
    };

    public static FacingDirection FromVector(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
            return FacingDirection.Down;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        if (angle < 0f)
            angle += 360f;

        int index = Mathf.RoundToInt(angle / 45f) % 8;

        return Order[index];
    }
}

[Serializable]
public class DirectionalFrames
{
    public Sprite[] up;
    public Sprite[] upRight;
    public Sprite[] right;
    public Sprite[] downRight;
    public Sprite[] down;
    public Sprite[] downLeft;
    public Sprite[] left;
    public Sprite[] upLeft;

    public Sprite[] Get(FacingDirection dir)
    {
        switch (dir)
        {
            case FacingDirection.Up:
                return up;

            case FacingDirection.UpRight:
                return upRight;

            case FacingDirection.Right:
                return right;

            case FacingDirection.DownRight:
                return downRight;

            case FacingDirection.Down:
                return down;

            case FacingDirection.DownLeft:
                return downLeft;

            case FacingDirection.Left:
                return left;

            case FacingDirection.UpLeft:
                return upLeft;

            default:
                return down;
        }
    }
}

[Serializable]
public class StateAnimationEntry
{
    public AnimationState state;

    public DirectionalFrames frames;

    [Tooltip(
        "An für loopende Animationen wie Idle und Run. " +
        "Aus für One-Shot-Animationen wie Angriffe."
    )]
    public bool loop = true;

    [Header("Charge")]

    [Tooltip(
        "Für BowRangedAttack und BowUpAttack aktivieren."
    )]
    public bool isChargeable = false;

    [Tooltip(
        "Frame, ab dem die Charging-Schleife beginnt."
    )]
    public int chargeLoopStartFrame = 0;

    [Tooltip(
        "Letzter Frame der Charging-Schleife."
    )]
    public int chargeLoopEndFrame = 0;
}

public class SpriteAnim : MonoBehaviour
{
    [SerializeField] private float speed = 1f;

    [SerializeField]
    [Tooltip("Only For Shop Icons")]
    private bool loopThrough;

    [SerializeField]
    public float mTimePerFrame = 0.125f;

    private float directionalSpeedMultiplier = 1f;

    private SpriteRenderer sr;
    private Image img;

    [SerializeField]
    public bool loop = true;

    // -------------------------------------------------------------------
    // Legacy Sprites
    // -------------------------------------------------------------------

    [Header("Legacy Sprites")]

    public Sprite[] idle_sprites;
    public Sprite[] attack_sprites;
    public Sprite[] evolve_sprites;
    public Sprite[] dead_sprites;
    public Sprite[] walk_sprites;

    // -------------------------------------------------------------------
    // Hero
    // -------------------------------------------------------------------

    [Header("Hero: 8-Richtungs-Animationen")]

    [Tooltip(
        "Idle, Run und die verschiedenen Angriffe."
    )]
    public List<StateAnimationEntry> directionalAnimations =
        new List<StateAnimationEntry>();

    private Dictionary<AnimationState, StateAnimationEntry> _lookup;

    private FacingDirection currentDirection =
        FacingDirection.Down;

    private bool isCharging;

    private float mElapsedTime = 0f;
    private int mCurrentFrame = 0;

    [HideInInspector]
    public bool destroyOnEndDeadAnimation;

    [field: SerializeField]
    public AnimationState animState { get; set; }

    // Legacy Events
    [HideInInspector]
    public Action OnIdleAnimationComplete,
        OnAttackAnimationComplete,
        OnWalkAnimationComplete,
        OnDeadAnimationComplete;

    // Hero Event
    public Action<AnimationState> OnDirectionalAnimationComplete;

    // -------------------------------------------------------------------
    // Unity
    // -------------------------------------------------------------------

    private void Awake()
    {
        BuildLookup();
    }

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        img = GetComponent<Image>();

        animState = AnimationState.Idle_Animation;

        Play();
    }

    private void Update()
    {
        float currentSpeedMultiplier = directionalSpeedMultiplier;

        if (animState == AnimationState.LightAttack ||
            animState == AnimationState.HeavyAttack ||
            animState == AnimationState.BowRangedAttack ||
            animState == AnimationState.BowUpAttack)
        {
            currentSpeedMultiplier *= attackSpeedMultiplier;
        }

        mElapsedTime +=
            speed *
            currentSpeedMultiplier *
            Time.deltaTime;

        if (mElapsedTime < mTimePerFrame)
            return;

        mElapsedTime = 0f;
        mCurrentFrame++;

        HandleChargeLoop();

        SetSprite();
    }
    private float attackSpeedMultiplier = 1f;

    public void SetAttackSpeed(float multiplier)
    {
        attackSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }
    // -------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------

    private void BuildLookup()
    {
        _lookup =
            new Dictionary<AnimationState, StateAnimationEntry>();

        foreach (StateAnimationEntry entry in directionalAnimations)
        {
            if (entry == null)
                continue;

            if (_lookup.ContainsKey(entry.state))
                continue;

            _lookup.Add(entry.state, entry);
        }
    }

    private void Play()
    {
        enabled = true;
    }

    // -------------------------------------------------------------------
    // Speed
    // -------------------------------------------------------------------

    public void SetSpeedMultiplier(float multiplier)
    {
        directionalSpeedMultiplier =
            Mathf.Max(0.05f, multiplier);
    }

    // -------------------------------------------------------------------
    // Direction
    // -------------------------------------------------------------------

    public void SetDirection(FacingDirection dir)
    {
        currentDirection = dir;
    }

    public FacingDirection GetDirection()
    {
        return currentDirection;
    }

    // -------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------

    public void SetState(
        AnimationState newState,
        bool forceRestart = false)
    {
        if (animState == newState && !forceRestart)
            return;

        animState = newState;

        mCurrentFrame = 0;
        mElapsedTime = 0f;

        isCharging = false;

        directionalSpeedMultiplier = 1f;

        enabled = true;
    }

    // -------------------------------------------------------------------
    // Bow Charge
    // -------------------------------------------------------------------

    public void SetCharging(bool charging)
    {
        isCharging = charging;
    }

    private void HandleChargeLoop()
    {
        if (!isCharging)
            return;

        if (_lookup == null)
            return;

        if (!_lookup.TryGetValue(
                animState,
                out StateAnimationEntry entry))
            return;

        if (!entry.isChargeable)
            return;

        if (mCurrentFrame > entry.chargeLoopEndFrame)
        {
            mCurrentFrame =
                entry.chargeLoopStartFrame;
        }
    }

    // -------------------------------------------------------------------
    // Legacy Dead
    // -------------------------------------------------------------------

    public void TriggerDeadAnimation(
        bool destroyOnEndDeadAnimation)
    {
        animState = AnimationState.Dead_Animation;

        mCurrentFrame = 0;
        mElapsedTime = 0f;

        this.destroyOnEndDeadAnimation =
            destroyOnEndDeadAnimation;

        enabled = true;
    }

    // -------------------------------------------------------------------
    // Sprite Selection
    // -------------------------------------------------------------------

    private void SetSprite()
    {
        try
        {
            switch (animState)
            {
                case AnimationState.Idle_Animation:

                    PlayLegacyAnimation(
                        idle_sprites,
                        OnIdleAnimationComplete
                    );

                    break;

                case AnimationState.Attack_Animation:

                    PlayLegacyAnimation(
                        attack_sprites,
                        OnAttackAnimationComplete,
                        true
                    );

                    break;

                case AnimationState.Walk_Animation:

                    PlayLegacyAnimation(
                        walk_sprites,
                        OnWalkAnimationComplete
                    );

                    break;

                case AnimationState.Dead_Animation:

                    PlayDeadAnimation();

                    break;

                default:

                    PlayDirectionalFrame();

                    break;
            }
        }
        catch
        {
            // Verhindert, dass ein fehlendes Sprite
            // die komplette Animation stoppt.
        }
    }

    // -------------------------------------------------------------------
    // Legacy Animation
    // -------------------------------------------------------------------

    private void PlayLegacyAnimation(
        Sprite[] sprites,
        Action onComplete,
        bool returnToIdle = false)
    {
        if (sprites == null || sprites.Length == 0)
            return;

        if (mCurrentFrame >= 0 &&
            mCurrentFrame < sprites.Length)
        {
            SetSpriteRendererOrImage(
                sprites[mCurrentFrame]
            );
        }

        if (mCurrentFrame >= sprites.Length)
        {
            if (loop)
            {
                mCurrentFrame = 0;
            }
            else
            {
                enabled = false;
            }

            onComplete?.Invoke();

            if (returnToIdle)
            {
                animState =
                    AnimationState.Idle_Animation;

                mCurrentFrame = 0;
            }

            AdvanceToNextAnimationIfLooping();
        }
    }

    private void PlayDeadAnimation()
    {
        if (dead_sprites == null ||
            dead_sprites.Length == 0)
            return;

        if (mCurrentFrame >= 0 &&
            mCurrentFrame < dead_sprites.Length)
        {
            SetSpriteRendererOrImage(
                dead_sprites[mCurrentFrame]
            );
        }

        if (mCurrentFrame >= dead_sprites.Length)
        {
            OnDeadAnimationComplete?.Invoke();

            if (destroyOnEndDeadAnimation)
                Destroy(gameObject);

            AdvanceToNextAnimationIfLooping();
        }
    }

    // -------------------------------------------------------------------
    // Hero Directional Animation
    // -------------------------------------------------------------------

    private void PlayDirectionalFrame()
    {
        if (_lookup == null)
            BuildLookup();

        if (!_lookup.TryGetValue(
                animState,
                out StateAnimationEntry entry))
            return;

        if (entry.frames == null)
            return;

        Sprite[] frames =
            entry.frames.Get(currentDirection);

        if (frames == null ||
            frames.Length == 0)
            return;

        if (mCurrentFrame >= 0 &&
            mCurrentFrame < frames.Length)
        {
            SetSpriteRendererOrImage(
                frames[mCurrentFrame]
            );
        }

        if (mCurrentFrame >= frames.Length)
        {
            if (entry.loop)
            {
                mCurrentFrame = 0;
            }
            else
            {
                mCurrentFrame =
                    frames.Length - 1;

                enabled = false;

                OnDirectionalAnimationComplete?.Invoke(
                    entry.state
                );
            }
        }
    }

    // -------------------------------------------------------------------
    // Renderer
    // -------------------------------------------------------------------

    private void SetSpriteRendererOrImage(
        Sprite sprite)
    {
        if (sr != null)
        {
            sr.sprite = sprite;
        }
        else if (img != null)
        {
            img.sprite = sprite;
        }
    }

    // -------------------------------------------------------------------
    // Shop Animation
    // -------------------------------------------------------------------

    private AnimationState[] loopSequence =
    {
        AnimationState.Attack_Animation,
        AnimationState.Walk_Animation
    };

    private int currentLoopIndex = 0;

    private void AdvanceToNextAnimationIfLooping()
    {
        if (!loopThrough)
            return;

        currentLoopIndex =
            (currentLoopIndex + 1) %
            loopSequence.Length;

        animState =
            loopSequence[currentLoopIndex];

        mCurrentFrame = 0;
    }
}