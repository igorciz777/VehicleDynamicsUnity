using UnityEngine;

namespace VehicleDynamics
{
    public class Strut
    {
        private readonly Rigidbody chassisBody;
        private readonly Rigidbody hubBody;
        private readonly ConfigurableJoint strutJoint;
        // Spring
        private float springLength;
        private float springStiffness;
        // Damper
        private float bumpStiffness;
        private float reboundStiffness;
        private float fastBumpStiffness;
        private float fastReboundStiffness;
        private float fastBumpThreshold;
        private float fastReboundThreshold;
        // Bump Stop
        private float bumpStopLength;
        private float bumpStopStiffness;
        private float bumpStopBumpDamping;
        private float bumpStopReboundDamping;
        // Mounts
        private Vector3 strutChassisAnchor;
        private Vector3 strutHubAnchor;
        // Internal
        private Vector3 strutDirection;
        private Vector3 springChassisAnchor;
        private Vector3 springHubAnchor;
        private readonly float maxForce;
        private float springCompression;
        private float hubToSpringAnchorDistance;
        private const float damperDeadzone = 0.01f;
        private const float lowSpeedDamping = 1000f;

        public Strut(
            Rigidbody chassisBody,
            Rigidbody hubBody,
            Vector3 chassisMountPos,
            Vector3 hubMountPos,

            float springLength,
            float springStiffness,

            float bumpStiffness,
            float reboundStiffness,
            float fastBumpStiffness,
            float fastReboundStiffness,
            float fastBumpThreshold,
            float fastReboundThreshold,

            float bumpStopLength,
            float bumpStopStiffness,
            float bumpStopBumpDamping,
            float bumpStopReboundDamping,

            bool solidAxleSpring = false
            )
        {
            this.chassisBody = chassisBody;
            this.hubBody = hubBody;
            this.springLength = springLength;
            this.springStiffness = springStiffness;
            this.bumpStiffness = bumpStiffness;
            this.reboundStiffness = reboundStiffness;
            this.fastBumpStiffness = fastBumpStiffness;
            this.fastReboundStiffness = fastReboundStiffness;
            this.fastBumpThreshold = fastBumpThreshold;
            this.fastReboundThreshold = fastReboundThreshold;
            this.bumpStopLength = bumpStopLength;
            this.bumpStopStiffness = bumpStopStiffness;
            this.bumpStopBumpDamping = bumpStopBumpDamping;
            this.bumpStopReboundDamping = bumpStopReboundDamping;
            

            strutDirection = -(hubMountPos - chassisMountPos).normalized;

            // Max force
            maxForce = chassisBody.mass * Mathf.Abs(Physics.gravity.y);

            strutJoint = CustomJoints.CreateSpringJoint(
                chassisBody.gameObject, hubBody.gameObject,
                chassisMountPos, hubMountPos,
                0, 0);
            // The spring and damper force is handled manually by this Strut class
            // instead of using the built-in PhysX spring and damper
            // This allows for more control and customization of the suspension behavior,
            // e.g. more complex damper, non-linear (progressive) spring, etc.
            if (solidAxleSpring) SolidAxleSetup();

            hubToSpringAnchorDistance = Vector3.Distance(hubMountPos, chassisMountPos) - springLength;
        }

        public void SolidAxleSetup()
        {
            strutJoint.yMotion = ConfigurableJointMotion.Locked;
            strutJoint.zMotion = ConfigurableJointMotion.Locked;
            strutJoint.angularXMotion = ConfigurableJointMotion.Locked;
        }

