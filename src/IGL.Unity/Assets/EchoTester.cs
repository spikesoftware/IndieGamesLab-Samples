using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.IO;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Collections.Specialized;
using System.Linq;

public class EchoTester : MonoBehaviour
{

    Guid _instance;
    IGL.Client.ServiceBusListener _listener;

    public UnityEngine.UI.Text MessageDisplay;
    public UnityEngine.UI.Text ButtonListenText;
    private bool _listenForMessages = false;

    void Start()
    {

        _instance = Guid.NewGuid();

        //// initial setup of IGL
        IGL.Configuration.CommonConfiguration.Instance.BackboneConfiguration.IssuerName = "IGLGuestClient";
        IGL.Configuration.CommonConfiguration.Instance.BackboneConfiguration.IssuerSecret = "2PenhRgdmlf6F1oNglk9Wra1FRH31pcOwbB3q4X0vDs=";
        IGL.Configuration.CommonConfiguration.Instance.BackboneConfiguration.ServiceNamespace = "indiegameslab";

        IGL.Configuration.CommonConfiguration.Instance.GameId = 100;
        IGL.Configuration.CommonConfiguration.Instance.PlayerId = "TestingTesting";

        _listener = new IGL.Client.ServiceBusListener();

        IGL.Client.ServiceBusListener.OnGameEventReceived += ServiceBusListener_OnGameEventReceived;
        IGL.Client.ServiceBusListener.OnListenError += ServiceBusListener_OnListenError;
    }

    private void ServiceBusListener_OnListenError(object sender, System.IO.ErrorEventArgs e)
    {
        Debug.LogErrorFormat("Error:{0}", e.GetException().Message);
    }

    private void ServiceBusListener_OnGameEventReceived(object sender, IGL.GamePacketArgs e)
    {
        if (e.GamePacket.GameEvent.Properties["Instance"] == null ||
            e.GamePacket.GameEvent.Properties["Instance"] != _instance.ToString())
            return;

        var sentTime = DateTime.Parse(e.GamePacket.GameEvent.Properties["Created"]);

        Debug.LogFormat("Packet:{0} Round Trip in {1} milliseconds.",
                        e.GamePacket.PacketNumber,
                        DateTime.UtcNow.Subtract(sentTime).TotalMilliseconds);
    }


    void Update()
    {
        if (_listenForMessages == true)
        {
            _listener.ListenForMessages();
        }
    }

    IEnumerator SubmitGameEvent()
    {
        yield return new WaitForEndOfFrame();

        var gameEvent = new IGL.GameEvent
        {
            Properties = new Dictionary<string, string>()
            {
                { "Created", DateTime.UtcNow.ToString() },
                { "Instance", _instance.ToString() }
            }
        };

        MessageDisplay.text = "Sent message without error: " + IGL.Client.ServiceBusWriter.SubmitGameEvent("Echo", 1, gameEvent).ToString() + Environment.NewLine + MessageDisplay.text;
    }

    public void StartListening()
    {
        if (ButtonListenText.text == "Start Listen")
        {
            MessageDisplay.text = "Listener started" + Environment.NewLine + MessageDisplay.text;
            _listenForMessages = true;
            ButtonListenText.text = "Stop Listening";
        }
        else
        {
            MessageDisplay.text = "Listener stopped" + Environment.NewLine + MessageDisplay.text;
            _listenForMessages = false;
            ButtonListenText.text = "Start Listen";
        }
    }

    public void SendEchoMessage()
    {
        var gameEvent = new IGL.GameEvent
        {
            Properties = new Dictionary<string, string>()
        {
            { "Created", DateTime.UtcNow.ToString() },
            { "Instance", _instance.ToString() }
        }
        };

        bool wasSuccessful = IGL.Client.ServiceBusWriter.SubmitGameEvent("Echo", 1, gameEvent);

        MessageDisplay.text = "Sent message without error: " + wasSuccessful.ToString() + Environment.NewLine + MessageDisplay.text;
    }

    public void SendArxMessage()
    {
        var gameEvent = new IGL.GameEvent
        {
            Properties = new Dictionary<string, string>()
        {
            { "Created", DateTime.UtcNow.ToString() },
            { "Instance", _instance.ToString() },
            { "StatViewModel.Name", "Pyramid" },
            { "StatViewModel.Value", UnityEngine.Random.Range(700, 950).ToString() },
        }
        };

        bool wasSuccessful = IGL.Client.ServiceBusWriter.SubmitGameEvent("arxevent", 1, gameEvent);

        MessageDisplay.text = "Sent message without error: " + wasSuccessful.ToString() + Environment.NewLine + MessageDisplay.text;
    }

}