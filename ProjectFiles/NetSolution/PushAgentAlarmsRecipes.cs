#region Using directives

using System;
using UAManagedCore;
using FTOptix.HMIProject;
using FTOptix.NetLogic;

#endregion

using uPLibrary.Networking.M2Mqtt.Messages;
using FTOptix.Store;
using FTOptix.SQLiteStore;
using Newtonsoft.Json.Linq;
using FTOptix.NativeUI;
using FTOptix.UI;
using FTOptix.Modbus;
using static PushAgent;

public class PushAgentAlarmsRecipes : BaseNetLogic {
    private PeriodicTask periodicTask;
    private string prevMachineName = "";
    private IUAVariable changeMachineName = Project.Current.GetVariable("Model/ChangeMachineName");

    public override void Start() {
        // Add subscriber
        if (MqttConnectorInstance.mqttconn == null) {
            periodicTask = new PeriodicTask(MqttConnect, 5000, LogicObject);
            periodicTask.Start();
        } else {
            prevMachineName = (string)Project.Current.GetVariable("Model/RetentiveMachineData/MachineName").Value;
            MqttConnectorInstance.mqttconn.AddSubscriber("/iotdemo-chatmessage-" + (string)Project.Current.GetVariable("Model/RetentiveMachineData/MachineName").Value, 1, SubscribeNotification);
            MqttConnectorInstance.mqttconn.AddSubscriber("/iotdemo-recipes", 1, SubscribeNotification);
        }

        changeMachineName.VariableChange += ChangeMachineName_VariableChange;
    }

    private void ChangeMachineName_VariableChange(object sender, VariableChangeEventArgs e) {
        if (changeMachineName.Value > 0) {
            MqttConnectorInstance.mqttconn.RemoveSubscriber(prevMachineName);
            prevMachineName = (string)Project.Current.GetVariable("Model/RetentiveMachineData/MachineName").Value;
            MqttConnectorInstance.mqttconn.AddSubscriber("/iotdemo-chatmessage-" + (string)Project.Current.GetVariable("Model/RetentiveMachineData/MachineName").Value, 1, SubscribeNotification);
        }
    }

    public override void Stop() {
        if (periodicTask != null) {
            periodicTask.Dispose();
            periodicTask = null;
        }
    }

    public void MqttConnect() {
        //Log.Info("CheckConnection");
        if (MqttConnectorInstance.mqttconn != null) {
            prevMachineName = (string)Project.Current.GetVariable("Model/RetentiveMachineData/MachineName").Value;
            MqttConnectorInstance.mqttconn.AddSubscriber("/iotdemo-chatmessage-" + (string)Project.Current.GetVariable("Model/RetentiveMachineData/MachineName").Value, 1, SubscribeNotification);
            MqttConnectorInstance.mqttconn.AddSubscriber("/iotdemo-recipes", 1, SubscribeNotification);
            periodicTask.Dispose();
            periodicTask = null;
        }
    }

