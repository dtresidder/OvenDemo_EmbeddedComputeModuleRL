#region Using directives

using FTOptix.NetLogic;
using FTOptix.UI;
using System.Threading;
using UAManagedCore;
using FTOptix.NativeUI;
using FTOptix.Modbus;

#endregion

public class AutoRefresher : BaseNetLogic {
    private DataGrid dataGrid;

    public override void Start() {
        var autoRefreshCheckBox = LogicObject.Owner.Owner.Get<CheckBox>("CheckBox1");
        var activeVariable = autoRefreshCheckBox.CheckedVariable;
        activeVariable.VariableChange += OnActiveVariableChanged;
    }

    private void OnActiveVariableChanged(object sender, VariableChangeEventArgs e) {
        if ((bool)e.NewValue) {
            dataGrid = (DataGrid)Owner;
            refreshTask = new PeriodicTask(RefreshDataGrid, 4000, LogicObject);
            refreshTask.Start();
        } else {
            refreshTask?.Dispose();
        }
    }

    public override void Stop() {
        refreshTask?.Dispose();
    }

    public void RefreshDataGrid() {
        Thread.Sleep(1000);
        dataGrid.Refresh();
    }

    private PeriodicTask refreshTask;
}
