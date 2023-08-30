#region Using directives

using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.UI;
using UAManagedCore;
using FTOptix.NativeUI;
using FTOptix.Modbus;

#endregion

public class ProgressBarManager : BaseNetLogic {

    public override void Start() {
        //var progress = Project.Current.GetVariable("Model/Phases/ProgressBarPhase3");
        //progress.VariableChange += Progress_VariableChange;
    }

    private void Progress_VariableChange(object sender, VariableChangeEventArgs e) {
        var chargeSetPoint = Project.Current.GetVariable("Model/Phases/PhasesSetpointPercent");
        var progressPercentage = ((IUAVariable)sender).Value / (double)chargeSetPoint.Value;
        Rectangle progressBar = (Rectangle)Owner;

        if (progressPercentage < 0.75) {
            progressBar.FillColor = Colors.LightSkyBlue;
        } else if (progressPercentage < 0.99) {
            progressBar.FillColor = Colors.LightSkyBlue;
        } else {
            progressBar.FillColor = Colors.LightSkyBlue;
        }
    }

    public override void Stop() {
        // Insert code to be executed when the user-defined logic is stopped
    }
}
