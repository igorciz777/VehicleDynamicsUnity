using Unity.Mathematics;
using UnityEngine;

namespace VehicleDynamics
{
    public class TireInput
    {
        public float kap; // slip ratio [-]
        public float alph; // slip angle [rad]
        public float Fz; // normal vertical load [N]
        public float gam; // camber/inclination angle [rad]
        public float Fz0; // nominal load [N]
        public float r0; // unloaded wheel radius [m]
        public float rE; // effective rolling radius [m]
        public float rollCoeff; // rolling resistance coefficient [-]
        public Vector2 mu; // friction coefficient (longitudinal, lateral) [-]
        public Vector2 compMu; // composite friction coeff
        public Vector2 degMu; // degressive friction coeff

        public TireInput()
        {
            kap = 0f;
            alph = 0f;
            Fz = 0f;
            gam = 0f;
            Fz0 = 0f;
            r0 = 0f;
            rE = 0f;
            rollCoeff = 0f;
            mu = Vector2.zero;
            compMu = Vector2.zero;
            degMu = Vector2.zero;
        }

        public TireInput(float slipRatio, float slipAngle, float normalLoad, float camberAngle, float nominalLoad, float unloadedRadius, float effectiveRadius, float rollingResistanceCoefficient, Vector2 frictionCoefficient, Vector2 compositeFriction, Vector2 degressiveFriction)
        {
            kap = slipRatio;
            alph = slipAngle;
            Fz = normalLoad;
            gam = camberAngle;
            Fz0 = nominalLoad;
            r0 = unloadedRadius;
            rE = effectiveRadius;
            rollCoeff = rollingResistanceCoefficient;
            mu = frictionCoefficient;
            compMu = compositeFriction;
            degMu = degressiveFriction;
        }

        public void UpdateTireInput(float slipRatio, float slipAngle, float normalLoad, float camberAngle, float nominalLoad, float unloadedRadius, float effectiveRadius, float rollingResistanceCoefficient, Vector2 frictionCoefficient, Vector2 compositeFriction, Vector2 degressiveFriction)
        {
            kap = slipRatio;
            alph = slipAngle;
            Fz = normalLoad;
            gam = camberAngle;
            Fz0 = nominalLoad;
            r0 = unloadedRadius;
            rE = effectiveRadius;
            rollCoeff = rollingResistanceCoefficient;
            mu = frictionCoefficient;
            compMu = compositeFriction;
            degMu = degressiveFriction;
        }
    }
    public abstract class Tire
    {
        public TireInput tr;
        public abstract float UpdateSharedParams();
        public abstract float GetPureLongitudinal();
        public abstract float GetPureLateral();
        public abstract float GetPureAligningTorque();
        public abstract float GetCombinedLongitudinal();
        public abstract float GetCombinedLateral();
        public abstract float GetCombinedAligningTorque();
        public abstract float GetRollingResistanceMoment();
        public abstract float GetOverturningMoment();
        public Vector4 GetForcesAndTorque(ref TireInput input)
        {
            tr = input;
            UpdateSharedParams();
            float Fx = GetCombinedLongitudinal();
            float Fy = GetCombinedLateral();
            float Mz = GetCombinedAligningTorque();

            return new Vector4(Fx, 0f, Fy, Mz);
        }
    }
    public class MagicFormulaSimplified : Tire
    {
        // Simplified Pacejka's Magic Formula
        private readonly MFSimpleTireObject td;
        public MagicFormulaSimplified(MFSimpleTireObject tireData)
        {
            td = tireData;
        }
        public override float UpdateSharedParams()
        {
            return 0f;
        }

        public override float GetPureLongitudinal()
        {
            if (tr.Fz <= 0) return 0f;

            float mu = tr.compMu.x;
            float Fz = tr.Fz;

            float Fx = mu * Fz * td.D * Mathf.Sin(td.C_Long * Mathf.Atan(td.B * tr.kap - td.E * (td.B * tr.kap - Mathf.Atan(td.B * tr.kap))));
            return Fx;
        }
        public override float GetPureLateral()
        {
            if (tr.Fz <= 0) return 0f;

            float mu = tr.compMu.y;
            float Fz = tr.Fz;

            float Fy = mu * Fz * td.D * Mathf.Sin(td.C_Lat * Mathf.Atan(td.B * tr.alph - td.E * (td.B * tr.alph - Mathf.Atan(td.B * tr.alph))));
            return Fy;
        }
        public override float GetPureAligningTorque()
        {
            // No aligning torque in simplified model
            return 0f;
        }
        public override float GetCombinedLongitudinal()
        {
            return GetPureLongitudinal();
        }
        public override float GetCombinedLateral()
        {
            return GetPureLateral();
        }
        public override float GetCombinedAligningTorque()
        {
            return GetPureAligningTorque();
        }
        public override float GetRollingResistanceMoment()
        {
            float My = tr.r0 * -tr.Fz * tr.rollCoeff;
            return My;
        }
        public override float GetOverturningMoment()
        {
            // No overturning moment in simplified model
            return 0f;
        }
    }
    public class MagicFormula : Tire
    {
        // Pacejka's Magic Formula
        private readonly MFTireObject td;

