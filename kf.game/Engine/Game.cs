using KnightFight.Game.Actions;
using KnightFight.Game.Bonuses;
using KnightFight.Game.Common;
using KnightFight.Game.Entities;
using KnightFight.Game.Weapons;

namespace KnightFight.Game.Engine;

/// <summary>
/// Core implementation of a KnightFight game match.
/// </summary>
public sealed class Game : IGame
{
    private readonly object _syncRoot = new();
    private readonly GameConfig _config;
    private readonly IRandomProvider _random;
    private readonly TimeProvider _time;
    private readonly List<BonusEntity> _bonuses = [];

    public Guid Id { get; }

    /// <inheritdoc />
    public DateTime StartedAt { get; }

    public int TurnNumber { get; private set; }
    public GameStatus Status { get; private set; }
    public GameWinner Winner { get; private set; }
    public BoardBounds Bounds { get; private set; }

    public Fighter Fighter1 { get; }
    public Fighter Fighter2 { get; }
    public IReadOnlyList<BonusEntity> Bonuses
    {
        get
        {
            lock (_syncRoot)
            {
                return _bonuses.ToArray();
            }
        }
    }

    public Game(
        string fighter1Id,
        string fighter2Id,
        string? fighter1Name = null,
        string? fighter2Name = null,
        GameConfig? config = null,
        IRandomProvider? randomProvider = null,
        Guid? gameId = null,
        TimeProvider? timeProvider = null)
    {
        _config = config ?? GameConfig.Default;
        _random = randomProvider ?? new DefaultRandomProvider();
        _time = timeProvider ?? TimeProvider.System;
        Id = gameId ?? Guid.NewGuid();
        StartedAt = _time.GetUtcNow().UtcDateTime;

        Bounds = _config.InitialBounds;
        TurnNumber = 0;
        Status = GameStatus.InProgress;
        Winner = GameWinner.None;

        var f1Main = _random.NextMainWeapon();
        var f1Off = _random.NextOffhandItem();
        var f2Main = _random.NextMainWeapon();
        var f2Off = _random.NextOffhandItem();

        Fighter1 = new Fighter(
            fighter1Id,
            fighter1Name ?? fighter1Id,
            _config.Fighter1StartPosition,
            _config.Fighter1StartDirection,
            f1Main,
            f1Off,
            _config.InitialFighterHp
        );

        Fighter2 = new Fighter(
            fighter2Id,
            fighter2Name ?? fighter2Id,
            _config.Fighter2StartPosition,
            _config.Fighter2StartDirection,
            f2Main,
            f2Off,
            _config.InitialFighterHp
        );

        var bonus1Type = _random.NextBonusType();
        var bonus2Type = _random.NextBonusType();

        _bonuses.Add(new BonusEntity(bonus1Type, _config.Bonus1Position, _config.InitialBonusHp));
        _bonuses.Add(new BonusEntity(bonus2Type, _config.Bonus2Position, _config.InitialBonusHp));
    }

    public bool HasChallenger(string challengerId) =>
        string.Equals(Fighter1.Id, challengerId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Fighter2.Id, challengerId, StringComparison.OrdinalIgnoreCase);

    public Fighter? GetFighter(string challengerId)
    {
        if (string.Equals(Fighter1.Id, challengerId, StringComparison.OrdinalIgnoreCase)) return Fighter1;
        if (string.Equals(Fighter2.Id, challengerId, StringComparison.OrdinalIgnoreCase)) return Fighter2;
        return null;
    }

    public bool EnqueueAction(string challengerId, GameAction action)
    {
        lock (_syncRoot)
        {
            if (Status == GameStatus.Finished) return false;
            var fighter = GetFighter(challengerId);
            return fighter != null && fighter.EnqueueAction(action);
        }
    }

    public IReadOnlyList<GameAction> GetBufferedActions(string challengerId)
    {
        lock (_syncRoot)
        {
            var fighter = GetFighter(challengerId);
            return fighter?.PeekBufferedActions() ?? Array.Empty<GameAction>();
        }
    }

    public void ClearBufferedActions(string challengerId)
    {
        lock (_syncRoot)
        {
            GetFighter(challengerId)?.ClearBufferedActions();
        }
    }

