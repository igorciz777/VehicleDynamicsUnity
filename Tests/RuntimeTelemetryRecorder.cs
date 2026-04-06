using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace VehicleDynamics.TestsRuntime
{
    [Serializable]
    public class TelemetrySample
    {
        public float time;
        public float steering;
        public float throttle;
        public float brake;
        public float clutch;
        public float handbrake;

        public float speedMs;
        public float speedKmh;
        public float engineRpm;
        public int gear;
        public float clutchTorque;

        public float maxAbsSlipRatio;
        public float maxAbsSlipAngleDeg;
        public float avgNormalLoad;

        public float frontLeftCompression;
        public float frontRightCompression;
        public float rearLeftCompression;
        public float rearRightCompression;

        public float frontAntiRollForce;
        public float rearAntiRollForce;

        public float tireAlignTorque;
        public float steeringArmTorque;

        public Vector3 position;
    }

    public class RuntimeTelemetryRecorder : MonoBehaviour
    {
        [Header("References")]
        public VehicleModel vehicleModel;

        private readonly List<TelemetrySample> samples = new(4096);
        private readonly List<Hub> hubs = new(8);

        public IReadOnlyList<TelemetrySample> Samples => samples;
        public TelemetrySample LatestSample => samples.Count > 0 ? samples[samples.Count - 1] : null;

        public void BeginRun()
        {
            samples.Clear();
            CacheHubs();
        }

        public void Capture(float time)
        {
            if (vehicleModel == null || vehicleModel.drivetrain == null)
            {
                return;
            }

            float maxAbsSlipRatio = 0f;
            float maxAbsSlipAngleDeg = 0f;
            float normalLoadSum = 0f;
            int groundedCount = 0;

            for (int i = 0; i < hubs.Count; i++)
            {
                Wheel wheel = hubs[i].GetWheel();
                if (wheel == null)
                {
                    continue;
                }

                maxAbsSlipRatio = Mathf.Max(maxAbsSlipRatio, Mathf.Abs(wheel.slipRatio));
                maxAbsSlipAngleDeg = Mathf.Max(maxAbsSlipAngleDeg, Mathf.Abs(wheel.slipAngle * Mathf.Rad2Deg));

                if (wheel.isGrounded)
                {
                    normalLoadSum += wheel.getNormalLoad();
                    groundedCount++;
                }
            }

            (float flComp, float frComp, float rlComp, float rrComp) = ReadSuspensionCompressions();
            (float frontArb, float rearArb) = ReadAntiRollForces();

            TelemetrySample sample = new()
            {
                time = time,
                steering = vehicleModel.steeringInput,
                throttle = vehicleModel.throttleInput,
                brake = vehicleModel.brakeInput,
                clutch = vehicleModel.clutchInput,
                handbrake = vehicleModel.handbrakeInput,
                speedMs = vehicleModel.vehicleRigidbody != null ? vehicleModel.vehicleRigidbody.linearVelocity.magnitude : 0f,
                speedKmh = vehicleModel.vehicleRigidbody != null ? vehicleModel.vehicleRigidbody.linearVelocity.magnitude * 3.6f : 0f,
                engineRpm = vehicleModel.drivetrain.engine != null ? vehicleModel.drivetrain.engine.engineRpm : 0f,
                gear = vehicleModel.drivetrain.transmission != null ? vehicleModel.drivetrain.transmission.currentGear : 0,
                clutchTorque = vehicleModel.drivetrain.clutch != null ? vehicleModel.drivetrain.clutch.clutchTorque : 0f,
                maxAbsSlipRatio = maxAbsSlipRatio,
                maxAbsSlipAngleDeg = maxAbsSlipAngleDeg,
                avgNormalLoad = groundedCount > 0 ? normalLoadSum / groundedCount : 0f,
                frontLeftCompression = flComp,
                frontRightCompression = frComp,
                rearLeftCompression = rlComp,
                rearRightCompression = rrComp,
                frontAntiRollForce = frontArb,
                rearAntiRollForce = rearArb,
                tireAlignTorque = vehicleModel.tireAlignTorque,
                steeringArmTorque = vehicleModel.steeringArmTorque,
                position = vehicleModel.transform.position,
            };

            samples.Add(sample);
        }

        public string WriteCsv(string directoryPath, string fileName)
        {
            Directory.CreateDirectory(directoryPath);
            string path = Path.Combine(directoryPath, fileName);

            StringBuilder sb = new(64 * 1024);
            sb.AppendLine("time,steering,throttle,brake,clutch,handbrake,speed_ms,speed_kmh,engine_rpm,gear,clutch_torque,max_abs_slip_ratio,max_abs_slip_angle_deg,avg_normal_load,front_left_compression,front_right_compression,rear_left_compression,rear_right_compression,front_antiroll_force,rear_antiroll_force,tire_align_torque,steering_arm_torque,pos_x,pos_y,pos_z");

            for (int i = 0; i < samples.Count; i++)
            {
                TelemetrySample s = samples[i];
                sb
                    .Append(Fmt(s.time)).Append(',')
                    .Append(Fmt(s.steering)).Append(',')
                    .Append(Fmt(s.throttle)).Append(',')
                    .Append(Fmt(s.brake)).Append(',')
                    .Append(Fmt(s.clutch)).Append(',')
                    .Append(Fmt(s.handbrake)).Append(',')
                    .Append(Fmt(s.speedMs)).Append(',')
                    .Append(Fmt(s.speedKmh)).Append(',')
                    .Append(Fmt(s.engineRpm)).Append(',')
                    .Append(s.gear).Append(',')
                    .Append(Fmt(s.clutchTorque)).Append(',')
                    .Append(Fmt(s.maxAbsSlipRatio)).Append(',')
                    .Append(Fmt(s.maxAbsSlipAngleDeg)).Append(',')
                    .Append(Fmt(s.avgNormalLoad)).Append(',')
                    .Append(Fmt(s.frontLeftCompression)).Append(',')
                    .Append(Fmt(s.frontRightCompression)).Append(',')
                    .Append(Fmt(s.rearLeftCompression)).Append(',')
                    .Append(Fmt(s.rearRightCompression)).Append(',')
                    .Append(Fmt(s.frontAntiRollForce)).Append(',')
                    .Append(Fmt(s.rearAntiRollForce)).Append(',')
                    .Append(Fmt(s.tireAlignTorque)).Append(',')
                    .Append(Fmt(s.steeringArmTorque)).Append(',')
                    .Append(Fmt(s.position.x)).Append(',')
                    .Append(Fmt(s.position.y)).Append(',')
                    .Append(Fmt(s.position.z))
                    .AppendLine();
            }

            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private void CacheHubs()
        {
            hubs.Clear();
            if (vehicleModel == null)
            {
                return;
            }

            Hub[] detected = vehicleModel.GetComponentsInChildren<Hub>(true);
            for (int i = 0; i < detected.Length; i++)
            {
                hubs.Add(detected[i]);
            }
        }

        private (float, float, float, float) ReadSuspensionCompressions()
        {
            float fl = 0f;
            float fr = 0f;
            float rl = 0f;
            float rr = 0f;

            if (vehicleModel == null || vehicleModel.carSuspension == null)
            {
                return (fl, fr, rl, rr);
            }

            for (int i = 0; i < vehicleModel.carSuspension.Length; i++)
            {
                Suspension suspension = vehicleModel.carSuspension[i];
                if (suspension == null)
                {
                    continue;
                }

                (Strut left, Strut right) = suspension.GetStruts();
                float leftCompression = left != null ? left.GetSpringCompression() : 0f;
                float rightCompression = right != null ? right.GetSpringCompression() : 0f;

                float localZ = vehicleModel.transform.InverseTransformPoint(suspension.transform.position).z;
                bool isFront = localZ >= 0f;
                if (isFront)
                {
                    fl = leftCompression;
                    fr = rightCompression;
                }
                else
                {
                    rl = leftCompression;
                    rr = rightCompression;
                }
            }

            return (fl, fr, rl, rr);
        }

        private (float, float) ReadAntiRollForces()
        {
            float front = 0f;
            float rear = 0f;

            if (vehicleModel == null || vehicleModel.carSuspension == null)
            {
                return (front, rear);
            }

            for (int i = 0; i < vehicleModel.carSuspension.Length; i++)
            {
                Suspension suspension = vehicleModel.carSuspension[i];
                if (suspension == null)
                {
                    continue;
                }

                float localZ = vehicleModel.transform.InverseTransformPoint(suspension.transform.position).z;
                bool isFront = localZ >= 0f;
                if (isFront)
                {
                    front = suspension.antirollForce;
                }
                else
                {
                    rear = suspension.antirollForce;
                }
            }

            return (front, rear);
        }

        private static string Fmt(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }
    }
}
