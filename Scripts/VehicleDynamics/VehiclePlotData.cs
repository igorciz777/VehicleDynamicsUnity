using UnityEngine;
using VehicleDynamics;

public class VehiclePlotData : MonoBehaviour
{
    [Header("Vehicle Components")]
    public Engine engine;
    public VehicleModel vehicleModel;
    public Drivetrain drivetrain;
    public Suspension frontSuspension;
    public Suspension rearSuspension;
    public Hub FL_Hub;
    public Hub FR_Hub;
    [Header("Plot Views 1")]
    public PlotView throttlePlot;
    public PlotView brakePlot;
    public PlotView clutchPlot;
    public PlotView engineRpmPlot;
    public PlotView vehicleSpeedPlot;
    [Header("Plot Views 2")]
    public PlotView FL_CompressionPlot;
    public PlotView FR_CompressionPlot;
    public PlotView RL_CompressionPlot;
    public PlotView RR_CompressionPlot;
    public PlotView F_ARB_Plot;
    public PlotView R_ARB_Plot;
    [Header("Plot Views 3")]
    public PlotView FLWheelSlipRatioPlot;
    public PlotView FLWheelSlipAnglePlot;
    public PlotView FRWheelSlipRatioPlot;
    public PlotView FRWheelSlipAnglePlot;
    public PlotView FLCamberAnglePlot;
    public PlotView FRCamberAnglePlot;
    private void Start()
    {
        engineRpmPlot.autoScaleY = false;
        engineRpmPlot.manualYRange = new(0f, engine.rpmMax);
    }
    private void Update()
    {
        throttlePlot.AddData(vehicleModel.throttleInput);
        brakePlot.AddData(vehicleModel.brakeInput);
        clutchPlot.AddData(vehicleModel.clutchInput);
        engineRpmPlot.AddData(engine.engineRpm);
        vehicleSpeedPlot.AddData(drivetrain.vehicleBody.linearVelocity.magnitude * 3.6f); // Convert to km/h

        // Suspension
        (Strut, Strut) frontStruts = frontSuspension.GetStruts();
        (Strut, Strut) rearStruts = rearSuspension.GetStruts();
        FL_CompressionPlot.AddData(frontStruts.Item1.GetSpringCompression());
        FR_CompressionPlot.AddData(frontStruts.Item2.GetSpringCompression());
        RL_CompressionPlot.AddData(rearStruts.Item1.GetSpringCompression());
        RR_CompressionPlot.AddData(rearStruts.Item2.GetSpringCompression());
        F_ARB_Plot.AddData(frontSuspension.antirollForce);
        R_ARB_Plot.AddData(rearSuspension.antirollForce);

        Wheel FL_Wheel = FL_Hub.GetWheel();
        FLWheelSlipRatioPlot.AddData(FL_Wheel.slipRatio * 100f);
        FLWheelSlipAnglePlot.AddData(FL_Wheel.slipAngle * Mathf.Rad2Deg);
        FLCamberAnglePlot.AddData(FL_Hub.camberAngle);

        Wheel FR_Wheel = FR_Hub.GetWheel();
        FRWheelSlipRatioPlot.AddData(FR_Wheel.slipRatio * 100f);
        FRWheelSlipAnglePlot.AddData(FR_Wheel.slipAngle * Mathf.Rad2Deg);
        FRCamberAnglePlot.AddData(FR_Hub.camberAngle);
    }
}
