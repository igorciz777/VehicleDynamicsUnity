using System.Collections;
using System.Collections.Generic;
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
        public static ConfigurableJoint CreateUniversalJoint(GameObject bodyA, GameObject bodyB, Vector3 anchor)
        {
            var joint = bodyA.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodyB.GetComponent<Rigidbody>();
            joint.anchor = bodyA.transform.InverseTransformPoint(anchor);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = bodyB.transform.InverseTransformPoint(anchor);

            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;

            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

            return joint;
        }
        public static ConfigurableJoint CreateSpringJoint(GameObject bodyA, GameObject bodyB, Vector3 chassisAnchor, Vector3 hubAnchor, float spring, float damper, float springLength)
        {
            var joint = bodyA.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodyB.GetComponent<Rigidbody>();
            joint.anchor = bodyA.transform.InverseTransformPoint(chassisAnchor);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = bodyB.transform.InverseTransformPoint(hubAnchor);

            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;

            SoftJointLimitSpring limitSpring = new SoftJointLimitSpring { spring = 0, damper = 0 };
            joint.linearLimitSpring = limitSpring;

            JointDrive drive = new JointDrive
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

            joint.targetPosition = new(springLength, 0, 0);

            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;

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
            SoftJointLimit limit = new SoftJointLimit { limit = 0.001f };
            joint.linearLimit = limit;

            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;

            joint.axis = Vector3.right;
            joint.secondaryAxis = Vector3.up;
            joint.configuredInWorldSpace = false;
            return joint;
        }
        public static ConfigurableJoint CreateTranslationalJoint(GameObject bodyA, GameObject bodyB, Vector3 anchor, Vector3 axis, float limit = 0.1f)
        {
            var joint = bodyA.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodyB.GetComponent<Rigidbody>();
            joint.anchor = bodyA.transform.InverseTransformPoint(anchor);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = bodyB.transform.InverseTransformPoint(anchor);
            if (axis == Vector3.right)
            {
                joint.xMotion = ConfigurableJointMotion.Limited;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Locked;
            }
            else if (axis == Vector3.up)
            {
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Limited;
                joint.zMotion = ConfigurableJointMotion.Locked;
            }
            else if (axis == Vector3.forward)
            {
                joint.xMotion = ConfigurableJointMotion.Locked;
                joint.yMotion = ConfigurableJointMotion.Locked;
                joint.zMotion = ConfigurableJointMotion.Limited;
            }

            joint.linearLimit = new SoftJointLimit { limit = limit };

            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;

            joint.axis = axis.normalized;
            joint.secondaryAxis = Vector3.Cross(axis.normalized, Vector3.up).normalized;
            joint.configuredInWorldSpace = false;
            return joint;
        }
    }
}