        public void Step(float motionRatio = 1f)
        {
            strutChassisAnchor = strutJoint.transform.TransformPoint(strutJoint.anchor);
            strutHubAnchor = strutJoint.connectedBody.transform.TransformPoint(strutJoint.connectedAnchor);
            strutDirection = -(strutHubAnchor - strutChassisAnchor).normalized;

            springChassisAnchor = strutChassisAnchor - strutDirection * bumpStopLength;
            springHubAnchor = strutHubAnchor + strutDirection * hubToSpringAnchorDistance;

            Vector3 damperWorldVelocity = hubBody.GetPointVelocity(strutHubAnchor) - chassisBody.GetPointVelocity(strutChassisAnchor);
            float damperVelocity = Vector3.Dot(damperWorldVelocity, strutDirection);

            // Spring force
            float springDistance = Vector3.Distance(springHubAnchor, springChassisAnchor);
            springCompression = springDistance - springLength;
            float springForce = -springCompression * springStiffness;

            // Damper force
            float damperForce;
            float absVel = Mathf.Abs(damperVelocity);
            if (absVel < damperDeadzone)
            {
                damperForce = damperVelocity * lowSpeedDamping;
            }
            else if (damperVelocity > 0f) // Bump
            {
                if (damperVelocity > fastBumpThreshold)
                {
                    damperForce = damperVelocity * fastBumpStiffness;
                }
                else
                {
                    damperForce = damperVelocity * bumpStiffness;
                }
            }
            else // Rebound
            {
                if (damperVelocity < -fastReboundThreshold)
                {
                    damperForce = damperVelocity * fastReboundStiffness;
                }
                else
                {
                    damperForce = damperVelocity * reboundStiffness;
                }
            }

            // Bump stop force
            if (Vector3.Distance(springHubAnchor, strutChassisAnchor) < bumpStopLength)
            {
                float bumpStopCompression = springDistance - bumpStopLength;
                if (damperVelocity > 0f) // Bump
                {
                    springForce += -bumpStopCompression * bumpStopStiffness;
                    damperForce += damperVelocity * bumpStopBumpDamping;
                }
                else // Rebound
                {
                    springForce += -bumpStopCompression * bumpStopStiffness;
                    damperForce += damperVelocity * bumpStopReboundDamping;
                }
            }

            // Clamp forces to maxForce
            springForce = Mathf.Clamp(springForce * motionRatio, -maxForce, maxForce);
            damperForce = Mathf.Clamp(damperForce * motionRatio, -maxForce, maxForce);
            // Apply forces
            hubBody.AddForceAtPosition((-springForce - damperForce) * strutDirection, strutHubAnchor, ForceMode.Force);
            chassisBody.AddForceAtPosition((springForce + damperForce) * strutDirection, strutChassisAnchor, ForceMode.Force);
        }
        // Getters
        public Vector3 GetStrutChassisAnchor()
        {
            return strutChassisAnchor;
        }
        public Vector3 GetStrutHubAnchor()
        {
            return strutHubAnchor;
        }
        public Vector3 GetSpringChassisAnchor()
        {
            return springChassisAnchor;
        }
        public Vector3 GetSpringHubAnchor()
        {
            return springHubAnchor;
        }
        public Vector3 GetStrutDirection()
        {
            return strutDirection;
        }
        public float GetSpringCompression()
        {
            return springCompression;
        }
        public ConfigurableJoint GetStrutJoint()
        {
            return strutJoint;
        }
        // Setters
        public void SetSpringParameters(float newSpringLength, float newSpringStiffness)
        {
            springLength = newSpringLength;
            springStiffness = newSpringStiffness;
            hubToSpringAnchorDistance = Vector3.Distance(strutHubAnchor, strutChassisAnchor) - springLength;
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
        public void SetBumpStopParameters(float newBumpStopLength, float newBumpStopStiffness, float newBumpStopBumpDamping, float newBumpStopReboundDamping)
        {
            bumpStopLength = newBumpStopLength;
            bumpStopStiffness = newBumpStopStiffness;
            bumpStopBumpDamping = newBumpStopBumpDamping;
            bumpStopReboundDamping = newBumpStopReboundDamping;
        }
        public void SetSteeringAngle(float angleDegrees)
        {
            strutJoint.targetRotation = Quaternion.Euler(-angleDegrees, 0f, 0f);
        }
    }
}