using UnityEngine;

namespace VehicleDynamics
{
    public class CustomJoints
    {
        public static ConfigurableJoint CreateRevoluteJoint(GameObject bodyA, GameObject bodyB, Vector3 anchor, Vector3 axis)
        {
            var joint = bodyA.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodyB.GetComponent<Rigidbody>();
            joint.anchor = bodyA.transform.InverseTransformPoint(anchor);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = bodyB.transform.InverseTransformPoint(anchor);

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.axis = axis.normalized;
            joint.secondaryAxis = Vector3.Cross(axis.normalized, Vector3.up).normalized;

            if (axis == Vector3.right)
            {
                joint.angularXMotion = ConfigurableJointMotion.Locked;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Locked;
            }
            else if (axis == Vector3.up)
            {
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Locked;
                joint.angularZMotion = ConfigurableJointMotion.Locked;
            }
            else if (axis == Vector3.forward)
            {
                joint.angularXMotion = ConfigurableJointMotion.Locked;
                joint.angularYMotion = ConfigurableJointMotion.Locked;
                joint.angularZMotion = ConfigurableJointMotion.Free;
            }
            else
            {
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;
            }

            return joint;
        }
        public static ConfigurableJoint CreateRevoluteJoint(GameObject bodyA, GameObject bodyB, Vector3 anchor, Vector3 axis, float angularLimit)
        {
            var joint = bodyA.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodyB.GetComponent<Rigidbody>();
            joint.anchor = bodyA.transform.InverseTransformPoint(anchor);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = bodyB.transform.InverseTransformPoint(anchor);

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.axis = axis.normalized;
            joint.secondaryAxis = Vector3.Cross(axis.normalized, Vector3.up).normalized;

            if (axis == Vector3.right)
            {
                joint.angularXMotion = ConfigurableJointMotion.Locked;
                joint.angularYMotion = ConfigurableJointMotion.Limited;
                joint.angularZMotion = ConfigurableJointMotion.Locked;
                joint.angularYLimit = new SoftJointLimit { limit = angularLimit };
            }
            else if (axis == Vector3.up)
            {
                joint.angularXMotion = ConfigurableJointMotion.Limited;
                joint.angularYMotion = ConfigurableJointMotion.Locked;
                joint.angularZMotion = ConfigurableJointMotion.Locked;
                joint.lowAngularXLimit = new SoftJointLimit { limit = -angularLimit };
                joint.highAngularXLimit = new SoftJointLimit { limit = angularLimit };
            }
            else if (axis == Vector3.forward)
            {
                joint.angularXMotion = ConfigurableJointMotion.Locked;
                joint.angularYMotion = ConfigurableJointMotion.Locked;
                joint.angularZMotion = ConfigurableJointMotion.Limited;
                joint.angularZLimit = new SoftJointLimit { limit = angularLimit };
            }
            else
            {
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularYMotion = ConfigurableJointMotion.Free;
                joint.angularZMotion = ConfigurableJointMotion.Free;
            }

            return joint;
        }
        public static ConfigurableJoint CreateSphereJoint(GameObject bodyA, GameObject bodyB, Vector3 anchor)
        {
            var joint = bodyA.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodyB.GetComponent<Rigidbody>();
            joint.anchor = bodyA.transform.InverseTransformPoint(anchor);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = bodyB.transform.InverseTransformPoint(anchor);

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;

            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            return joint;
        }
        public static ConfigurableJoint CreateSpringJoint(GameObject bodyA, GameObject bodyB, Vector3 chassisAnchor, Vector3 hubAnchor, float spring, float damper)
        {
            var joint = bodyA.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodyB.GetComponent<Rigidbody>();
            joint.anchor = bodyA.transform.InverseTransformPoint(chassisAnchor);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = bodyB.transform.InverseTransformPoint(hubAnchor);

            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;

            SoftJointLimitSpring limitSpring = new() { spring = 0, damper = 0 };
            joint.linearLimitSpring = limitSpring;

            JointDrive drive = new()
            {
                positionSpring = spring,
                positionDamper = damper,
                maximumForce = Mathf.Infinity
            };

            joint.xDrive = drive;
            joint.yDrive = drive;
            joint.zDrive = drive;

            joint.configuredInWorldSpace = false;

            // Set the axis of the spring joint based on the direction between chassis and hub anchors
            Vector3 springAxis = (hubAnchor - chassisAnchor).normalized;
            joint.axis = bodyA.transform.InverseTransformDirection(springAxis);

            // Set secondary axis
            joint.secondaryAxis = Vector3.Cross(joint.axis, Vector3.up).normalized;

            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;


            // Steering drive
            JointDrive angularDrive = new()
            {
                positionSpring = 20000f,
                positionDamper = 100f,
                maximumForce = Mathf.Infinity
            };
            joint.angularXDrive = angularDrive;

            return joint;
        }
        public static ConfigurableJoint CreateAxleJoint(GameObject bodyA, GameObject bodyB)
        {
            var joint = bodyA.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodyB.GetComponent<Rigidbody>();
            joint.anchor = Vector3.zero;
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = bodyB.transform.InverseTransformPoint(bodyA.transform.TransformPoint(Vector3.zero));

            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Limited;

            // Allow very tiny flex
            SoftJointLimit limit = new() { limit = 0.001f };
            joint.linearLimit = limit;

            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;

            joint.axis = Vector3.right;
            joint.secondaryAxis = Vector3.up;
            joint.configuredInWorldSpace = false;
            return joint;
        }
    }
}