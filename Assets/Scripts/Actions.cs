

using System;
using System.Collections.Generic;
using System.Collections;
using TowerDefense;
using UnityEngine;


public static class Actions
{
    public static Action <GameObject> onEnemyReachedEnd;
    public static Action <GameObject> onEnemyDeath;
    public static Action <GameObject> onEnemySpawn;

    public static Action onLvlComplete;
    public static Action onWaveSpawnComplete;
    public static Action onWaveCleared;

    public static Action <WallGroup> onWallBuilt;
    public static Action <WallGroup> onWallDestroyed;
    public static Action <WallGroup> onWallSold;

    public static Action <WallGroup> onWallRepair;

    public static Action onPlayerDeath;
    
    public static Action onGridChanged;
    public static Action<GameObject> TowerPlaced;


}
