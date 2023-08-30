#region Using directives

using FTOptix.Alarm;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UAManagedCore;
using FTOptix.NativeUI;
using FTOptix.UI;
using FTOptix.Modbus;
using OpcUa = UAManagedCore.OpcUa;
using static PushAgent;

#endregion

public class DigitalAlarmWithPushNotificationLogic : BaseNetLogic, IUAEventObserver {

    private sealed class EventData {

        public EventData(IUAObject eventNotifier, IUAObjectType eventType, IReadOnlyList<object> args) {
            EventNotifier = eventNotifier;
            EventType = eventType;
            Args = args;
        }

        public IUAObject EventNotifier { get; set; }
        public IUAObjectType EventType { get; set; }
        public IReadOnlyList<object> Args { get; set; }
    }

    public override void Start() {
        alarmObject = (AlarmController)Owner;
        previousActiveState = GetInitialActiveState();
        affinityId = LogicObject.Context.AssignAffinityId();
        RegisterForLocalizedEvents();
    }

    private void RegisterForLocalizedEvents() {
        systemUserSessionHanlders = new Dictionary<string, ISessionHandler>();
        var projectLocales = Project.Current.Locales;

        using (var destroyOnExit = LogicObject.Context.Sessions.ImpersonateRootTemporary()) {
            foreach (var locale in projectLocales) {
                var sessionHandler = LogicObject.Context.Sessions.InternalCreateSession(
                    new QualifiedName(2, "SystemUser_" + locale), NodeId.Random(Project.Current.NodeId.NamespaceIndex));
                systemUserSessionHanlders.Add(locale, sessionHandler);
            }
        }

        eventRegistrations = new List<IEventRegistration>();

        foreach (var locale in projectLocales) {
            var sessionHandler = systemUserSessionHanlders[locale];
            using (var destroyOnExit = LogicObject.Context.Sessions.ImpersonateSessionTemporary(sessionHandler)) {
                eventRegistrations.Add(alarmObject.RegisterUAEventObserver(this, OpcUa.ObjectTypes.AlarmConditionType, affinityId));
            }
        }
    }

    public override void Stop() {
        foreach (var registration in eventRegistrations)
            registration?.Dispose();

        foreach (var sessionHandler in systemUserSessionHanlders.Values)
            sessionHandler?.Dispose();
    }

    public void OnEvent(IUAObject eventNotifier, IUAObjectType eventType, IReadOnlyList<object> args, ulong senderId) {
        var eventArguments = eventType.EventArguments;
        var eventId = (ByteString)eventArguments.GetFieldValue(args, "EventId");
        var eventIdString = eventId.ToString();

        if (!receivedEvents.ContainsKey(eventIdString)) {
            var eventList = new List<EventData>
            {
                new EventData(eventNotifier, eventType, args)
            };
            receivedEvents.Add(eventIdString, eventList);
        } else {
            receivedEvents[eventIdString].Add(new EventData(eventNotifier, eventType, args));
        }

        if (receivedEvents[eventIdString].Count == Project.Current.Locales.Length) {
            try {
                var alarmMessage = ConstructAlarmMessage(eventId);
                PushAlarmDatas(alarmMessage);
            } catch (Exception e) {
                Log.Error(e.Message);
            }
        }
    }

    private void PushAlarmDatas(string alarmMessage) {
        NetLogicObject pushAgent = Project.Current.Find<NetLogicObject>("PushAgentAlarmsRecipes");
        var args = new string[] { alarmMessage };
        pushAgent.ExecuteMethod("PushAlarm", args);
        Log.Debug("Messaggio Inviato: " + alarmMessage);
    }

    private bool GetInitialActiveState() {
        var retainedAlarmsNode = InformationModel.Get(FTOptix.Alarm.Objects.RetainedAlarms);

        var retainedAlarm = retainedAlarmsNode.Find(alarmObject.BrowseName);
        if (retainedAlarm == null)
            return false;

        return retainedAlarm.GetVariable("ActiveState/Id").Value;
    }

    private bool IsAlarmTransitioningFromInactiveState(bool currentActiveState) {
        if (!previousActiveState && currentActiveState) {
            previousActiveState = currentActiveState;
            return true;
        }

        previousActiveState = currentActiveState;
        return false;
    }

    private string ConstructAlarmMessage(ByteString eventId) {
        var currentEventList = receivedEvents[eventId.ToString()];
        var eventArguments = currentEventList[0].EventType.EventArguments;
        var args = currentEventList[0].Args;

        var timestamp = eventArguments.GetFieldValue(args, "Time");
        var ackedState = eventArguments.GetFieldValue(args, "AckedState/Id");
        var confirmedState = eventArguments.GetFieldValue(args, "ConfirmedState/Id");
        var activeState = eventArguments.GetFieldValue(args, "ActiveState/Id");
        var enabledState = eventArguments.GetFieldValue(args, "EnabledState/Id");
        var conditionName = eventArguments.GetFieldValue(args, "ConditionName");
        //var sourceName = MqttConnectorInstance.mqttconn.clientID;
        var sourceName = "banana";
        var severity = eventArguments.GetFieldValue(args, "Severity");
        var localTime = eventArguments.GetFieldValue(args, "LocalTime");

        var sb = new StringBuilder();
        var sw = new StringWriter(sb);
        using (var writer = new JsonTextWriter(sw)) {
            writer.Formatting = Formatting.None;

            writer.WriteStartObject();
            writer.WritePropertyName("ConditionName");
            writer.WriteValue(conditionName);
            writer.WritePropertyName("Time");
            writer.WriteValue(timestamp);
            writer.WritePropertyName("ActiveState_Id");
            writer.WriteValue(activeState);
            writer.WritePropertyName("AckedState_Id");
            writer.WriteValue(ackedState);
            writer.WritePropertyName("ConfirmedState_Id");
            writer.WriteValue(confirmedState);
            writer.WritePropertyName("EnabledState_Id");
            writer.WriteValue(enabledState);
            writer.WritePropertyName("SourceName");
            writer.WriteValue(sourceName);
            writer.WritePropertyName("Severity");
            writer.WriteValue(severity);
            writer.WritePropertyName("LocalTime");
            writer.WriteValue(((Struct)localTime).Values[0]);

            foreach (var evt in currentEventList) {
                var message = (LocalizedText)evt.EventType.EventArguments.GetFieldValue(evt.Args, "Message");
                writer.WritePropertyName("Message_" + message.LocaleId);
                writer.WriteValue(message.Text);
            }

            writer.WriteEnd();
        }

        return sb.ToString();
    }

    private bool previousActiveState;

    private List<IEventRegistration> eventRegistrations;
    private Dictionary<string, ISessionHandler> systemUserSessionHanlders;
    private readonly Dictionary<string, List<EventData>> receivedEvents = new Dictionary<string, List<EventData>>();
    private uint affinityId;

    //private IUANode emailUserNode; was unused
    private IUAObject alarmObject;
}
