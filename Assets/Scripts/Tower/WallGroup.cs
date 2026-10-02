using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ScriptableObjects;
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
        WallSegment wallSegmentPrefab)
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

        if (LevelManager.instance.DoPurchase(GetBuildCost()) == false)
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

        wallLevel++;
        RefreshVisuals();
        Actions.onWallBuilt?.Invoke(this);
    }
    public void Repair()
    {
        if (!IsDestroyed)
            return;

        if (!GridManager.Instance.CanPlaceWallGroup(wallCells))
            return;

        if (LevelManager.instance.DoPurchase(GetCurrentRepairCost()) == false)
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
        // UPGRADE
        // =========================================================

        public void UpgradeWall()
        {
            
            if (LevelManager.instance.DoPurchase(GetCurrentUpgradeCost()) == false)
                return;

            if (wallUpgradePath == null)
            {
                Debug.LogWarning(
                    $"WallGroup '{name}' besitzt keinen UpgradePath.",
                    this
                );

                return;
            }

            if (wallUpgradePath.levels == null ||
                wallUpgradePath.levels.Length == 0)
            {
                Debug.LogWarning(
                    $"WallGroup '{name}' besitzt keine Upgrade-Level.",
                    this
                );

                return;
            }

            if (wallLevel-1 >= wallUpgradePath.levels.Length)
            {
                Debug.Log(
                    $"WallGroup '{name}' ist bereits maximal verbessert."
                );

                return;
            }

            TowerUpgradeLevel upgrade = wallUpgradePath.levels[wallLevel-1];

            ApplyUpgrade(upgrade);

            wallLevel++;
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

            // Gesamt-HP der WallGroup aktualisieren
            maxHP += upgrade.healthpoints * segments.Count;

            // Aktuelle HP ebenfalls aktualisieren
            hp += upgrade.healthpoints * segments.Count;

            ApplyWallSprites(upgrade.wallSprites);

            Debug.Log(
                $"WallGroup '{name}' wurde auf Level {wallLevel + 1} verbessert."
            );
        }                                                                                                    

        private void ApplyWallSprites(Sprite[] newSprites)
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

                segment.SetWallSprites(newSprites);
            }
        }

        // =========================================================
        // UPGRADE INFORMATION
        // =========================================================

        public bool CanUpgrade()
        {
            return wallUpgradePath != null &&
                   wallUpgradePath.levels != null &&
                   wallLevel-1 < wallUpgradePath.levels.Length;
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
            int upgradeCost = 0;
            foreach (var segment in segments.Values)
            {
                upgradeCost += segment.upgradePaths[0].levels[wallLevel-1].upgradeCost;

            }
            return upgradeCost;
        }

        public int GetCurrentRepairCost()
        {
            return (int)(GetPreviousUpgrade().upgradeCost * 0.7f);
        }
        public TowerUpgradeLevel GetCurrentUpgrade()
        {
            if (!CanUpgrade())
                return null;

            return wallUpgradePath.levels[wallLevel-1];
        }
        public TowerUpgradeLevel GetPreviousUpgrade()
        {
            if (!CanUpgrade())
                return null;
            try
            {
                return wallUpgradePath.levels[wallLevel - 2];

            }
            catch
            {
                return wallUpgradePath.levels[wallLevel-1];

            }
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

        if (isBuilt && !isDestroyed && !CanUpgrade())
            return;
        
        Action action = null;
        string interactionText = "";
        int cost = 0;
        string description = "";


        if (!isBuilt)
        {
            interactionText = "to build";
            cost = GetBuildCost();
            description = towerdesc;
            action = Build;

        }
        else if (isDestroyed)
        {
            interactionText = "to repair";
            cost = GetCurrentRepairCost();
            description = "This wall has been destroyed and has to be rebuild!";
            action = Repair;
            
        }
        else if (CanUpgrade())
        {
            interactionText = "to upgrade";
            cost = GetCurrentUpgradeCost();
            action = UpgradeWall;

        }


        if (isBuilt && CanUpgrade() &&!isDestroyed)
        {
            description = GetCurrentUpgrade().description;
        }

        ShowInteractionPopup(interactionText, cost, description);
        

        if (interactionCoroutine != null)
            StopCoroutine(interactionCoroutine);

        interactionCoroutine = StartCoroutine(HoldInteraction(action));
    }
    public void ExitInteractionRange(Hero hero)
    {
        

        if (interactionCoroutine != null)
        {
            StopCoroutine(interactionCoroutine);
            interactionCoroutine = null;
        }

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
        if (isDestroyed)
        {
            if (wallLevel == 0)
            {
                title = "Repair: " + towername;
                int segmentCount = segments.Count;

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
            else
            {
                TowerUpgradeLevel currentUpgrade = GetPreviousUpgrade();

                title =  wallLevel == 1 ? "Repair: " + towername :
                    "Repair: " + currentUpgrade.upgradeName;

                int segmentCount = segments.Count;

                if (segmentCount > 0)
                {
                    
                    stats.Add(new UpgradeStatData(
                        "Health",
                        0,
                        maxHP,
                        InteractionPopup.instance.healthIcon,
                        new Color(0.13f, 0.55f, 0.13f),
                        $" (+{currentUpgrade.healthpoints} per wall)"
                    ));
                }
            }
        }
        else
        {
            
            if (wallLevel == 0)
            {
                title = "Buy: " + towername;

                int segmentCount = segments.Count;

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
            else
            {
                TowerUpgradeLevel upgrade = GetCurrentUpgrade();
                TowerUpgradeLevel currentUpgrade = GetPreviousUpgrade();
                if (upgrade == null)
                {
                    Debug.LogWarning("Kein gültiges Wall-Upgrade vorhanden.");
                    return;
                }

                title =  wallLevel == 1 ? "Upgrade: " + towername +" to " + upgrade.upgradeName :
                    "Upgrade: " + currentUpgrade.upgradeName + " to " + upgrade.upgradeName;

                int segmentCount = segments.Count;

                if (segmentCount > 0)
                {
                    int oldHealth = maxHP;

                    int newHealth = oldHealth + upgrade.healthpoints * segmentCount;

                    stats.Add(new UpgradeStatData(
                        "Health",
                        oldHealth,
                        newHealth,
                        InteractionPopup.instance.healthIcon,
                        new Color(0.13f, 0.55f, 0.13f),
                        $" (+{upgrade.healthpoints} per wall)"
                    ));
                }
            }
        }
        

        // Zuerst das Popup anzeigen
        InteractionPopup.instance.Show(
            GetInteractionPosition(),
            interactiontext,
            title,
            goldCost,
            desc
        );

        // Anschließend die Stat-Anzeige aktualisieren
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


}