    private void SubscribeClientMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e) {
        //erase recipes DB
        var myStore = Project.Current.Get<Store>("DataStores/EmbeddedDatabase1");
        object[,] resultSet;
        string[] header;
        myStore.Query(@"DELETE FROM ""iotdemo-Recipes""", out header, out resultSet);
        JObject o = JObject.Parse(System.Text.Encoding.UTF8.GetString(e.Message));

        //update recipes DB
        StoreData(o);
        //Log.Info("RecipesDb Updated");
    }

    private void ManageRecipes(MqttMsgPublishEventArgs e) {
        //erase recipes DB
        var myStore = Project.Current.Get<Store>("DataStores/EmbeddedDatabase1");
        object[,] resultSet;
        string[] header;
        myStore.Query(@"DELETE FROM ""iotdemo-Recipes""", out header, out resultSet);
        JObject o = JObject.Parse(System.Text.Encoding.UTF8.GetString(e.Message));

        //update recipes DB
        StoreData(o);
        //Log.Info("RecipesDb Updated");
    }

    private void SubscribeNotification(object sender, MqttMsgPublishEventArgs e) {
        string topicChat = "/iotdemo-notification-" + (string)Project.Current.GetVariable("Model/RetentiveMachineData/MachineName").Value;
        switch (e.Topic) {
            case "/iotdemo-recipes":
                ManageRecipes(e);
                break;

            default:
                ManageChatMessage(e);
                break;
        }
    }

    private void ManageChatMessage(MqttMsgPublishEventArgs e) {
        JObject o = JObject.Parse(System.Text.Encoding.UTF8.GetString(e.Message));
        if ((string)o.Property("MessageType") == "Chat") {
            string message = (string)o.Property("Message");
            Project.Current.GetVariable("Model/ChatMessageReceived").Value = message;
        } else {
            string message = (string)o.Property("Message");
            Project.Current.GetVariable("Model/Notification").Value = message;
        }
    }

    private void StoreData(JObject o) {
        SQLiteStore recipesDb = Project.Current.Get<SQLiteStore>("DataStores/EmbeddedDatabase1");
        var tblRecipes = recipesDb.Tables.Get<Table>("iotdemo-Recipes");
        object[,] rawValues = new object[1, 17];
        string[] columns = new string[17] { "Name", "/SET_Duration_F_1", "/SET_TemperatureSetpoint_F_1", "/SET_SpeedFanInput_F_1", "/SET_SpeedFanOutput_F_1", "/SET_Duration_F_2", "/SET_TemperatureSetpoint_F_2", "/SET_SpeedFanInput_F_2", "/SET_SpeedFanOutput_F_2", "/SET_Duration_F_3", "/SET_TemperatureSetpoint_F_3", "/SET_SpeedFanInput_F_3", "/SET_SpeedFanOutput_F_3", "/SET_Duration_F_0", "/SET_TemperatureSetpoint_F_0", "/SET_SpeedFanInput_F_0", "/SET_SpeedFanOutput_F_0" };

        var recipes = o.SelectToken("$.Recipes");
        JObject o1;
        foreach (var item in recipes) {
            o1 = (JObject)item;
            //Log.Info((string)o1.Property("Name"));
            rawValues[0, 0] = (string)o1.Property("Name");
            rawValues[0, 1] = Convert.ToUInt32((string)o1.Property("Phase1Duration"));
            rawValues[0, 2] = Convert.ToUInt32((string)o1.Property("Phase1SetPoint"));
            rawValues[0, 3] = Convert.ToUInt32((string)o1.Property("Phase1InputFanSpeed"));
            rawValues[0, 4] = Convert.ToUInt32((string)o1.Property("Phase1ExtractionFanSpeed"));
            rawValues[0, 5] = Convert.ToUInt32((string)o1.Property("Phase2Duration"));
            rawValues[0, 6] = Convert.ToUInt32((string)o1.Property("Phase2SetPoint"));
            rawValues[0, 7] = Convert.ToUInt32((string)o1.Property("Phase2InputFanSpeed"));
            rawValues[0, 8] = Convert.ToUInt32((string)o1.Property("Phase2ExtractionFanSpeed"));
            rawValues[0, 9] = Convert.ToUInt32((string)o1.Property("Phase3Duration"));
            rawValues[0, 10] = Convert.ToUInt32((string)o1.Property("Phase3SetPoint"));
            rawValues[0, 11] = Convert.ToUInt32((string)o1.Property("Phase3InputFanSpeed"));
            rawValues[0, 12] = Convert.ToUInt32((string)o1.Property("Phase3ExtractionFanSpeed"));
            rawValues[0, 13] = 0;
            rawValues[0, 14] = 0;
            rawValues[0, 15] = 0;
            rawValues[0, 16] = 0;

            tblRecipes.Insert(columns, rawValues);
        }
    }

    [ExportMethod]
    public void PushAlarm(string message) {
        if (MqttConnectorInstance.mqttconn != null)
            MqttConnectorInstance.mqttconn.Publish(message, "/iotdemo-alarm", false, 2);
    }
}
