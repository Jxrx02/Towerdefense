using System;

[System.Serializable]
public class LevelResult
{
    public int levelIndex;

    public bool victory;

    public int score;

    public float completionTime;

    public int remainingHealth;

    public int enemiesKilled;

    public int wavesCompleted;

    public int coinsEarned;

    // Weitere Performance-Werte
    public int damageTaken;
    public int towersLost;
    public int wallsLost;
    public int towersBuilt;
    public int wallsBuilt;
}