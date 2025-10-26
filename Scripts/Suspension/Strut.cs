using UnityEngine;

namespace VehicleDynamics
{
    public class Strut
    {
        private readonly Rigidbody chassisBody;
        private readonly Rigidbody hubBody;
        private readonly ConfigurableJoint strutJoint;
        private float springRestLength;
        private float springStiffness;
        private float bumpStopLength;
        private float bumpStopStiffness;
        private float bumpStopBumpDamping;
        private float bumpStopReboundDamping;
        private float bumpStiffness;
        private float reboundStiffness;
        private float fastBumpStiffness;
        private float fastReboundStiffness;
        private float fastBumpThreshold;
        private float fastReboundThreshold;
        private Vector3 springChassisAnchor;
        private Vector3 springHubAnchor;
        private Vector3 springDirection;
        private readonly float maxForce;

        private float compression;

        public Strut(
            Rigidbody chassisBody,
            Rigidbody hubBody,
            Vector3 chassisMountPos,
            Vector3 hubMountPos,
            float springStiffness,
            float springRestLength,
            float bumpStopLength,
            float bumpStopStiffness,
            float bumpStopBumpDamping,
            float bumpStopReboundDamping,
            float bumpStiffness,
            float reboundStiffness,
            float fastBumpStiffness,
            float fastReboundStiffness,
            float fastBumpThreshold,
            float fastReboundThreshold,
            bool solidAxleSpring = false
            )
        {
            this.chassisBody = chassisBody;
            this.hubBody = hubBody;
            this.springStiffness = springStiffness;
            this.bumpStopLength = bumpStopLength;
            this.bumpStopStiffness = bumpStopStiffness;
            this.bumpStopBumpDamping = bumpStopBumpDamping;
            this.bumpStopReboundDamping = bumpStopReboundDamping;
            this.bumpStiffness = bumpStiffness;
            this.reboundStiffness = reboundStiffness;
            this.fastBumpStiffness = fastBumpStiffness;
            this.fastReboundStiffness = fastReboundStiffness;
            this.fastBumpThreshold = fastBumpThreshold;
            this.fastReboundThreshold = fastReboundThreshold;
            this.springRestLength = springRestLength;

            springDirection = -(hubMountPos - chassisMountPos).normalized;
            // springDirection = hubBody.transform.up;

            // Max force
            maxForce = chassisBody.mass * Mathf.Abs(Physics.gravity.y);

            strutJoint = CustomJoints.CreateSpringJoint(
                chassisBody.gameObject, hubBody.gameObject,
                chassisMountPos, hubMountPos,
                0, 0, springRestLength);
            // The spring and damper force is handled manually by this Strut class
            // instead of using the built-in PhysX spring and damper
            // This allows for more control and customization of the suspension behavior,
            // e.g. more complex damper, non-linear (progressive) spring, etc.
            if (solidAxleSpring) SolidAxleSetup();
        }

        public void SolidAxleSetup()
        {
            strutJoint.yMotion = ConfigurableJointMotion.Locked;
            strutJoint.zMotion = ConfigurableJointMotion.Locked;
            strutJoint.angularXMotion = ConfigurableJointMotion.Locked;
        }

        public void Step()
        {
            springChassisAnchor = strutJoint.transform.TransformPoint(strutJoint.anchor);
            springHubAnchor = strutJoint.connectedBody.transform.TransformPoint(strutJoint.connectedAnchor);

            springDirection = -(springHubAnchor - springChassisAnchor).normalized;

            Vector3 strutVelocity = hubBody.GetPointVelocity(springHubAnchor) - chassisBody.GetPointVelocity(springChassisAnchor);
            float suspensionVelocity = Vector3.Dot(strutVelocity, springDirection);

            // Spring force
            float springDistance = Vector3.Distance(springChassisAnchor, springHubAnchor);
            compression = springDistance - springRestLength;
            float springForce = -compression * springStiffness;

            // Damper force
            float damperForce;
            if (suspensionVelocity > 0f) // Bump
            {
                if (suspensionVelocity > fastBumpThreshold)
                {
                    damperForce = suspensionVelocity * fastBumpStiffness;
                }
                else
                {
                    damperForce = suspensionVelocity * bumpStiffness;
                }
            }
            else // Rebound
            {
                if (suspensionVelocity < -fastReboundThreshold)
                {
                    damperForce = suspensionVelocity * fastReboundStiffness;
                }
                else
                {
                    damperForce = suspensionVelocity * reboundStiffness;
                }
            }

            // Bump stop force
            if (Mathf.Abs(springDistance) < bumpStopLength)
            {
                float bumpStopCompression = springDistance - bumpStopLength;
                if (suspensionVelocity > 0f) // Bump
                {
                    springForce += -bumpStopCompression * bumpStopStiffness;
                    damperForce += suspensionVelocity * bumpStopBumpDamping;
                }
                else // Rebound
                {
                    springForce += -bumpStopCompression * bumpStopStiffness;
                    damperForce += suspensionVelocity * bumpStopReboundDamping;
                }
            }

            // Clamp forces to maxForce
            springForce = Mathf.Clamp(springForce, -maxForce, maxForce);
            damperForce = Mathf.Clamp(damperForce, -maxForce, maxForce);
            // Apply forces
            hubBody.AddForceAtPosition((-springForce - damperForce) * springDirection, springHubAnchor, ForceMode.Force);
            chassisBody.AddForceAtPosition((springForce + damperForce) * springDirection, springChassisAnchor, ForceMode.Force);
        }
        // Getters
        public Vector3 GetSpringChassisAnchor()
        {
            return springChassisAnchor;
        }
        public Vector3 GetSpringHubAnchor()
        {
            return springHubAnchor;
        }
        public Vector3 GetSpringDirection()
        {
            return springDirection;
        }
        public float GetCompression()
        {
            return compression;
        }
        public ConfigurableJoint GetJoint()
        {
            return strutJoint;
        }
        // Setters
        public void SetSpringParameters(float newSpringConstant, float newSpringRestLength, float newBumpStopLength, float newBumpStopStiffness, float newBumpStopBumpDamping, float newBumpStopReboundDamping)
        {
            springStiffness = newSpringConstant;
            springRestLength = newSpringRestLength;
            bumpStopLength = newBumpStopLength;
            bumpStopStiffness = newBumpStopStiffness;
            bumpStopBumpDamping = newBumpStopBumpDamping;
            bumpStopReboundDamping = newBumpStopReboundDamping;
        }
        public void SetDamperParameters(float newBumpStiffness, float newReboundStiffness, float newFastBumpStiffness, float newFastReboundStiffness, float newFastBumpThreshold, float newFastReboundThreshold)
        {
            bumpStiffness = newBumpStiffness;
            reboundStiffness = newReboundStiffness;
            fastBumpStiffness = newFastBumpStiffness;
            fastReboundStiffness = newFastReboundStiffness;
            fastBumpThreshold = newFastBumpThreshold;
            fastReboundThreshold = newFastReboundThreshold;
        }
        public void SetSteeringAngle(float angleDegrees)
        {
            strutJoint.targetRotation = Quaternion.Euler(-angleDegrees, 0f, 0f);
        }
    }
}