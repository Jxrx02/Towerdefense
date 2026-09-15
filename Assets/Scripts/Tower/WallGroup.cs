using System.Collections;
using System.Collections.Generic;
using TowerDefense;
using TowerDefense.GridMovement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
public class WallGroup : MonoBehaviour
{
    private readonly List<Vector3Int> wallCells = new();

    private readonly Dictionary<Vector3Int, WallSegment> segments = new();

    private bool isBuilt;
    public bool IsBuilt => isBuilt;
    
    private bool isDestroyed;
    public bool IsDestroyed => isDestroyed;

    private float interactionHoldDuration = 4f;
    
    private Coroutine interactionCoroutine;
    private Hero interactingHero;
    
    private int maxHP;
    public int MaxHP => maxHP;

    private int buildCost;
    public int BuildCost => buildCost;

    private int repairCost;
    public int RepairCost => repairCost;

    private int hp;
    public int HP => hp;
    

    public IReadOnlyList<Vector3Int> WallCells => wallCells;

    // =========================================================
    // INITIALIZE
    // =========================================================

    public void Initialize(
        IEnumerable<Vector3Int> cells,
        Tilemap groundTilemap,
        WallSegment wallSegmentPrefab)
    {
        wallCells.Clear();
        segments.Clear();
        
        maxHP = 0;
        hp = 0;
        buildCost = 0;
        repairCost = 0;
        isDestroyed = false;

        foreach (Vector3Int cell in cells)
        {
            if (wallCells.Contains(cell))
                continue;

            wallCells.Add(cell);

            Vector3 worldPosition =
                groundTilemap.GetCellCenterWorld(cell);

            Vector3 localPosition =
                transform.InverseTransformPoint(worldPosition);

            CreateWallSegment(
                cell,
                localPosition,
                wallSegmentPrefab
            );

            WallSegment segment = segments[cell];
            buildCost += segment.towerInitPrice;
            
            maxHP += segment.HP;
        }

        repairCost = (int)(buildCost * 0.7);
        hp = maxHP;
        isBuilt = false;

        SetUnbuiltVisual();
        // Erst nachdem ALLE Segmente existieren,
        // werden die Nachbarschaften berechnet.
        RefreshVisuals();
    }
    // =========================================================
    // CREATE WALL SEGMENT
    // =========================================================

    private void CreateWallSegment(
        Vector3Int cell,
        Vector3 localPosition,
        WallSegment wallSegmentPrefab)
    {
        WallSegment segment =
            Instantiate(
                wallSegmentPrefab,
                transform
            );

        segment.name =
            $"WallSegment_{cell.x}_{cell.y}";

        segment.transform.localPosition =
            localPosition;

        segment.Initialize(
            this,
            cell
        );

        segments.Add(
            cell,
            segment
        );

        if (TowerHeroManager.instance != null)
        {
            TowerHeroManager.instance.walls.Add(
                segment.gameObject
            );
        }
    }

    // =========================================================
    // CELL QUERY
    // =========================================================

    public bool ContainsCell(Vector3Int cell)
    {
        return wallCells.Contains(cell);
    }

    // =========================================================
    // GET SEGMENT
    // =========================================================

    public WallSegment GetSegment(Vector3Int cell)
    {
        segments.TryGetValue(
            cell,
            out WallSegment segment
        );

        return segment;
    }

    // =========================================================
    // BUILD
    // =========================================================

    public void Build()
    {
        if (isBuilt)
            return;
        
        if (!GridManager.Instance.CanPlaceWallGroup(wallCells))
            return;

        if (LevelManager.instance.DoPurchase(buildCost) == false)
        {
            return;
        }

        GridManager.Instance.PlaceWallGroup(wallCells);
        hp = maxHP;

        isBuilt = true;
        isDestroyed = false;

        foreach (WallSegment segment in segments.Values)
        {
            segment.gameObject.SetActive(true);

            segment.SetBuilt();

            if (TowerHeroManager.instance != null)
            {
                TowerHeroManager.instance.RegisterTower(
                    segment.gameObject
                );
            }
        }

        RefreshVisuals();
        Actions.onWallBuilt?.Invoke(this);
    }
    public void Repair()
    {
        if (!IsDestroyed)
            return;

        if (!GridManager.Instance.CanPlaceWallGroup(wallCells))
            return;

        if (LevelManager.instance.DoPurchase(repairCost) == false)
            return;

        hp = maxHP;

        GridManager.Instance.PlaceWallGroup(wallCells);

        isBuilt = true;
        isDestroyed = false;

        foreach (WallSegment segment in segments.Values)
        {
            if (TowerHeroManager.instance != null)
            {
                TowerHeroManager.instance.RegisterTower(
                    segment.gameObject
                );
            }
        }

        SetRepairVisual();
        RefreshVisuals();

        Actions.onWallRepair?.Invoke(this);

        Debug.Log("Wall repariert");
    }

