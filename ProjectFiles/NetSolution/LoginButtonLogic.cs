#region Using directives
using System;
using FTOptix.CoreBase;
using FTOptix.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.NetLogic;
using FTOptix.Core;
using FTOptix.UI;
using FTOptix.Modbus;
#endregion

public class LoginButtonLogic : BaseNetLogic
{
    public override void Start()
    {
        ComboBox comboBox = Owner.Owner.Get<ComboBox>("Username");
        if (Project.Current.AuthenticationMode == AuthenticationMode.ModelOnly)
        {
            comboBox.Mode = ComboBoxMode.Normal;
        }
        else
        {
            comboBox.Mode = ComboBoxMode.Editable;
        }
    }

    public override void Stop()
    {

    }

    [ExportMethod]
    public void PerformLogin(string username, string password)
    {
        var usersAlias = LogicObject.GetAlias("Users");
        if (usersAlias == null || usersAlias.NodeId == NodeId.Empty)
        {
            Log.Error("LoginButtonLogic", "Missing Users alias");
            return;
        }

        var passwordExpiredDialogType = LogicObject.GetAlias("PasswordExpiredDialogType") as DialogType;
        if (passwordExpiredDialogType == null)
        {
            Log.Error("LoginButtonLogic", "Missing PasswordExpiredDialogType alias");
            return;
        }

        Button loginButton = (Button)Owner;
        loginButton.Enabled = false;

        try
        {
            var loginResult = Session.Login(username, password);
            switch (loginResult.ResultCode)
            {
                case ChangeUserResultCode.WrongPassword:
                    loginButton.Enabled = true;
                    Log.Error("LoginButtonLogic", "Wrong username or password");
                    break;
                case ChangeUserResultCode.PasswordExpired:
                    loginButton.Enabled = true;
                    var user = usersAlias.Get<User>(username);
                    var ownerButton = (Button)Owner;
                    ownerButton.OpenDialog(passwordExpiredDialogType, user.NodeId);
                    return;
                case ChangeUserResultCode.UserNotFound:
                    loginButton.Enabled = true;
                    Log.Error("LoginButtonLogic", "Could not find user " + username);
                    break;
                case ChangeUserResultCode.UnableToCreateUser:
                    loginButton.Enabled = true;
                    Log.Error("LoginButtonLogic", "Unable to create user " + username);
                    break;
                case ChangeUserResultCode.LoginAttemptBlocked:
                    loginButton.Enabled = true;
                    Log.Error("LoginButtonLogic", "Login attempt blocked");
                    break;
                case ChangeUserResultCode.HomonymUsers:
                    loginButton.Enabled = true;
                    break;
            }

            var outputMessageLabel = Owner.Owner.GetObject("LoginFormOutputMessage");
            var outputMessageLogic = outputMessageLabel.GetObject("LoginFormOutputMessageLogic");
            outputMessageLogic.ExecuteMethod("SetOutputMessage", new object[] { (int)loginResult.ResultCode });
        }
        catch (Exception e)
        {
            Log.Error("LoginButtonLogic", e.Message);
        }

        loginButton.Enabled = true;
    }
}
