# Preliminary Screening Unit Digital Twin

## Scope

This module models only the first wastewater treatment component: preliminary screening and screenings removal.
It is intentionally isolated from the full WWTP animation so it can be refined as an accurate digital twin before the next component is added.

## Engineering Assumptions

- Configuration: two mechanically cleaned screens in parallel.
- Screen type: steep step-screen style, selected as the realistic default because the site photos show enclosed mechanical screen equipment and screenings discharge.
- Screen installation angle: 75 degrees.
- Slot width: 6 mm.
- Channel width: 0.8 m per screen channel.
- Channel depth: 2.0 m.
- Normal water depth: 0.55 m.
- Bypass: one emergency/manual bypass channel with manual bar rack and stop gate.
- Average plant flow: 15,000 m3/day.
- Peak factor: 3.5.
- Allowable screening headloss threshold: 0.15 m.
- Rake cycle trigger: by timer or captured screenings load.
- Physics: screenings particles use Rigidbody gravity after discharge; flowing/captured particles are process-controlled for determinism.

These values are realistic defaults, not verified site measurements. Replace them with measured dimensions when available.

## References Used

- EPA screening/grit removal guidance: coarse screens remove rags, plastics and large debris before downstream equipment; mechanically cleaned bar screens commonly use 15-75 mm clear spacing, with approach velocity and headloss as key design criteria.
- HUBER STEP SCREEN SSV references: steep 70-75 degree installations, 6 mm slot examples, bottom-level screenings lifting, self-cleaning movable lamella principle.
- User-provided site photos: concrete channels, grating floors, blue motors/valves, yellow screenings dumpster, screen room, raw wastewater color and screenings texture.

## Scene Hierarchy Generated

`Component 01 - Preliminary Screening Unit`

- `__PreliminaryScreeningUnit_Generated`
- `Screen_Channel_1`
- `Screen_Channel_2`
- `Emergency_Bypass_Manual_Bar_Rack`
- `Shared_Transfer_Trough`
- `Screenings_Screw_Conveyor`
- `Yellow_Screenings_Dumpster`
- `Hydraulic_Flow_Particles`
- `Screenings_Debris_Particles`
- `Operator_Panel_Backplate`
- `Operator_Telemetry_Text`
- `Alarm_Beacon`

## Runtime State Machine

- `Offline`
- `Ready`
- `Running`
- `RakeCycle`
- `ScreeningsDischarge`
- `HighLevelAlarm`
- `JamFault`
- `MaintenanceLockout`

## Simulated Behavior

- Raw wastewater droplets move through both inlet channels.
- Debris particles travel with the flow.
- Particles larger than the selected slot width are captured at the screen.
- Captured load increases hydraulic headloss.
- Higher headloss raises upstream water level.
- Rake cycles start by elapsed time or captured load.
- Rake teeth travel up the screen plane and collect captured debris.
- Screenings discharge into a shared trough and fall into the yellow dumpster.
- Jam scenario raises motor current and enters `JamFault`.
- High-load scenario increases capture/headloss pressure and can enter `HighLevelAlarm`.

## Operator Controls

Keyboard controls are enabled for desktop testing:

- `P`: start/stop
- `C`: manual cleaning cycle
- `H`: high inlet load scenario
- `J`: jam fault scenario
- `R`: reset faults

Public methods exist for VR UI binding:

- `StartUnit()`
- `StopUnit()`
- `ManualRakeCycle()`
- `ResetFaults()`
- `ToggleJamFault()`
- `ToggleHighLoad()`

## Unity Setup

Use the editor menu:

`WaterVision/Build Component 01 - Preliminary Screening Unit`

This creates the module under:

`Simulation Area/Component 01 - Preliminary Screening Unit`

## Validation Checklist

- Confirm two independent screen channels are visible.
- Confirm bypass channel and manual rack are present.
- Press Play and verify water particles flow through channels.
- Verify large debris captures on the screen face.
- Press `C` and verify rake travel path moves up the screen plane.
- Verify debris falls into the dumpster with gravity.
- Press `H` and verify upstream level/headloss increase.
- Press `J` and verify alarm state and motor current increase.
- Confirm UI metrics update during operation.

## Known Follow-Up Work

- Move Rigidbody operations to `FixedUpdate`.
- Replace primitive geometry with site-measured meshes once dimensions are measured.
- Add audio for motor/rake and screenings discharge.
- Add XR grab/inspect markers for screen bars, rake teeth, level sensor, and motor.
- Add trend graph for headloss and motor current.
- Add manual/auto control mode with lockout/tagout interaction.