    public TurnResult CalculateTurn()
    {
        lock (_syncRoot)
        {
            if (Status == GameStatus.Finished)
            {
                return new TurnResult(
                    TurnNumber,
                    GameAction.Wait,
                    GameAction.Wait,
                    Bounds,
                    Bounds,
                    false,
                    [new GameEvent("GameAlreadyFinished", "Game has already concluded.", null, null, null)],
                    CreateSnapshot()
                );
            }

            TurnNumber++;
            var events = new List<GameEvent>();
            var boundsBefore = Bounds;

            // Step 1: Reset stances
            Fighter1.ResetTurnStance();
            Fighter2.ResetTurnStance();

            // Step 2: Pop queued actions
            var action1 = Fighter1.IsAlive ? Fighter1.DequeueAction() : GameAction.Wait;
            var action2 = Fighter2.IsAlive ? Fighter2.DequeueAction() : GameAction.Wait;

            events.Add(new GameEvent("ActionPopped", $"{Fighter1.Name} performed {action1}", Fighter1.Id));
            events.Add(new GameEvent("ActionPopped", $"{Fighter2.Name} performed {action2}", Fighter2.Id));

            // Step 3: Rotations & Shield Stances
            ApplyRotationsAndStances(Fighter1, action1, events);
            ApplyRotationsAndStances(Fighter2, action2, events);

            // Step 4: Movement
            ApplyMovement(Fighter1, action1, events);
            ApplyMovement(Fighter2, action2, events);

            // Step 5: Bonus Pickups
            CheckBonusPickups(events);

            // Step 6: Offhand Utility (Hammer / Grappling Hook)
            ApplyOffhandUtilities(Fighter1, Fighter2, action1, events);
            ApplyOffhandUtilities(Fighter2, Fighter1, action2, events);

            // Step 7: Main Weapon Attacks (simultaneous resolution)
            bool f1CanAttack = Fighter1.IsAlive && action1 == GameAction.HitMain;
            bool f2CanAttack = Fighter2.IsAlive && action2 == GameAction.HitMain;
            ResolveMainWeaponAttacks(f1CanAttack, f2CanAttack, events);

            // Step 8: Ring Bonus Effect
            ApplyRingBonus(Fighter1, Fighter2, events);
            ApplyRingBonus(Fighter2, Fighter1, events);

            // Step 9: Map Shrinking
            bool didShrink = false;
            if (TurnNumber % _config.TurnsPerShrink == 0)
            {
                Bounds = Bounds.Shrink(_config.ShrinkAmount);
                didShrink = true;
                events.Add(new GameEvent("BoardShrunk", $"Field shrunk to {Bounds}", null, null, null));
            }

            // Step 10: Out-of-Bounds Damage
            ApplyOutOfBoundsDamage(events);

            // Step 11: Update Win/Loss Status
            UpdateGameStatus(events);

            var snapshot = CreateSnapshot();
            return new TurnResult(
                TurnNumber,
                action1,
                action2,
                boundsBefore,
                Bounds,
                didShrink,
                events,
                snapshot
            );
        }
    }

    private static void ApplyRotationsAndStances(Fighter fighter, GameAction action, List<GameEvent> events)
    {
        if (!fighter.IsAlive) return;

        switch (action)
        {
            case GameAction.TurnLeft:
                fighter.Direction = fighter.Direction.TurnLeft();
                events.Add(new GameEvent("TurnedLeft", $"{fighter.Name} turned left, now facing {fighter.Direction}", fighter.Id));
                break;
            case GameAction.TurnRight:
                fighter.Direction = fighter.Direction.TurnRight();
                events.Add(new GameEvent("TurnedRight", $"{fighter.Name} turned right, now facing {fighter.Direction}", fighter.Id));
                break;
            case GameAction.HitOffhand when fighter.OffhandItem == OffhandItem.Shield:
                fighter.IsShieldActive = true;
                events.Add(new GameEvent("ShieldRaised", $"{fighter.Name} raised shield", fighter.Id));
                break;
        }
    }

    private static void ApplyMovement(Fighter fighter, GameAction action, List<GameEvent> events)
    {
        if (!fighter.IsAlive || action != GameAction.Move) return;

        int steps = fighter.HasBonus(BonusType.Horse) ? 2 : 1;
        var dir = fighter.HasBonus(BonusType.Reverse) ? fighter.Direction.Opposite() : fighter.Direction;

        fighter.Position = fighter.Position.Move(dir, steps);
        events.Add(new GameEvent("Moved", $"{fighter.Name} moved to {fighter.Position}", fighter.Id, fighter.Position, steps));
    }

    private void CheckBonusPickups(List<GameEvent> events)
    {
        CheckFighterBonusPickup(Fighter1, Fighter2, events);
        CheckFighterBonusPickup(Fighter2, Fighter1, events);
    }

