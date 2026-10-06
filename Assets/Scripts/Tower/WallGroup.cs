using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ScriptableObjects;
using Tower;
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
    
    public bool isDestroyed;
    public bool IsDestroyed => isDestroyed;

    private float interactionHoldDuration = 4f;
    
    private Coroutine interactionCoroutine;
    
    private string towername, towerdesc;
    private int maxHP, wallinitprice;
    public int MaxHP => maxHP;

    private int hp;
    public int HP => hp;
    
    [Header("Wall Upgrade")]
    [SerializeField]
    private UpgradePath wallUpgradePath;

    private WallGate wallGate;
    public int wallLevel = 0;

    public int WallLevel => wallLevel;
    
    public UpgradePath WallUpgradePath
    {
        get => wallUpgradePath;
        set => wallUpgradePath = value;
    }

    // =========================================================
    // INITIALIZE
    // =========================================================

    public void Initialize(
        IEnumerable<Vector3Int> cells,
        Tilemap groundTilemap,
        WallSegment wallSegmentPrefab,
        WallGate wallGatePrefab)
    {
        wallCells.Clear();
        segments.Clear();
        
        maxHP = 0;
        hp = 0;
        isDestroyed = false;

        foreach (Vector3Int cell in cells)
        {
            if (wallCells.Contains(cell))
                continue;

            wallCells.Add(cell);
        }
        if (wallCells.Count == 0)
            return;

        Vector3Int middleCell = wallCells[wallCells.Count / 2];

        bool isHorizontal =
            wallCells.Max(c => c.x) - wallCells.Min(c => c.x) >=
            wallCells.Max(c => c.y) - wallCells.Min(c => c.y);

        foreach (Vector3Int cell in wallCells)
        {
            Vector3 worldPosition =
                groundTilemap.GetCellCenterWorld(cell);

            Vector3 localPosition =
                transform.InverseTransformPoint(worldPosition);

            bool isGatePosition = cell == middleCell;

            CreateWallSegment(
                cell,
                localPosition,
                wallSegmentPrefab,
                wallGatePrefab,
                isGatePosition,
                isHorizontal
            );

            WallSegment segment = segments[cell];

            towername = segment.towerName;
            towerdesc = segment.towerDesc;
            wallinitprice = segment.towerInitPrice;

            maxHP += segment.HP;
        }

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
        WallSegment wallSegmentPrefab,
        WallGate wallGatePrefab,
        bool isGatePosition,
        bool isHorizontal)
    {
        WallSegment segment;

        if (isGatePosition)
        {
            wallGate = Instantiate(
                wallGatePrefab,
                transform
            );

            wallGate.InitializeGate(isHorizontal);
            segment = wallGate;

            wallGate.name = $"WallGate_{cell.x}_{cell.y}";
        }
        else
        {
            segment = Instantiate(
                wallSegmentPrefab,
                transform
            );

            segment.name = $"WallSegment_{cell.x}_{cell.y}";
        }

        segment.transform.localPosition = localPosition;

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
        if (isBuilt || isDestroyed)
            return;

        if (!GridManager.Instance.CanPlaceWallGroup(wallCells))
            return;

        if (!LevelManager.instance.DoPurchase(GetBuildCost()))
            return;

        GridManager.Instance.PlaceWallGroup(wallCells);

        isBuilt = true;
        isDestroyed = false;

        wallLevel = 0;

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

        // Level 0 auf alle Segmente anwenden
        TowerUpgradeLevel initialLevel = GetCurrentUpgrade();

        if (initialLevel != null)
        {
            ApplyUpgrade(initialLevel);
        }

        hp = maxHP;

        RefreshVisuals();

        Actions.onWallBuilt?.Invoke(this);
    }
    public void Repair()
    {
        if (!isDestroyed)
            return;

        if (!GridManager.Instance.CanPlaceWallGroup(wallCells))
            return;

        int repairCost = GetCurrentRepairCost();

        if (!LevelManager.instance.DoPurchase(repairCost))
            return;

        GridManager.Instance.PlaceWallGroup(wallCells);

        hp = maxHP;

        isBuilt = true;
        isDestroyed = false;

        foreach (WallSegment segment in segments.Values)
        {
            if (segment == null)
                continue;

            segment.gameObject.SetActive(true);
            segment.SetBuilt();

            if (TowerHeroManager.instance != null)
            {
                TowerHeroManager.instance.RegisterTower(
                    segment.gameObject
                );
            }
        }

        // Aktuelle Level-Sprites wiederherstellen
        TowerUpgradeLevel currentLevel = GetCurrentUpgrade();

        if (currentLevel != null)
            ApplyWallSprites(currentLevel.wallSprites, currentLevel.gateHorizontalClosed,currentLevel.gateHorizontalOpen,currentLevel.gateVerticalClosed,currentLevel.gateVerticalOpen);

        RefreshVisuals();

        Actions.onWallRepair?.Invoke(this);

        Debug.Log($"Wall repariert auf Level {wallLevel}");
    }
        // =========================================================
        // UPGRADE
        // =========================================================

        public void UpgradeWall()
        {
            if (!CanUpgrade())
                return;

            TowerUpgradeLevel nextUpgrade = GetNextUpgrade();

            if (nextUpgrade == null)
                return;

            int upgradeCost = GetCurrentUpgradeCost();

            if (!LevelManager.instance.DoPurchase(upgradeCost))
                return;

            ApplyUpgrade(nextUpgrade);

            wallLevel++;

            RefreshVisuals();

            Debug.Log(
                $"WallGroup '{name}' wurde auf Level {wallLevel} verbessert."
            );
        }

        private void ApplyUpgrade(TowerUpgradeLevel upgrade)
        {
            if (upgrade == null)
                return;

            foreach (WallSegment segment in segments.Values)
            {
                if (segment == null)
                    continue;

                segment.statHealthPoints += upgrade.healthpoints;
                segment.currentHealth += upgrade.healthpoints;
            }

            int totalHealthIncrease =
                upgrade.healthpoints * segments.Count;

            maxHP += totalHealthIncrease;
            hp += totalHealthIncrease;

            ApplyWallSprites(upgrade.wallSprites, upgrade.gateHorizontalClosed,upgrade.gateHorizontalOpen,upgrade.gateVerticalClosed,upgrade.gateVerticalOpen);
        }                                                                                          

        private void ApplyWallSprites(
            Sprite[] newSprites,
            Sprite gateHorizontalClosed,
            Sprite gateHorizontalOpen,
            Sprite gateVerticalClosed,
            Sprite gateVerticalOpen)
        {
            if (newSprites == null || newSprites.Length != 16)
            {
                Debug.LogWarning(
                    $"WallGroup '{name}': " +
                    "Das Upgrade besitzt keine gültigen 16 Wall-Sprites.",
                    this
                );

                return;
            }

            foreach (WallSegment segment in segments.Values)
            {
                if (segment == null)
                    continue;

                if (segment is WallGate gate)
                {
                    gate.SetGateSprites(
                        gateHorizontalClosed,
                        gateHorizontalOpen,
                        gateVerticalClosed,
                        gateVerticalOpen
                    );
                }
                else
                {
                    segment.SetWallSprites(newSprites);
                }
            }
        }

        // =========================================================
        // UPGRADE INFORMATION
        // =========================================================
        private TowerUpgradeLevel GetUpgradeLevel(int level)
        {
            if (wallUpgradePath == null ||
                wallUpgradePath.levels == null ||
                level < 0 ||
                level >= wallUpgradePath.levels.Length)
            {
                return null;
            }

            return wallUpgradePath.levels[level];
        }
        public bool CanUpgrade()
        {
            return isBuilt &&
                   !isDestroyed &&
                   GetNextUpgrade() != null;
        }

        public int GetBuildCost()
        {
            int buildCost = 0;
            foreach (var segment in segments.Values)
            {
                buildCost += segment.towerInitPrice;

            }
            return buildCost;
        }

        public int GetCurrentUpgradeCost()
        {
            if (!CanUpgrade())
                return 0;

            TowerUpgradeLevel nextUpgrade = GetNextUpgrade();

            return nextUpgrade.upgradeCost * segments.Count;
        }

        public int GetCurrentRepairCost()
        {
            TowerUpgradeLevel currentUpgrade = GetCurrentUpgrade();

            if (currentUpgrade == null)
                return (int)(wallinitprice * 0.7f * segments.Count);

            return (int)(
                currentUpgrade.upgradeCost * 0.7f * segments.Count
            );
        }

        public TowerUpgradeLevel GetCurrentUpgrade()
        {
            return GetUpgradeLevel(wallLevel);
        }
        public TowerUpgradeLevel GetNextUpgrade()
        {
            return GetUpgradeLevel(wallLevel + 1);
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
            return;

        Action action = null;
        string interactionText = "";
        string description = "";
        int cost = 0;
        SetGateOpen(true);

        // 1. Noch nicht gebaut
        if (!isBuilt && !isDestroyed)
        {
            interactionText = "to build";
            cost = GetBuildCost();
            description = towerdesc;
            action = Build;
        }

        // 2. Zerstört
        else if (isDestroyed)
        {
            interactionText = "to repair";
            cost = GetCurrentRepairCost();

            description =
                "This wall has been destroyed and has to be rebuilt!";

            action = Repair;
        }

        // 3. Gebaut und Upgrade verfügbar
        else if (isBuilt && !isDestroyed && CanUpgrade())
        {
            interactionText = "to upgrade";
            cost = GetCurrentUpgradeCost();

            TowerUpgradeLevel nextUpgrade = GetNextUpgrade();

            description = nextUpgrade.description;

            action = UpgradeWall;
        }

        // 4. Bereits gebaut, nicht zerstört, aber maximales Level
        else
        {
            HideInteractionPopup();
            return;
        }

        ShowInteractionPopup(
            interactionText,
            cost,
            description
        );

        if (interactionCoroutine != null)
            StopCoroutine(interactionCoroutine);

        interactionCoroutine =
            StartCoroutine(HoldInteraction(action));
    }
    public void ExitInteractionRange(Hero hero)
    {

        if (interactionCoroutine != null)
        {
            StopCoroutine(interactionCoroutine);
            interactionCoroutine = null;
        }
        SetGateOpen(false);

        HideInteractionPopup();
    }
    private IEnumerator HoldInteraction(Action action)
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

                float progress = Mathf.Clamp01(holdTimer / interactionHoldDuration);

                InteractionPopup.instance.SetProgress(progress);

                if (holdTimer >= interactionHoldDuration)
                {
                    CompleteInteraction(action);
                    interactionCoroutine = null;
                    yield break;
                }

                yield return null;
            }

            InteractionPopup.instance.ResetProgress();
        }
    }

    private void CompleteInteraction(Action action)
    {
        if (!LevelManager.instance.isDay)
            return;
        
        action?.Invoke();

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
    private void ShowInteractionPopup(
    string interactiontext,
    int goldCost,
    string desc)
{
    if (InteractionPopup.instance == null)
        return;

    List<UpgradeStatData> stats = new List<UpgradeStatData>();

    string title;
    int segmentCount = segments.Count;

    TowerUpgradeLevel currentLevel = GetCurrentUpgrade();
    TowerUpgradeLevel nextLevel = GetNextUpgrade();

    if (!isBuilt && !isDestroyed)
    {
        title = "Buy: " + towername;

        if (segmentCount > 0)
        {
            stats.Add(new UpgradeStatData(
                "Health",
                0,
                maxHP,
                InteractionPopup.instance.healthIcon,
                new Color(0.13f, 0.55f, 0.13f),
                $" ({maxHP / segmentCount} per wall)"
            ));
        }
    }
    else if (isDestroyed)
    {
        title = currentLevel == null
            ? "Repair: " + towername
            : "Repair: " + currentLevel.upgradeName;

        if (segmentCount > 0)
        {
            stats.Add(new UpgradeStatData(
                "Health",
                0,
                maxHP,
                InteractionPopup.instance.healthIcon,
                new Color(0.13f, 0.55f, 0.13f),
                $" ({maxHP / segmentCount} per wall)"
            ));
        }
    }
    else if (isBuilt && !isDestroyed && nextLevel != null)
    {
        title = currentLevel == null
            ? "Upgrade: " + towername + " to " + nextLevel.upgradeName
            : "Upgrade: " + currentLevel.upgradeName + " to " + nextLevel.upgradeName;

        int newHealth =
            maxHP + nextLevel.healthpoints * segmentCount;

        stats.Add(new UpgradeStatData(
            "Health",
            maxHP,
            newHealth,
            InteractionPopup.instance.healthIcon,
            new Color(0.13f, 0.55f, 0.13f),
            $" (+{nextLevel.healthpoints} per wall)"
        ));
    }
    else
    {
        return;
    }

    InteractionPopup.instance.Show(
        GetInteractionPosition(),
        interactiontext,
        title,
        goldCost,
        desc
    );

    InteractionPopup.instance.ShowUpgradeStats(stats);
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


    public bool IsGateOpen()
    {
        return wallGate.IsOpen;
    }
    public void SetGateOpen(bool isOpen)
    {
        wallGate.SetGateOpen(isOpen);
    }
}