        // Referenced calculated coefficients
        public float Kx, mux; // From pure longitudinal
        public float Fx; // From combined longitudinal
        public float Svy, Shy, Ky, By, Cy, Fy0, muy; // From lateral
        public float Svx, Shx; // From longitudinal
        public float Fy, Svyk; // From combined lateral
        public float Bt, Ct, Dt, Et, Br, Cr, Dr, alph_t, alph_r; // From aligning torque

        // Shared coefficients
        public float Fz0_prim; // Nominal load
        public float dfz; // Fz deviation
        public MagicFormula(MFTireObject tireData)
        {
            td = tireData;
        }
        public override float UpdateSharedParams()
        {
            Fz0_prim = tr.Fz0; // Nominal load
            dfz = (tr.Fz - Fz0_prim) / (Fz0_prim + Mathf.Epsilon); // Fz deviation
            return 0f;
        }
        public override float GetPureLongitudinal()
        {
            Svx = tr.Fz * (td.pVx1 + td.pVx2 * dfz) * tr.degMu.x; // Fx Vertical shift
            Shx = td.pHx1 + td.pHx2 * dfz; // Fx Horizontal shift

            mux = (td.pDx1 + td.pDx2 * dfz) * (1 - td.pDx3 * tr.gam * tr.gam) * tr.compMu.x;

            Kx = tr.Fz * (td.pKx1 + td.pKx2 * dfz) * Mathf.Exp(td.pKx3 * dfz); // Slip stiffness
            float Cx = td.pCx1; // Shape factor
            float Dx = mux * tr.Fz; // Peak value
            float Bx = Kx / (Cx * Dx + Mathf.Epsilon); // Stiffness factor
            float Ex = (td.pEx1 + td.pEx2 * dfz + td.pEx3 * dfz * dfz) * (1 - td.pEx4 * Mathf.Sign(tr.kap)); // Curvature factor
            if (Ex > 1f) Ex = 1f;

            float kapx = tr.kap + Shx; // Shifted slip ratio
            float Fx0 = Dx * Mathf.Sin(Cx * Mathf.Atan(Bx * kapx - Ex * (Bx * kapx - Mathf.Atan(Bx * kapx)))) + Svx; // Pure longitudinal force

            return Fx0;
        }
        public override float GetPureLateral()
        {
            float gam_y = tr.gam; // Inclination angle for lateral forces
            float gam_y_s = Mathf.Sin(gam_y); // Camber spin

            Svy = tr.Fz * (td.pVy1 + td.pVy2 * dfz + (td.pVy3 + td.pVy4 * dfz) * gam_y_s) * tr.degMu.y; // Fy vertical shift
            Shy = td.pHy1 + td.pHy2 * dfz + (td.pHy3 * gam_y_s); // Fy horizontal shift

            float alph_y = tr.alph + Shy; // Shifted slip angle

            muy = (td.pDy1 + td.pDy2 * dfz) * (1 - td.pDy3 * gam_y_s * gam_y_s) * tr.compMu.y; // Friction coefficient

            float Ky0 = td.pKy1 * Fz0_prim * Mathf.Sin(2f * Mathf.Atan(tr.Fz / (td.pKy2 * Fz0_prim))); // Cornering stiffness at Fznom
            Ky = Ky0 * (1 - td.pKy3 * Mathf.Abs(gam_y_s)); // Cornering stiffness
            Cy = td.pCy1; // Shape factor
            float Dy = muy * tr.Fz; // Peak value
            By = Ky / (Cy * Dy + Mathf.Epsilon); // Stiffness factor
            float Ey = (td.pEy1 + td.pEy2 * dfz) * (1 - (td.pEy3 + td.pEy4 * gam_y_s) * Mathf.Sign(alph_y)); // Curvature factor
            if (Ey > 1f) Ey = 1f;

            Fy0 = Dy * Mathf.Sin(Cy * Mathf.Atan(By * alph_y - Ey * (By * alph_y - Mathf.Atan(By * alph_y)))) + Svy; // Pure lateral force

            return Fy0;
        }
        public override float GetPureAligningTorque()
        {
            float gam_z = tr.gam; // Inclination angle for aligning torque
            float gam_z_s = Mathf.Sin(gam_z); // Camber spin

            float Shf = Shy + Svy / (Ky + Mathf.Epsilon); // Horizontal shift of pneumatic trail
            float Sht = td.qHz1 + td.qHz2 * dfz + (td.qHz3 + td.qHz4 * dfz) * gam_z_s; // Horizontal shift of pneumatic trail

            alph_t = tr.alph + Sht; // Slip angle for pneumatic trail
            alph_r = tr.alph + Shf; // Slip angle for residual moment

            Cr = 1f; // Shape factor for residual moment
            Dr = tr.Fz * (td.qDz6 + td.qDz7 * dfz + (td.qDz8 + td.qDz9 * dfz) * gam_z_s) * tr.r0 * tr.compMu.y; // Peak value of residual moment
            Br = td.qBz9 * (1f / tr.compMu.y) + td.qBz10 * By * Cy; // Slope factor of residual moment

            Ct = td.qCz1;
            Bt = (td.qBz1 + td.qBz2 * dfz + td.qBz3 * dfz * dfz) * (1 + td.qBz4 * gam_z_s + td.qBz5 * Mathf.Abs(gam_z_s)) * 1f / tr.compMu.y; // Slope factor of pneumatic trail
            Dt = tr.Fz * (td.qDz1 + td.qDz2 * dfz) * (1 + td.qDz3 * gam_z_s + td.qDz4 * gam_z_s * gam_z_s) * (tr.r0 / Fz0_prim);
            Et = (td.qEz1 + td.qEz2 * dfz + td.qEz3 * dfz * dfz) * (1 + (td.qEz4 + td.qEz5 * gam_z_s) * (2f / Mathf.PI * Mathf.Atan(Bt * Ct * alph_t)));
            if (Et > 1f) Et = 1f;

            float Mzr = Dr * Mathf.Cos(Cr * Mathf.Atan(Br * alph_r)) * Mathf.Cos(tr.alph); // Residual moment
            float t = Dt * Mathf.Cos(Ct * Mathf.Atan(Bt * alph_t - Et * (Bt * alph_t - Mathf.Atan(Bt * alph_t)))) * Mathf.Cos(tr.alph); // Pneumatic trail

            float Mz0 = -t * Fy0 + Mzr; // Pure aligning torque

            return Mathf.Clamp(Mz0, -tr.Fz * tr.r0, tr.Fz * tr.r0);
        }
        public override float GetCombinedLongitudinal()
        {
            float Fx0 = GetPureLongitudinal();

            float Cxa = td.rCx1; // Shape factor for combined slip
            float Bxa = td.rBx1 * Mathf.Cos(Mathf.Atan(td.rBx2 * tr.kap)); // Slope factor for combined slip
            float Exa = td.rEx1 + td.rEx2 * dfz; // Curvature factor for combined slip
            if (Exa > 1f) Exa = 1f;

            float Shxa = td.rHx1; // Horizontal shift for combined slip

            float alph_s = tr.alph + Shxa; // Shifted slip angle for combined slip

            float Dxa = Fx0 / (Mathf.Cos(Cxa * Mathf.Atan(Bxa * Shxa - Exa * (Bxa * Shxa - Mathf.Atan(Bxa * Shxa)))) + Mathf.Epsilon); // Combined slip peak value

            Fx = Dxa * Mathf.Cos(Cxa * Mathf.Atan(Bxa * alph_s - Exa * (Bxa * alph_s - Mathf.Atan(Bxa * alph_s)))); // Combined longitudinal force

            return Mathf.Clamp(Fx, -tr.Fz * mux, tr.Fz * mux);
        }
        public override float GetCombinedLateral()
        {
            float Fy0 = GetPureLateral();
            float gam_y_s = Mathf.Sin(tr.gam); // Camber spin

            float Cyk = td.rCy1; // Shape factor for combined slip
            float Byk = td.rBy1 * Mathf.Cos(Mathf.Atan(td.rBy2 * (tr.alph - td.rBy3))); // Slope factor for combined slip
            float Eyk = td.rEy1 + td.rEy2 * dfz; // Curvature factor for combined slip
            if (Eyk > 1f) Eyk = 1f;

            float Shyk = td.rHy1 + td.rHy2 * dfz; // Horizontal shift for combined slip
            float Dvyk = muy * tr.Fz * (td.rVy1 + td.rVy2 * dfz + td.rVy3 * gam_y_s) * Mathf.Cos(Mathf.Atan(td.rVy4 * tr.alph));
            Svyk = Dvyk * Mathf.Sin(td.rVy5 * Mathf.Atan(td.rVy6 * tr.kap)); // Kappa-induced side force

            float kap_s = tr.kap + Shyk; // Shifted slip ratio for combined slip

            float Dyk = Fy0 / (Mathf.Cos(Cyk * Mathf.Atan(Byk * Shyk - Eyk * (Byk * Shyk - Mathf.Atan(Byk * Shyk)))) + Mathf.Epsilon); // Combined slip peak value

            Fy = Dyk * Mathf.Cos(Cyk * Mathf.Atan(Byk * kap_s - Eyk * (Byk * kap_s - Mathf.Atan(Byk * kap_s)))) + Svyk; // Combined lateral force

            return Mathf.Clamp(Fy, -tr.Fz * muy, tr.Fz * muy);
        }
        public override float GetCombinedAligningTorque()
        {
            return GetPureAligningTorque(); // Very little difference to combined, using pure for performance
        }
        public override float GetRollingResistanceMoment()
        {
            float My = tr.r0 * -tr.Fz * tr.rollCoeff;
            return My;
        }
        public override float GetOverturningMoment()
        {
            float Mx = Fy * tr.r0 * Mathf.Cos(tr.gam);
            return Mx;
        }
    }
}