    private void CheckFighterBonusPickup(Fighter fighter, Fighter opponent, List<GameEvent> events)
    {
        if (!fighter.IsAlive) return;

        foreach (var bonus in _bonuses.Where(b => b.IsPresentOnField))
        {
            if (bonus.Position == fighter.Position)
            {
                bonus.IsCollected = true;
                events.Add(new GameEvent("BonusCollected", $"{fighter.Name} picked up {bonus.Type}", fighter.Id, bonus.Position));

                switch (bonus.Type)
                {
                    case BonusType.Heart:
                        fighter.Heal(1);
                        events.Add(new GameEvent("BonusHeartApplied", $"{fighter.Name} gained 1 HP, now at {fighter.HitPoints}", fighter.Id, null, 1));
                        break;
                    case BonusType.Horse:
                        fighter.AddBonus(BonusType.Horse);
                        events.Add(new GameEvent("BonusHorseApplied", $"{fighter.Name} gained Horse bonus (moves 2 squares)", fighter.Id));
                        break;
                    case BonusType.Reverse:
                        fighter.AddBonus(BonusType.Reverse);
                        events.Add(new GameEvent("BonusReverseApplied", $"{fighter.Name} gained Reverse bonus (moves backwards)", fighter.Id));
                        break;
                    case BonusType.Ring:
                        fighter.AddBonus(BonusType.Ring);
                        events.Add(new GameEvent("BonusRingApplied", $"{fighter.Name} gained Ring bonus (repels adjacent entities)", fighter.Id));
                        break;
                    case BonusType.Tp:
                        (fighter.Position, opponent.Position) = (opponent.Position, fighter.Position);
                        events.Add(new GameEvent("BonusTpApplied", $"{fighter.Name} swapped positions with {opponent.Name}", fighter.Id));
                        break;
                    case BonusType.Bolt:
                        opponent.TakeDamage(1);
                        events.Add(new GameEvent("BonusBoltApplied", $"{opponent.Name} struck by bolt for 1 damage, HP={opponent.HitPoints}", opponent.Id, null, 1));
                        break;
                    case BonusType.Storm:
                        fighter.TakeDamage(1);
                        opponent.TakeDamage(1);
                        events.Add(new GameEvent("BonusStormApplied", $"Storm hit both fighters for 1 damage. {fighter.Name} HP={fighter.HitPoints}, {opponent.Name} HP={opponent.HitPoints}", null, null, 1));
                        break;
                }
            }
        }
    }

    private void ApplyOffhandUtilities(Fighter user, Fighter opponent, GameAction action, List<GameEvent> events)
    {
        if (!user.IsAlive || action != GameAction.HitOffhand) return;

        switch (user.OffhandItem)
        {
            case OffhandItem.Hammer:
            {
                var targetPos = user.Position.Move(user.Direction, 1);
                if (opponent.IsAlive && opponent.Position == targetPos)
                {
                    opponent.Position = opponent.Position.Move(user.Direction, 2);
                    events.Add(new GameEvent("HammerPushedFighter", $"{user.Name} pushed {opponent.Name} 2 squares to {opponent.Position}", user.Id, opponent.Position, 2));
                }

                foreach (var bonus in _bonuses.Where(b => b.IsPresentOnField && b.Position == targetPos))
                {
                    bonus.Position = bonus.Position.Move(user.Direction, 2);
                    events.Add(new GameEvent("HammerPushedBonus", $"{user.Name} hammered bonus to {bonus.Position}", user.Id, bonus.Position, 2));
                }
                break;
            }

            case OffhandItem.GrapplingHook:
            {
                bool hit = false;
                for (int d = 1; d <= 3; d++)
                {
                    var targetPos = user.Position.Move(user.Direction, d);
                    if (opponent.IsAlive && opponent.Position == targetPos)
                    {
                        if (d > 1)
                        {
                            opponent.Position = user.Position.Move(user.Direction, d - 1);
                        }
                        events.Add(new GameEvent("GrappledFighter", $"{user.Name} hooked {opponent.Name} to {opponent.Position}", user.Id, opponent.Position, 1));
                        hit = true;
                        break;
                    }

                    var bonus = _bonuses.FirstOrDefault(b => b.IsPresentOnField && b.Position == targetPos);
                    if (bonus != null)
                    {
                        if (d > 1)
                        {
                            bonus.Position = user.Position.Move(user.Direction, d - 1);
                        }
                        events.Add(new GameEvent("GrappledBonus", $"{user.Name} hooked bonus to {bonus.Position}", user.Id, bonus.Position, 1));
                        hit = true;
                        break;
                    }
                }

                if (!hit)
                {
                    events.Add(new GameEvent("GrappleMissed", $"{user.Name} used grappling hook but caught nothing", user.Id));
                }
                break;
            }
        }
    }

