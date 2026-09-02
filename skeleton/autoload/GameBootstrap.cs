using Godot;
using System;
using System.Collections.Generic;

// Last Animal — M01 core-framework (MC 839.3, teddy, 2026-09-01).
//
// GameBootstrap: the composition root + service locator (C3). Autoload (I1:
// orchestration only — binding, ordering, resolving; NO gameplay logic).
//
// Contract (C3):
//   Bind<T>(T instance) / Resolve<T>()  — service-locator round-trip.
//   Boot order = module registration order (I6): modules boot in the order
//   their Bind<T> calls are made; EventBus is wired first so modules can
//   subscribe before any of them boots.
namespace LastAnimal.Core;

public partial class GameBootstrap : Node
{
    private readonly Dictionary<Type, object> _services = new();
    // I6/C3: boot order = registration order. A Dictionary keyed by type cannot
    // hold two registrations of the same module type (a second Bind<T> would
    // overwrite the first and silently drop it), so module booting walks this
    // ordered list while Resolve<T> stays type-keyed.
    private readonly List<object> _bootOrder = new();

    // C3 — bind an instance under its runtime type. Registration ORDER is the
    // boot order (I6), so call these in the order you want modules to start.
    public void Bind<T>(T instance) where T : class
    {
        if (instance is null)
            throw new ArgumentException("cannot bind null", nameof(instance));
        _services[typeof(T)] = instance;
        _bootOrder.Add(instance);
    }

    // C3 — resolve a previously bound service. Returns null (not throw) so a
    // missing optional module is a clean no-op; callers check for null.
    public T? Resolve<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var s))
            return (T)s;
        return null;
    }

    public bool Has<T>() where T : class => _services.ContainsKey(typeof(T));

    // C3 — run every registered IGameModule in registration order. EventBus is
    // always booted first (it must exist before any module subscribes).
    public void Boot()
    {
        _bootCounter = 0;

        foreach (var service in _bootOrder)
        {
            if (service is IGameModule module)
            {
                GD.Print("GameBootstrap: booting module ", module.Name,
                         " (order ", _bootCounter, ")");
                _bootCounter++;
                module.Boot();
            }
        }
    }

    private int _bootCounter;
}
