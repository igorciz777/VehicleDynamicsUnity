using UnityEngine;

[CreateAssetMenu(fileName = "MFTireObject", menuName = "Scriptable Objects/MFTireObject")]
public class MFTireObject : TireObject
{
    // Pure Slip Longitudinal Coefficients
    [Header("Pure Slip Longitudinal Coefficients")]
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
    [Header("Pure Slip Lateral Coefficients")]
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
    [Header("Pure Slip Aligning Torque Coefficients")]
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
    [Header("Combined Longitudinal Coefficients")]
    public float rBx1 = 12f; // Slope factor for combined slip Fx reduction
    public float rBx2 = 10f; // Variation of slope Fx reduction with kappa
    public float rCx1 = 1.25f; // Shape factor for combined slip Fx reduction
    public float rEx1 = 0.65f; // Curvature factor of combined Fx
    public float rEx2 = -0.25f; // Curvature factor of combined Fx with load
    public float rHx1 = 0.005f; // Shift factor for combined slip Fx reduction

    // Combined Lateral Coefficients
    [Header("Combined Lateral Coefficients")]
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
}