    private void ResolveMainWeaponAttacks(bool f1CanAttack, bool f2CanAttack, List<GameEvent> events)
    {
        int f1DamageTaken = 0;
        int f2DamageTaken = 0;
        var bonusesDamage = new Dictionary<BonusEntity, int>();

        if (f1CanAttack)
        {
            var targetPositions = Fighter1.MainWeapon.GetTargetPositions(Fighter1.Position, Fighter1.Direction);
            events.Add(new GameEvent("MainWeaponAttacked", $"{Fighter1.Name} attacked with {Fighter1.MainWeapon}", Fighter1.Id));

            foreach (var pos in targetPositions)
            {
                if (Fighter2.IsAlive && Fighter2.Position == pos)
                {
                    if (IsBlockedByShield(Fighter1.Position, Fighter2))
                    {
                        events.Add(new GameEvent("AttackBlocked", $"{Fighter2.Name} blocked attack from {Fighter1.Name} with shield", Fighter2.Id));
                    }
                    else
                    {
                        f2DamageTaken += 1;
                        events.Add(new GameEvent("FighterDamaged", $"{Fighter2.Name} took 1 damage from {Fighter1.Name}", Fighter2.Id, pos, 1));
                    }
                }

                foreach (var bonus in _bonuses.Where(b => b.IsPresentOnField && b.Position == pos))
                {
                    bonusesDamage[bonus] = bonusesDamage.GetValueOrDefault(bonus, 0) + 1;
                    events.Add(new GameEvent("BonusDamaged", $"Bonus {bonus.Type} at {pos} was hit and took 1 damage", null, pos, 1));
                }
            }
        }

        if (f2CanAttack)
        {
            var targetPositions = Fighter2.MainWeapon.GetTargetPositions(Fighter2.Position, Fighter2.Direction);
            events.Add(new GameEvent("MainWeaponAttacked", $"{Fighter2.Name} attacked with {Fighter2.MainWeapon}", Fighter2.Id));

            foreach (var pos in targetPositions)
            {
                if (Fighter1.IsAlive && Fighter1.Position == pos)
                {
                    if (IsBlockedByShield(Fighter2.Position, Fighter1))
                    {
                        events.Add(new GameEvent("AttackBlocked", $"{Fighter1.Name} blocked attack from {Fighter2.Name} with shield", Fighter1.Id));
                    }
                    else
                    {
                        f1DamageTaken += 1;
                        events.Add(new GameEvent("FighterDamaged", $"{Fighter1.Name} took 1 damage from {Fighter2.Name}", Fighter1.Id, pos, 1));
                    }
                }

                foreach (var bonus in _bonuses.Where(b => b.IsPresentOnField && b.Position == pos))
                {
                    bonusesDamage[bonus] = bonusesDamage.GetValueOrDefault(bonus, 0) + 1;
                    events.Add(new GameEvent("BonusDamaged", $"Bonus {bonus.Type} at {pos} was hit and took 1 damage", null, pos, 1));
                }
            }
        }

        if (f1DamageTaken > 0) Fighter1.TakeDamage(f1DamageTaken);
        if (f2DamageTaken > 0) Fighter2.TakeDamage(f2DamageTaken);

        foreach (var (bonus, dmg) in bonusesDamage)
        {
            bonus.TakeDamage(dmg);
        }
    }

    private static bool IsBlockedByShield(Position attackerPos, Fighter defender)
    {
        if (!defender.IsShieldActive) return false;

        // Frontal defense: check if attacker position is in front of the defender
        var (fDx, fDy) = defender.Direction.ToDelta();
        int dx = attackerPos.X - defender.Position.X;
        int dy = attackerPos.Y - defender.Position.Y;

        return (dx * fDx + dy * fDy) > 0;
    }

    private void ApplyRingBonus(Fighter user, Fighter opponent, List<GameEvent> events)
    {
        if (!user.IsAlive || !user.HasBonus(BonusType.Ring)) return;

        if (opponent.IsAlive && user.Position.ChebyshevDistance(opponent.Position) == 1)
        {
            int dx = Math.Sign(opponent.Position.X - user.Position.X);
            int dy = Math.Sign(opponent.Position.Y - user.Position.Y);
            if (dx == 0 && dy == 0) dx = 1;

            opponent.Position = opponent.Position.Offset(dx, dy);
            events.Add(new GameEvent("RingPushedFighter", $"{user.Name}'s ring pushed {opponent.Name} to {opponent.Position}", user.Id, opponent.Position));
        }

        foreach (var bonus in _bonuses.Where(b => b.IsPresentOnField && user.Position.ChebyshevDistance(b.Position) == 1))
        {
            int dx = Math.Sign(bonus.Position.X - user.Position.X);
            int dy = Math.Sign(bonus.Position.Y - user.Position.Y);
            if (dx == 0 && dy == 0) dx = 1;

            bonus.Position = bonus.Position.Offset(dx, dy);
            events.Add(new GameEvent("RingPushedBonus", $"{user.Name}'s ring pushed bonus to {bonus.Position}", user.Id, bonus.Position));
        }
    }

