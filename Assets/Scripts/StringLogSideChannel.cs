using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.SideChannels;
using System.Text;
using System;

public class StringLogSideChannel : SideChannel
{
    public string datasetReceived = null;
    public StringLogSideChannel(string guid)
    {
        ChannelId = new Guid(guid);
    }

    protected override void OnMessageReceived(IncomingMessage msg)
    {
        var receivedString = msg.ReadString();
        Debug.Log("From Python : " + receivedString);
        datasetReceived = receivedString;
    }

    public void SendEnvInfoToPython(string info)
    {
        using (var msgOut = new OutgoingMessage())
        {
            msgOut.WriteString(info);
            QueueMessageToSend(msgOut);
        }
    }

    //public void SendDebugStatementToPython(string logString, string stackTrace, LogType type)
    //{
    //    if (type == LogType.Error)
    //    {
    //        var stringToSend = type.ToString() + ": " + logString + "\n" + stackTrace;
    //        using (var msgOut = new OutgoingMessage())
    //        {
    //            msgOut.WriteString(stringToSend);
    //            QueueMessageToSend(msgOut);
    //        }
    //    }
    //}
}