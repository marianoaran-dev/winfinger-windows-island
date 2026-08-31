using CommunityToolkit.Mvvm.ComponentModel;
using Windows.System.Power;

namespace WinFinger.Services;

public readonly record struct PowerStatusSnapshot(
    BatteryStatus BatteryStatus,
    PowerSupplyStatus PowerSupplyStatus,
    int RemainingChargePercent)
{
    public bool IsCharging =>
        BatteryStatus == BatteryStatus.Charging ||
        (PowerSupplyStatus == PowerSupplyStatus.Adequate && BatteryStatus != BatteryStatus.Discharging);
}

public readonly record struct PowerStatusChange(
    PowerStatusSnapshot Previous,
    PowerStatusSnapshot Current);

/// <summary>
/// Event-driven Windows battery/power-state bridge for DynamicNotch activities.
/// PowerManager supplies native change notifications, so no background polling loop is required.
/// </summary>
public sealed partial class PowerStatusService : ObservableObject
{
    [ObservableProperty] private BatteryStatus _batteryStatus = BatteryStatus.NotPresent;
    [ObservableProperty] private PowerSupplyStatus _powerSupplyStatus = PowerSupplyStatus.NotPresent;
    [ObservableProperty] private int _remainingChargePercent = -1;

    private bool _started;
    private PowerStatusSnapshot _snapshot = ReadSnapshot();

    public event Action<PowerStatusChange>? StatusChanged;

    public void Start()
    {
        if (_started) return;
        _started = true;

        _snapshot = ReadSnapshot();
        Apply(_snapshot);

        PowerManager.BatteryStatusChanged += OnPowerChanged;
        PowerManager.PowerSupplyStatusChanged += OnPowerChanged;
        PowerManager.RemainingChargePercentChanged += OnPowerChanged;
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;

        PowerManager.BatteryStatusChanged -= OnPowerChanged;
        PowerManager.PowerSupplyStatusChanged -= OnPowerChanged;
        PowerManager.RemainingChargePercentChanged -= OnPowerChanged;
    }

    private void OnPowerChanged(object? sender, object args)
    {
        var previous = _snapshot;
        var current = ReadSnapshot();
        if (current == previous) return;

        _snapshot = current;
        Apply(current);
        StatusChanged?.Invoke(new PowerStatusChange(previous, current));
    }

    private void Apply(PowerStatusSnapshot snapshot)
    {
        BatteryStatus = snapshot.BatteryStatus;
        PowerSupplyStatus = snapshot.PowerSupplyStatus;
        RemainingChargePercent = snapshot.RemainingChargePercent;
    }

    private static PowerStatusSnapshot ReadSnapshot() => new(
        PowerManager.BatteryStatus,
        PowerManager.PowerSupplyStatus,
        PowerManager.RemainingChargePercent);
}
