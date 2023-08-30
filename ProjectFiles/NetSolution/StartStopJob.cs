#region Using directives

using FTOptix.HMIProject;
using FTOptix.NetLogic;
using System;
using UAManagedCore;
using FTOptix.NativeUI;
using FTOptix.UI;
using FTOptix.Modbus;

#endregion

public class StartStopJob : BaseNetLogic {
    private RemoteVariableSynchronizer variableSynchronizer;
    private IUAVariable running;

    public override void Start() {
        running = Project.Current.GetVariable("CommDrivers/EthernetIPDriver1/EthernetIPStation1/Tags/Program:Oven/Running");
        Log.Info(running.BrowseName);
        variableSynchronizer = new RemoteVariableSynchronizer();
        variableSynchronizer.Add(running);
        running.VariableChange += Running_VariableChange;
    }

    private void Running_VariableChange(object sender, VariableChangeEventArgs e) {
        if ((bool)running.Value) {
            Project.Current.GetVariable("Model/Job/Start").Value = DateTime.Now;
            Log.Info("Job Started at: " + (String)Project.Current.GetVariable("Model/Job/Start").Value);
        } else {
            Project.Current.GetVariable("Model/Job/Finish").Value = DateTime.Now;
            Log.Info("Job Ended at: " + (String)Project.Current.GetVariable("Model/Job/Finish").Value);
            try {
                NetLogicObject pushAgent = Project.Current.Find<NetLogicObject>("PushAgent");
                var args = new string[] { };
                pushAgent.ExecuteMethod("PushNewJob", args);
                Log.Info("Pushed Job Info");
            } catch (Exception eM) {
                Log.Info("Error pushing log information:" + eM.Message);
            }
        }
    }

    public override void Stop() {
        // Insert code to be executed when the user-defined logic is stopped
    }
}
