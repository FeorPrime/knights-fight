using KnightFight.Game.Actions;
using KnightFight.Game.Bonuses;
using KnightFight.Game.Common;
using KnightFight.Game.Weapons;

namespace KnightFight.Game.Entities;

/// <summary>
/// Represents a fighter/challenger knight on the battlefield.
/// </summary>
public sealed class Fighter
{
    public const int MaxBufferedMoves = 3;

    private readonly Queue<GameAction> _actionQueue = new(MaxBufferedMoves);
    private readonly HashSet<BonusType> _activeBonuses = [];
    private readonly object _lock = new();

    public string Id { get; }
    public string Name { get; }
    public Position Position { get; set; }
    public Direction Direction { get; set; }
    public int HitPoints { get; set; }
    public int MaxHitPoints { get; set; }
    public MainWeapon MainWeapon { get; set; }
    public OffhandItem OffhandItem { get; set; }
    public bool IsShieldActive { get; set; }

    public bool IsAlive => HitPoints > 0;
    public IReadOnlyCollection<BonusType> ActiveBonuses
    {
        get
        {
            lock (_lock)
            {
                return _activeBonuses.ToArray();
            }
        }
    }

    public int BufferedMovesCount
    {
        get
        {
            lock (_lock)
            {
                return _actionQueue.Count;
            }
        }
    }

    public Fighter(
        string id,
        string name,
        Position initialPosition,
        Direction initialDirection,
        MainWeapon mainWeapon,
        OffhandItem offhandItem,
        int initialHp = 3)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? id;
        Position = initialPosition;
        Direction = initialDirection;
        MainWeapon = mainWeapon;
        OffhandItem = offhandItem;
        HitPoints = Math.Max(0, initialHp);
        MaxHitPoints = HitPoints;
    }

    /// <summary>
    /// Attempts to enqueue a move. Returns true if queued, false if queue is full (max 3).
    /// </summary>
    public bool EnqueueAction(GameAction action)
    {
        lock (_lock)
        {
            if (_actionQueue.Count >= MaxBufferedMoves)
            {
                return false;
            }

            _actionQueue.Enqueue(action);
            return true;
        }
    }

    /// <summary>
    /// Dequeues the next action to perform. If empty, returns GameAction.Wait.
    /// </summary>
    public GameAction DequeueAction()
    {
        lock (_lock)
        {
            return _actionQueue.Count > 0 ? _actionQueue.Dequeue() : GameAction.Wait;
        }
    }

    /// <summary>
    /// Clears any buffered moves in the queue.
    /// </summary>
    public void ClearBufferedActions()
    {
        lock (_lock)
        {
            _actionQueue.Clear();
        }
    }

    public IReadOnlyList<GameAction> PeekBufferedActions()
    {
        lock (_lock)
        {
            return _actionQueue.ToArray();
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || !IsAlive) return;
        HitPoints = Math.Max(0, HitPoints - damage);
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || !IsAlive) return;
        HitPoints += amount;
        if (HitPoints > MaxHitPoints)
        {
            MaxHitPoints = HitPoints;
        }
    }

    public void AddBonus(BonusType bonus)
    {
        lock (_lock)
        {
            _activeBonuses.Add(bonus);
        }
    }

    public bool HasBonus(BonusType bonus)
    {
        lock (_lock)
        {
            return _activeBonuses.Contains(bonus);
        }
    }

    public void ResetTurnStance()
    {
        IsShieldActive = false;
    }
}
