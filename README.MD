# VehicleDynamicsUnity Project

VehicleDynamics is a Unity-based vehicle dynamics framework intended for simulation-oriented projects, technical prototypes, and future distribution as a reusable Unity package rather than as a standalone game. The implementation extends Unity PhysX with custom subsystems for suspension, tire-road interaction, drivetrain, steering, and runtime telemetry.

Playable Demo: https://igorciz777.github.io/VehicleDynamicsUnity/

## Requirements
- Unity 6.0 and newer, might work on older versions (untested)
- Unity Input System and TextMesh Pro
- Unity DirectInput https://github.com/MrTimcakes/Unity-DirectInput

## Overview

The repository implements a modular vehicle model with explicit separation of sprung and unsprung masses. Instead of relying on Unity's default WheelCollider workflow, it uses physically structured hub, strut, wheel, and drivetrain components executed in the fixed-step simulation loop. The tire-ground contact itself is solved through multi-ray SphereCast sampling to determine contact points, normals, and slip conditions, which are then fed into a custom Pacejka Magic Formula-based tire model. The drivetrain supports engine torque curves, clutch slip, multi-gear transmission logic, and differential behavior for various drive layouts. Input handling is designed to accommodate multiple device types with optional Force Feedback support.

## Implemented Scope

- Explicit suspension force calculation with spring, damper, fast bump/rebound, and bump-stop behavior.
- Independent suspension architecture with double wishbone or MacPherson-style layouts, Ackermann steering, toe/camber adjustment, and anti-roll bar support.
- Custom wheel-ground contact solved through multi-ray SphereCast sampling instead of WheelCollider.
- Tire models based on Pacejka Magic Formula combined-slip variant with aligning torque and rolling resistance.
- Surface-dependent friction and rolling resistance for mesh and terrain (per terrain texture) roads.
- Modular drivetrain with engine, clutch, transmission, differential logic, and support for FWD, RWD, and AWD layouts.
- Input handling for keyboard, gamepad, joystick, and DirectInput devices, with support for Force Feedback
- Runtime plotting utilities for observing slip, suspension compression, RPM, speed, and related simulation variables.
- Runtime test case framework for defining and executing repeatable scenarios with telemetry recording.

## Screenshots
![Screenshot 1](https://i.imgur.com/3f7A1DM.jpeg)
![Screenshot 2](https://i.imgur.com/vwO7NTM.jpeg)
![Screenshot 3](https://i.imgur.com/0rSlSEw.jpeg)
![Screenshot 4](https://i.imgur.com/qLeQQHA.jpeg)

## Sources used

1. H. B. Pacejka, *Tire and Vehicle Dynamics*, 3rd ed., Butterworth-Heinemann, 2012.
2. M. Blundell, D. Harty, *The Multibody Systems Approach to Vehicle Dynamics*, 2nd ed., Butterworth-Heinemann, 2015.
3. R. Rajamani, *Vehicle Dynamics and Control*, 2nd ed., Springer, 2012.
4. B. Mashadi, D. A. Crolla, *Vehicle Powertrain Systems*, John Wiley & Sons, 2012.
5. W. F. Milliken, D. L. Milliken, *Race Car Vehicle Dynamics*, SAE International, 1995.
6. J. C. Dixon, *The Shock Absorber Handbook*, 2nd ed., Wiley, 2007.
7. M. Croft-White, *Measurement and Analysis of Rally Car Dynamics at High Attitude Angles*, PhD thesis, Cranfield University, 2006.
8. Unity Technologies, *Unity User Manual - Physics, Rigidbody, ConfigurableJoint, Input System*, https://docs.unity3d.com/.
9. NVIDIA, *NVIDIA PhysX SDK Documentation*, https://nvidia-omniverse.github.io/PhysX/.
10. MrTimcakes, *Unity-DirectInput*, https://github.com/MrTimcakes/Unity-DirectInput.
11. LemonMontage420, *TORSION-Community-Edition*, https://github.com/LemonMontage420/TORSION-Community-Edition.
