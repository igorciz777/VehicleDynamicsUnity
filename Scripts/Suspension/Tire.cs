using UnityEngine;

namespace VehicleDynamics
{
    public struct TireInput
    {
        public float kap; // slip ratio [-]
        public float alph; // slip angle [rad]
        public float Fz; // normal vertical load [N]
        public float gam; // camber/inclination angle [rad]
        public float Fz0; // nominal load [N]
        public float r0; // unloaded wheel radius [m]
        public float m_belt; // tire belt mass [kg]
        public Vector2 mu; // friction coefficient (longitudinal, lateral) [-]
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
        public float B = 10f; // Stiffness factor
        public float C_Long = 1.6f; // Longitudinal Shape factor
        public float C_Lat = 1.35f;  // Lateral Shape factor
        public float D = 1f;   // Peak factor
        public float E = 0.97f; // Curvature factor

        public override float UpdateSharedParams()
        {
            return 0f;
        }

        public override float GetPureLongitudinal()
        {
            if (tr.Fz <= 0) return 0f;

            float mu = tr.mu.x;
            float Fz = tr.Fz;

            float Fx = mu * Fz * D * Mathf.Sin(C_Long * Mathf.Atan(B * tr.kap - E * (B * tr.kap - Mathf.Atan(B * tr.kap))));
            return Fx;
        }
        public override float GetPureLateral()
        {
            if (tr.Fz <= 0) return 0f;

            float mu = tr.mu.y;
            float Fz = tr.Fz;

            float Fy = mu * Fz * D * Mathf.Sin(C_Lat * Mathf.Atan(B * tr.alph - E * (B * tr.alph - Mathf.Atan(B * tr.alph))));
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
            // Simple rolling resistance model
            float Crr = 0.015f; // Rolling resistance coefficient
            float My = Crr * tr.Fz * tr.r0;
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

        // Pure Slip Longitudinal Coefficients
        public float pCx1 = 1.6f; // Shape factor Cfx for longitudinal force
        public float pDx1 = 1.5f; // Longitudinal friction Mux at Fznom
        public float pDx2 = -0.16f; // Variation of friction Mux with load
        public float pDx3 = 0f; // Variation of friction Mux with inclination
        public float pEx1 = 0.45f; // Longitudinal curvature Efx at Fznom
        public float pEx2 = 0.9f; // Variation of curvature Efx with load
        public float pEx3 = 0.07f; // Variation of curvature Efx with load squared
        public float pEx4 = 0f; // Factor in curvature Efx while driving
        public float pKx1 = 22f; // Longitudinal slip stiffness Kfx/Fz at Fznom
        public float pKx2 = 0.5f; // Variation of slip stiffness Kfx/Fz with load
        public float pKx3 = 0.2f; // Exponent in slip stiffness Kfx/Fz with load
        public float pHx1 = 0.001f; // Horizontal shift Shx at Fznom
        public float pHx2 = 0.0005f; // Variation of shift Shx with load
        public float pVx1 = 0f; // Vertical shift Svx/Fz at Fznom
        public float pVx2 = 0f; // Variation of shift Svx/Fz with load

        // Pure Slip Lateral Coefficients
        public float pCy1 = 1.35f; // Shape factor Cfy for lateral forces
        public float pDy1 = 1.4f; // Lateral friction Muy
        public float pDy2 = -0.3f; // Variation of friction Muy with load
        public float pDy3 = -2.5f; // Variation of friction Muy with squared camber
        public float pEy1 = -0.5f; // Lateral curvature Efy at Fznom
        public float pEy2 = -0.005f; // Variation of curvature Efy with load
        public float pEy3 = -5f; // Zero order camber dependency of curvature Efy
        public float pEy4 = -500f; // Variation of curvature Efy with camber
        public float pKy1 = -20f; // Maximum value of stiffness Kfy/Fznom
        public float pKy2 = 2f; // Load at which Kfy reaches maximum value
        public float pKy3 = -0.025f; // Variation of Kfy/Fznom with camber
        public float pHy1 = 0.002f; // Horizontal shift Shy at Fznom
        public float pHy2 = 0f; // Variation of shift Shy with load
        public float pHy3 = 0.03f; // Variation of shift Shy with camber
        public float pVy1 = 0.04f; // Vertical shift in Svy/Fz at Fznom
        public float pVy2 = -0.01f; // Variation of shift Svy/Fz with load
        public float pVy3 = -0.3f; // Variation of shift Svy/Fz with camber
        public float pVy4 = -0.7f; // Variation of shift Svy/Fz with camber and load

        // Pure Slip Aligning Torque Coefficients
        public float qBz1 = 10f; // Trail slope factor for trail Bpt at Fznom
        public float qBz2 = -2f; // Variation of slope Bpt with load
        public float qBz3 = -0.5f; // Variation of slope Bpt with load squared
        public float qBz4 = 0.04f; // Variation of slope Bpt with camber
        public float qBz5 = 0.4f; // Variation of slope Bpt with absolute camber
        public float qBz9 = 8.9f; // Slope factor Br of residual torque Mzr
        public float qBz10 = 0f; // Slope factor Br of residual torque Mzr
        public float qCz1 = 1.2f; // Shape factor Cpt for pneumatic trail
        public float qDz1 = 0.09f; // Peak trail Dpt = Dpt*(Fz/Fznom*R0)
        public float qDz2 = -0.009f; // Variation of peak Dpt with load
        public float qDz3 = -0.05f; // Variation of peak Dpt with camber
        public float qDz4 = 0.75f; // Variation of peak Dpt with camber squared
        public float qDz6 = -0.007f; // Peak residual torque Dmr = Dmr/(Fz*R0)
        public float qDz7 = 0.005f; // Variation of peak factor Dmr with load
        public float qDz8 = -0.18f; // Variation of peak factor Dmr with camber
        public float qDz9 = 0.03f; // Variation of peak factor Dmr with camber and load
        public float qEz1 = -1.5f; // Trail curvature Ept at Fznom
        public float qEz2 = 0.33f; // Variation of curvature Ept with load
        public float qEz3 = 0f; // Variation of curvature Ept with load squared
        public float qEz4 = 0.27f; // Variation of curvature Ept with sign of Alpha-t
        public float qEz5 = -3.6f; // Variation of Ept with camber and sign Alpha-t
        public float qHz1 = 0.004f; // Trail horizontal shift Sht at Fznom
        public float qHz2 = 0.002f; // Variation of shift Sht with load
        public float qHz3 = 0.12f; // Variation of shift Sht with camber
        public float qHz4 = 0.06f; // Variation of shift Sht with camber and load

        // Combined Longitudinal Coefficients
        public float rBx1 = 12f; // Slope factor for combined slip Fx reduction
        public float rBx2 = 10f; // Variation of slope Fx reduction with kappa
        public float rCx1 = 1.25f; // Shape factor for combined slip Fx reduction
        public float rEx1 = 0.65f; // Curvature factor of combined Fx
        public float rEx2 = -0.25f; // Curvature factor of combined Fx with load
        public float rHx1 = 0.005f; // Shift factor for combined slip Fx reduction

        // Combined Lateral Coefficients
        public float rBy1 = 8f; // Slope factor for combined Fy reduction
        public float rBy2 = 9f; // Variation of slope Fy reduction with alpha
        public float rBy3 = -0.0275f; // Shift term for alpha in slope Fy reduction
        public float rCy1 = 1f; // Shape factor for combined Fy reduction
        public float rEy1 = -0.275f; // Curvature factor of combined Fy
        public float rEy2 = 0.33f; // Curvature factor of combined Fy with load
        public float rHy1 = 0f; // Shift factor for combined Fy reduction
        public float rHy2 = 0f; // Shift factor for combined Fy reduction with load
        public float rVy1 = -0.0275f; // Kappa induced side force Svyk/Muy*Fz at Fznom
        public float rVy2 = 0.05f; // Variation of Svyk/Muy*Fz with load
        public float rVy3 = -0.275f; // Variation of Svyk/Muy*Fz with camber
        public float rVy4 = 12f; // Variation of Svyk/Muy*Fz with alpha
        public float rVy5 = 1.9f; // Variation of Svyk/Muy*Fz with kappa
        public float rVy6 = -10.7f; // Variation of Svyk/Muy*Fz with atan(kappa)

        // Referenced calculated coefficients
        public float Kx; // From pure longitudinal
        public float Fx; // From combined longitudinal
        public float Svy, Shy, Ky, By, Cy, Fy0, muy; // From lateral
        public float Svx, Shx; // From longitudinal
        public float Fy, Svyk; // From combined lateral
        public float Bt, Ct, Dt, Et, Br, Cr, Dr, alph_t, alph_r; // From aligning torque

        // Shared coefficients
        public float Fz0_prim; // Nominal load
        public float dfz; // Fz deviation

        public override float UpdateSharedParams()
        {
            Fz0_prim = tr.Fz0; // Nominal load
            dfz = (tr.Fz - Fz0_prim) / (Fz0_prim + Mathf.Epsilon); // Fz deviation
            return 0f;
        }
        public override float GetPureLongitudinal()
        {
            Svx = tr.Fz * (pVx1 + pVx2 * dfz) * tr.mu.x; // Fx Vertical shift
            Shx = pHx1 + pHx2 * dfz; // Fx Horizontal shift

            float mux = (pDx1 + pDx2 * dfz) * (1 - pDx3 * tr.gam * tr.gam) * tr.mu.x;

            Kx = tr.Fz * (pKx1 + pKx2 * dfz) * Mathf.Exp(pKx3 * dfz); // Slip stiffness
            float Cx = pCx1; // Shape factor
            float Dx = mux * tr.Fz; // Peak value
            float Bx = Kx / (Cx * Dx + Mathf.Epsilon); // Stiffness factor
            float Ex = (pEx1 + pEx2 * dfz + pEx3 * dfz * dfz) * (1 - pEx4 * Mathf.Sign(tr.kap)); // Curvature factor
            if (Ex > 1f) Ex = 1f;

            float kapx = tr.kap + Shx; // Shifted slip ratio
            float Fx0 = Dx * Mathf.Sin(Cx * Mathf.Atan(Bx * kapx - Ex * (Bx * kapx - Mathf.Atan(Bx * kapx)))) + Svx; // Pure longitudinal force

            return Fx0;
        }
        public override float GetPureLateral()
        {
            float gam_y = tr.gam; // Inclination angle for lateral forces

            Svy = tr.Fz * (pVy1 + pVy2 * dfz + (pVy3 + pVy4 * dfz) * gam_y) * tr.mu.y; // Fy vertical shift
            Shy = pHy1 + pHy2 * dfz + (pHy3 * gam_y); // Fy horizontal shift

            float alph_y = tr.alph + Shy; // Shifted slip angle

            muy = (pDy1 + pDy2 * dfz) * (1 - pDy3 * gam_y * gam_y) * tr.mu.y; // Friction coefficient

            float Ky0 = pKy1 * Fz0_prim * Mathf.Sin(2f * Mathf.Atan(tr.Fz / (pKy2 * Fz0_prim))); // Cornering stiffness at Fznom
            Ky = Ky0 * (1 - pKy3 * Mathf.Abs(gam_y)); // Cornering stiffness
            Cy = pCy1; // Shape factor
            float Dy = muy * tr.Fz; // Peak value
            By = Ky / (Cy * Dy + Mathf.Epsilon); // Stiffness factor
            float Ey = (pEy1 + pEy2 * dfz) * (1 - (pEy3 + pEy4 * gam_y) * Mathf.Sign(alph_y)); // Curvature factor
            if (Ey > 1f) Ey = 1f;

            Fy0 = Dy * Mathf.Sin(Cy * Mathf.Atan(By * alph_y - Ey * (By * alph_y - Mathf.Atan(By * alph_y)))) + Svy; // Pure lateral force

            return Fy0;
        }
        public override float GetPureAligningTorque()
        {
            float gam_z = tr.gam; // Inclination angle for aligning torque

            float Shf = Shy + Svy / (Ky + Mathf.Epsilon); // Horizontal shift of pneumatic trail
            float Sht = qHz1 + qHz2 * dfz + (qHz3 + qHz4 * dfz) * gam_z; // Horizontal shift of pneumatic trail

            alph_t = tr.alph + Sht; // Slip angle for pneumatic trail
            alph_r = tr.alph + Shf; // Slip angle for residual moment

            Cr = 1f; // Shape factor for residual moment
            Dr = tr.Fz * (qDz6 + qDz7 * dfz + (qDz8 + qDz9 * dfz) * gam_z) * tr.r0 * tr.mu.y; // Peak value of residual moment
            Br = qBz9 * (1f / tr.mu.y) + qBz10 * By * Cy; // Slope factor of residual moment

            Ct = qCz1;
            Bt = (qBz1 + qBz2 * dfz + qBz3 * dfz * dfz) * (1 + qBz4 * gam_z + qBz5 * Mathf.Abs(gam_z)) * 1f / tr.mu.y; // Slope factor of pneumatic trail
            Dt = tr.Fz * (qDz1 + qDz2 * dfz) * (1 + qDz3 * gam_z + qDz4 * gam_z * gam_z) * (tr.r0 / Fz0_prim);
            Et = (qEz1 + qEz2 * dfz + qEz3 * dfz * dfz) * (1 + (qEz4 + qEz5 * gam_z) * (2f / Mathf.PI * Mathf.Atan(Bt * Ct * alph_t)));
            if (Et > 1f) Et = 1f;

            float Mzr = Dr * Mathf.Cos(Cr * Mathf.Atan(Br * alph_r)) * Mathf.Cos(tr.alph); // Residual moment
            float t = Dt * Mathf.Cos(Ct * Mathf.Atan(Bt * alph_t - Et * (Bt * alph_t - Mathf.Atan(Bt * alph_t)))) * Mathf.Cos(tr.alph); // Pneumatic trail

            float Mz0 = -t * Fy0 + Mzr; // Pure aligning torque

            return Mz0;
        }
        public override float GetCombinedLongitudinal()
        {
            float Fx0 = GetPureLongitudinal();

            float Cxa = rCx1; // Shape factor for combined slip
            float Bxa = rBx1 * Mathf.Cos(Mathf.Atan(rBx2 * tr.kap)); // Slope factor for combined slip
            float Exa = rEx1 + rEx2 * dfz; // Curvature factor for combined slip
            if (Exa > 1f) Exa = 1f;

            float Shxa = rHx1; // Horizontal shift for combined slip

            float alph_s = tr.alph + Shxa; // Shifted slip angle for combined slip

            float Dxa = Fx0 / (Mathf.Cos(Cxa * Mathf.Atan(Bxa * Shxa - Exa * (Bxa * Shxa - Mathf.Atan(Bxa * Shxa)))) + Mathf.Epsilon); // Combined slip peak value

            Fx = Dxa * Mathf.Cos(Cxa * Mathf.Atan(Bxa * alph_s - Exa * (Bxa * alph_s - Mathf.Atan(Bxa * alph_s)))); // Combined longitudinal force

            return Fx;
        }
        public override float GetCombinedLateral()
        {
            float Fy0 = GetPureLateral();

            float Cyk = rCy1; // Shape factor for combined slip
            float Byk = rBy1 * Mathf.Cos(Mathf.Atan(rBy2 * (tr.alph - rBy3))); // Slope factor for combined slip
            float Eyk = rEy1 + rEy2 * dfz; // Curvature factor for combined slip
            if (Eyk > 1f) Eyk = 1f;

            float Shyk = rHy1 + rHy2 * dfz; // Horizontal shift for combined slip
            float Dvyk = muy * tr.Fz * (rVy1 + rVy2 * dfz + rVy3 * tr.gam) * Mathf.Cos(Mathf.Atan(rVy4 * tr.alph));
            Svyk = Dvyk * Mathf.Sin(rVy5 * Mathf.Atan(rVy6 * tr.kap)); // Kappa-induced side force

            float kap_s = tr.kap + Shyk; // Shifted slip ratio for combined slip

            float Dyk = Fy0 / (Mathf.Cos(Cyk * Mathf.Atan(Byk * Shyk - Eyk * (Byk * Shyk - Mathf.Atan(Byk * Shyk)))) + Mathf.Epsilon); // Combined slip peak value

            Fy = Dyk * Mathf.Cos(Cyk * Mathf.Atan(Byk * kap_s - Eyk * (Byk * kap_s - Mathf.Atan(Byk * kap_s)))) + Svyk; // Combined lateral force

            return Fy;
        }
        public override float GetCombinedAligningTorque()
        {
            return GetPureAligningTorque(); // Very little difference to combined, using pure for performance
        }
        public override float GetRollingResistanceMoment()
        {
            float My = tr.r0 * (Svx + Kx * Shx);
            return My;
        }
        public override float GetOverturningMoment()
        {
            float Mx = Fy * tr.r0 * Mathf.Cos(tr.gam);
            return Mx;
        }
    }
}