    // =========================================================
    // UNBUILT VISUAL
    // =========================================================

    public void SetUnbuiltVisual()
    {
        foreach (WallSegment segment in segments.Values)
        {
            segment.SetUnbuiltVisual();
        }
    }
    public void SetRepairVisual()
    {
        foreach (WallSegment segment in segments.Values)
        {
            segment.SetBuilt();
        }
    }

    // =========================================================
    // REFRESH VISUALS
    // =========================================================

    public void RefreshVisuals()
    {
        foreach (WallSegment segment in segments.Values)
        {
            segment.RefreshVisual();
        }
    }

    public void EnterInteractionRange(Hero hero)
    {
        if (!LevelManager.instance.isDay)
        {
            Debug.Log("Its night");
            return;
        }

        if (isBuilt && !isDestroyed)
            return;

        interactingHero = hero;

        ShowInteractionPopup();

        if (interactionCoroutine != null)
            StopCoroutine(interactionCoroutine);

        interactionCoroutine = StartCoroutine(HoldInteraction());
    }
    public void ExitInteractionRange(Hero hero)
    {
        if (hero != interactingHero)
            return;

        interactingHero = null;

        if (interactionCoroutine != null)
        {
            StopCoroutine(interactionCoroutine);
            interactionCoroutine = null;
        }

        HideInteractionPopup();
    }
    private IEnumerator HoldInteraction()
    {
        while (true)
        {
            yield return new WaitUntil(
                () => Input.GetKeyDown(KeyCode.E)
            );

            float holdTimer = 0f;

            while (Input.GetKey(KeyCode.E))
            {
                holdTimer += Time.deltaTime;

                float progress =
                    Mathf.Clamp01(
                        holdTimer / interactionHoldDuration
                    );

                InteractionPopup.instance.SetProgress(progress);

                if (holdTimer >= interactionHoldDuration)
                {
                    CompleteInteraction();
                    interactionCoroutine = null;
                    yield break;
                }

                yield return null;
            }

            InteractionPopup.instance.ResetProgress();
        }
    }

    private void CompleteInteraction()
    {
        if (!LevelManager.instance.isDay)
            return;

        if (isDestroyed)
        {
            Repair();
        }
        else if (!isBuilt)
        {
            Build();
        }

        HideInteractionPopup();
    }
    private Vector3 GetInteractionPosition()
    {
        if (segments.Count == 0)
            return transform.position;

        Vector3 center = Vector3.zero;

        foreach (WallSegment segment in this.segments.Values)
        {
            center += segment.transform.position;
        }

        return center / segments.Count;
    }
    private void ShowInteractionPopup()
    {
        InteractionPopup.instance.Show(GetInteractionPosition());
    }

    private void HideInteractionPopup()
    {
        InteractionPopup.instance.Hide();
    }


    // =========================================================
    // REMOVE SEGMENT
    // =========================================================
    public void TakeDamage(int damage)
    {
        if (!isBuilt || isDestroyed)
            return;

        if (damage <= 0)
            return;

        hp -= damage;

        if (hp <= 0)
        {
            hp = 0;
            DisableWallGroup();
        }
    }
    private void DisableWallGroup()
    {
        Actions.onWallDestroyed?.Invoke(this);
        
        isDestroyed = true;
        isBuilt = false;

        // Zellen wieder begehbar machen
        GridManager.Instance.RemoveWallGroup(wallCells);

        // Segmente aus dem aktiven Tower-/Wall-System entfernen
        if (TowerHeroManager.instance != null)
        {
            foreach (WallSegment segment in segments.Values)
            {
                TowerHeroManager.instance.UnRegisterTower(
                    segment.gameObject
                );
            }
        }
        // Wieder als unbuilt darstellen
        SetUnbuiltVisual();

        RefreshVisuals();

    }
    public void DestroyWallGroup()
    {
        GridManager.Instance.RemoveWallGroup(wallCells);

        if (TowerHeroManager.instance != null)
        {
            foreach (WallSegment segment in segments.Values)
            {
                TowerHeroManager.instance.UnRegisterTower(
                    segment.gameObject
                );
            }
        }

        wallCells.Clear();
        segments.Clear();

        isBuilt = false;

        Destroy(gameObject);
    }
    public void RemoveWallSegment(Vector3Int cell)
    {
        if (!segments.TryGetValue(
                cell,
                out WallSegment segment))
        {
            return;
        }

        segments.Remove(cell);
        wallCells.Remove(cell);

        GridManager.Instance.RemoveWallGroup(
            new[] { cell }
        );

        if (TowerHeroManager.instance != null)
        {
            TowerHeroManager.instance.UnRegisterTower(segment.gameObject);
        }

        // Wichtig:
        // Nachdem das Segment entfernt wurde,
        // müssen die verbleibenden Segmente
        // ihre Nachbarschaften neu berechnen.
        RefreshVisuals();

        if (wallCells.Count == 0)
        {
            Destroy(gameObject);
        }
    }


}