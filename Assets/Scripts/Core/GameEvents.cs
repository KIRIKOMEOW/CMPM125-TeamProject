using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action<int, int> PlayerHealthChanged;
    public static event Action<int, int> BladesChanged;
    public static event Action<float, float> RecallCooldownChanged;
    public static event Action<int> CoinsChanged;
    public static event Action<Vector2, int> CoinCollected;
    public static event Action<int, int> WaveStarted;
    public static event Action<int> WaveCleared;
    public static event Action<int> EnemiesRemainingChanged;
    public static event Action<float> WaveTimerChanged;
    public static event Action<GameState> GameStateChanged;
    public static event Action<UpgradeType, int> UpgradePurchased;
    public static event Action<Vector2> BladeThrown;
    public static event Action<Vector2, bool> EnemyHit;
    public static event Action<Vector2, EnemyType> EnemyKilled;
    public static event Action<Vector2> PlayerHit;
    public static event Action<Vector2> BarrelExploded;
    public static event Action RecallStarted;

    // Wave systems and scene props use this before pooled objects are returned.
    public static event Action RunReset;

    public static void RaisePlayerHealthChanged(int current, int max) => PlayerHealthChanged?.Invoke(current, max);
    public static void RaiseBladesChanged(int inHand, int max) => BladesChanged?.Invoke(inHand, max);
    public static void RaiseRecallCooldownChanged(float remaining, float total) => RecallCooldownChanged?.Invoke(remaining, total);
    public static void RaiseCoinsChanged(int total) => CoinsChanged?.Invoke(total);
    public static void RaiseCoinCollected(Vector2 position, int amount) => CoinCollected?.Invoke(position, amount);
    public static void RaiseWaveStarted(int wave, int totalWaves) => WaveStarted?.Invoke(wave, totalWaves);
    public static void RaiseWaveCleared(int wave) => WaveCleared?.Invoke(wave);
    public static void RaiseEnemiesRemainingChanged(int count) => EnemiesRemainingChanged?.Invoke(count);
    public static void RaiseWaveTimerChanged(float seconds) => WaveTimerChanged?.Invoke(seconds);
    public static void RaiseGameStateChanged(GameState state) => GameStateChanged?.Invoke(state);
    public static void RaiseUpgradePurchased(UpgradeType type, int newLevel) => UpgradePurchased?.Invoke(type, newLevel);
    public static void RaiseBladeThrown(Vector2 position) => BladeThrown?.Invoke(position);
    public static void RaiseEnemyHit(Vector2 position, bool fromRecall) => EnemyHit?.Invoke(position, fromRecall);
    public static void RaiseEnemyKilled(Vector2 position, EnemyType type) => EnemyKilled?.Invoke(position, type);
    public static void RaisePlayerHit(Vector2 position) => PlayerHit?.Invoke(position);
    public static void RaiseBarrelExploded(Vector2 position) => BarrelExploded?.Invoke(position);
    public static void RaiseRecallStarted() => RecallStarted?.Invoke();
    public static void RaiseRunReset() => RunReset?.Invoke();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSubscribers()
    {
        PlayerHealthChanged = null;
        BladesChanged = null;
        RecallCooldownChanged = null;
        CoinsChanged = null;
        CoinCollected = null;
        WaveStarted = null;
        WaveCleared = null;
        EnemiesRemainingChanged = null;
        WaveTimerChanged = null;
        GameStateChanged = null;
        UpgradePurchased = null;
        BladeThrown = null;
        EnemyHit = null;
        EnemyKilled = null;
        PlayerHit = null;
        BarrelExploded = null;
        RecallStarted = null;
        RunReset = null;
    }
}