    private void ApplyOutOfBoundsDamage(List<GameEvent> events)
    {
        if (Fighter1.IsAlive && !Bounds.Contains(Fighter1.Position))
        {
            Fighter1.TakeDamage(_config.OutOfBoundsDamagePerTurn);
            events.Add(new GameEvent("OutOfBoundsDamage", $"{Fighter1.Name} is outside map at {Fighter1.Position} and took {_config.OutOfBoundsDamagePerTurn} damage, HP={Fighter1.HitPoints}", Fighter1.Id, Fighter1.Position, _config.OutOfBoundsDamagePerTurn));
        }

        if (Fighter2.IsAlive && !Bounds.Contains(Fighter2.Position))
        {
            Fighter2.TakeDamage(_config.OutOfBoundsDamagePerTurn);
            events.Add(new GameEvent("OutOfBoundsDamage", $"{Fighter2.Name} is outside map at {Fighter2.Position} and took {_config.OutOfBoundsDamagePerTurn} damage, HP={Fighter2.HitPoints}", Fighter2.Id, Fighter2.Position, _config.OutOfBoundsDamagePerTurn));
        }

        foreach (var bonus in _bonuses.Where(b => b.IsPresentOnField))
        {
            if (!Bounds.Contains(bonus.Position))
            {
                bonus.TakeDamage(_config.OutOfBoundsDamagePerTurn);
                events.Add(new GameEvent("BonusOutOfBoundsDamage", $"Bonus {bonus.Type} outside map at {bonus.Position} took {_config.OutOfBoundsDamagePerTurn} damage (HP={bonus.HitPoints})", null, bonus.Position, _config.OutOfBoundsDamagePerTurn));
            }
        }
    }

    private void UpdateGameStatus(List<GameEvent> events)
    {
        bool f1Alive = Fighter1.IsAlive;
        bool f2Alive = Fighter2.IsAlive;

        if (!f1Alive && !f2Alive)
        {
            Status = GameStatus.Finished;
            Winner = GameWinner.Draw;
            events.Add(new GameEvent("GameEnded", "Both fighters died. The match is a draw.", null, null, null));
        }
        else if (!f1Alive)
        {
            Status = GameStatus.Finished;
            Winner = GameWinner.Fighter2;
            events.Add(new GameEvent("GameEnded", $"{Fighter2.Name} won the match!", Fighter2.Id, null, null));
        }
        else if (!f2Alive)
        {
            Status = GameStatus.Finished;
            Winner = GameWinner.Fighter1;
            events.Add(new GameEvent("GameEnded", $"{Fighter1.Name} won the match!", Fighter1.Id, null, null));
        }
        else
        {
            Status = GameStatus.InProgress;
            Winner = GameWinner.None;
        }
    }

    public GameSnapshot CreateSnapshot()
    {
        lock (_syncRoot)
        {
            var f1Snapshot = new FighterSnapshot(
                Fighter1.Id,
                Fighter1.Name,
                Fighter1.Position,
                Fighter1.Direction,
                Fighter1.HitPoints,
                Fighter1.MaxHitPoints,
                Fighter1.MainWeapon,
                Fighter1.OffhandItem,
                Fighter1.ActiveBonuses,
                Fighter1.IsShieldActive,
                Fighter1.IsAlive,
                Fighter1.BufferedMovesCount
            );

            var f2Snapshot = new FighterSnapshot(
                Fighter2.Id,
                Fighter2.Name,
                Fighter2.Position,
                Fighter2.Direction,
                Fighter2.HitPoints,
                Fighter2.MaxHitPoints,
                Fighter2.MainWeapon,
                Fighter2.OffhandItem,
                Fighter2.ActiveBonuses,
                Fighter2.IsShieldActive,
                Fighter2.IsAlive,
                Fighter2.BufferedMovesCount
            );

            var bonusSnapshots = _bonuses.Select(b => new BonusSnapshot(
                b.Id,
                b.Type,
                b.Position,
                b.HitPoints,
                b.IsCollected,
                b.IsDestroyed
            )).ToArray();

            return new GameSnapshot(
                Id,
                TurnNumber,
                Status,
                Winner,
                Bounds,
                f1Snapshot,
                f2Snapshot,
                bonusSnapshots
            );
        }
    }
}
