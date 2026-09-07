# Modbus Register Mapping

## Holding Registers

| Register | Address | Description | Scaling | Example |
|----------|---------|-------------|---------|---------|
| HR0 | 0 | Temperature (°C) | ÷ 10 | 255 → 25.5°C |
| HR1 | 1 | Pressure (kPa) | ÷ 10 | 1013 → 101.3 kPa |
| HR2 | 2 | Speed (RPM) | Direct | 1500 → 1500 RPM |
| HR3 | 3 | Flow rate (L/min) | ÷ 10 | 100 → 10.0 L/min |
| HR4 | 4 | Temperature setpoint (°C) | ÷ 10 | Write only |
| HR5 | 5 | Speed setpoint (RPM) | Direct | Write only |
| HR6 | 6 | Pressure limit (kPa) | ÷ 10 | Write only |

## Coils

| Coil | Address | Description | Notes |
|------|---------|-------------|-------|
| Coil0 | 0 | Running | Always true during simulation |
| Coil1 | 1 | Alarm active | Set by AlarmEngine |
| Coil2 | 2 | Emergency stop | false during simulation |
| Coil3 | 3 | Heating | Temperature < Setpoint |

## Connection Parameters

- Protocol: Modbus TCP
- Default IP: 127.0.0.1
- Default Port: 502
- Slave ID: 1
- Poll interval: 500 ms (configurable: 100/500/1000/2000)
