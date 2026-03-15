# VehicleDynamicsUnity Project

VehicleDynamics is a Unity-based vehicle dynamics framework intended for simulation-oriented projects, technical prototypes, and future distribution as a reusable Unity package rather than as a standalone game. The implementation extends Unity PhysX with custom subsystems for suspension, tire-road interaction, drivetrain, steering, and runtime telemetry.

Playable Demo: https://igorciz777.github.io/VehicleDynamicsUnity/

## Overview

The repository implements a modular vehicle model with explicit separation of sprung and unsprung masses. Instead of relying on Unity's default WheelCollider workflow, it uses physically structured hub, strut, wheel, and drivetrain components executed in the fixed-step simulation loop. This provides finer control over tire forces, suspension kinematics, steering response, and force application at the contact patch.

## Implemented Scope

- Explicit suspension force calculation with spring, damper, fast bump/rebound, and bump-stop behavior.
- Independent suspension architecture with double wishbone or MacPherson-style layouts, Ackermann steering, toe/camber adjustment, and anti-roll bar support.
- Custom wheel-ground contact solved through multi-ray SphereCast sampling instead of WheelCollider.
- Tire models based on Pacejka Magic Formula, including simplified and combined-slip variants with aligning torque and rolling resistance.
- Surface-dependent friction and rolling resistance for mesh and terrain roads.
- Modular drivetrain with engine, clutch, transmission, differential logic, and support for FWD, RWD, and AWD layouts.
- Input handling for keyboard, gamepad, joystick, and DirectInput devices, with support for Force Feedback
- Runtime plotting utilities for observing slip, suspension compression, RPM, speed, and related simulation variables.

## Scientific Basis

The structure of the model follows standard vehicle-dynamics concepts used in multibody simulation and tire modeling literature. In particular, the repository draws on the separation of geometry and force elements typical of multibody systems, semi-empirical tire force formulations of the Pacejka family, and explicit spring-damper representations used in suspension analysis.

## Screenshots
![Screenshot 1](https://i.imgur.com/3f7A1DM.jpeg)
![Screenshot 2](https://i.imgur.com/vwO7NTM.jpeg)
![Screenshot 3](https://i.imgur.com/0rSlSEw.jpeg)
![Screenshot 4](https://i.imgur.com/qLeQQHA.jpeg)