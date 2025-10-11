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
            bool leafSpring = false
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

            strutJoint = CustomJoints.CreateSpringJoint(
                chassisBody.gameObject, hubBody.gameObject,
                chassisMountPos, hubMountPos,
                0, 0, springRestLength);
            // The spring and damper force is handled manually by this Strut class
            // instead of using the built-in PhysX spring and damper
            // This allows for more control and customization of the suspension behavior,
            // e.g. more complex damper, non-linear (progressive) spring, etc.
            if (leafSpring) LeafSpringSetup();
        }

        public void LeafSpringSetup()
        {
            strutJoint.yMotion = ConfigurableJointMotion.Locked;
            strutJoint.zMotion = ConfigurableJointMotion.Locked;
            strutJoint.angularYZDrive = new JointDrive { positionSpring = springStiffness, positionDamper = bumpStiffness, maximumForce = Mathf.Infinity };
        }

        public void Step()
        {
            springChassisAnchor = strutJoint.transform.TransformPoint(strutJoint.anchor);
            springHubAnchor = strutJoint.connectedBody.transform.TransformPoint(strutJoint.connectedAnchor);

            Vector3 strutVelocity = hubBody.GetPointVelocity(springHubAnchor) - chassisBody.GetPointVelocity(springChassisAnchor);
            float suspensionVelocity = Vector3.Dot(strutVelocity, springDirection);

            // Spring force
            float springDistance = Vector3.Distance(springChassisAnchor, springHubAnchor);
            compression = springDistance - springRestLength;
            float springForce = -compression * springStiffness;

            // Bump stop force
            if (Mathf.Abs(springDistance) < bumpStopLength)
            {
                Debug.Log("Bump stop engaged");
                float bumpStopCompression = springDistance - bumpStopLength;
                if (suspensionVelocity > 0f) // Bump
                {
                    springForce += -bumpStopCompression * bumpStopStiffness - suspensionVelocity * bumpStopBumpDamping;
                }
                else // Rebound
                {
                    springForce += -bumpStopCompression * bumpStopStiffness - suspensionVelocity * bumpStopReboundDamping;
                }
            }
        
            // Damper force
            float damperForce;
            if (suspensionVelocity > 0f) // Bump
            {
                if(suspensionVelocity > fastBumpThreshold)
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
                if(suspensionVelocity < -fastReboundThreshold)
                {
                    damperForce = suspensionVelocity * fastReboundStiffness;
                }
                else
                {
                    damperForce = suspensionVelocity * reboundStiffness;
                }
            }

            hubBody.AddForceAtPosition((-springForce - damperForce) * springDirection, hubBody.transform.position);
            chassisBody.AddForceAtPosition((springForce + damperForce) * springDirection, hubBody.transform.position);
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
    }
}