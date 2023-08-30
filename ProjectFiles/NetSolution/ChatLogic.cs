#region Using directives

using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.UI;
using System;
using System.Linq;
using UAManagedCore;
using FTOptix.NativeUI;
using FTOptix.Modbus;

#endregion

public class ChatLogic : BaseNetLogic {
    private IUAVariable chatMessageReceived;

    public override void Start() {
        chatMessageReceived = Project.Current.GetVariable("Model/ChatMessageReceived");
        chatMessageReceived.VariableChange += ChatMessageReceived_VariableChange;
    }

    private void ChatMessageReceived_VariableChange(object sender, VariableChangeEventArgs e) {
        AddChatMessage((String)Project.Current.GetVariable("Model/ChatMessageReceived").Value, false);
    }

    public override void Stop() {
        chatMessageReceived.VariableChange -= ChatMessageReceived_VariableChange;
    }

    [ExportMethod]
    public void ClearChat() {
        foreach (var itemCol in ((ColumnLayout)Owner).Children.OfType<Label>())
            itemCol.Delete();
    }

    [ExportMethod]
    public void AddChatMessage(string message, bool me) {
        Label myMessageLabel = InformationModel.Make<Label>("Message" + Owner.Children.Count.ToString());
        myMessageLabel.Text = DateTime.Now.ToString() + ": " + message;
        myMessageLabel.WordWrap = true;
        myMessageLabel.Width = 350;
        if (me) {
            myMessageLabel.TextColor = Colors.MidnightBlue;
            myMessageLabel.HorizontalAlignment = HorizontalAlignment.Right;
            myMessageLabel.TextHorizontalAlignment = TextHorizontalAlignment.Right;
        } else {
            myMessageLabel.TextColor = Colors.DodgerBlue;
            myMessageLabel.HorizontalAlignment = HorizontalAlignment.Left;
            myMessageLabel.TextHorizontalAlignment = TextHorizontalAlignment.Left;
        }
        Owner.Children.Add(myMessageLabel);
    }
}
