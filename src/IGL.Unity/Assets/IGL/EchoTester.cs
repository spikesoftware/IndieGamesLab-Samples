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

public class EchoTester : MonoBehaviour {

    Guid _instance;
    IGL.Client.ServiceBusListener _listener;

    public UnityEngine.UI.Text MessageDisplay;
    public UnityEngine.UI.Text ButtonListenText;
    private bool _listenForMessages = false;

    private Queue<string> _messageQueue;
    
    void Start () {

        _instance = Guid.NewGuid();

        // initial setup of IGL        
        // please see http://indiegameslab.com/service/azure-function/ for the status of the Guest Service
        IGL.Configuration.CommonConfiguration.Instance.BackboneConfiguration.IssuerName = "IGLGuestClient";        
        IGL.Configuration.CommonConfiguration.Instance.BackboneConfiguration.IssuerSecret = "zQttoJG+laBopt7WMvbzV5Hk3oq0y6SxSqucjwnP7T4=";
        IGL.Configuration.CommonConfiguration.Instance.BackboneConfiguration.ServiceNamespace = "indiegameslab";

        IGL.Configuration.CommonConfiguration.Instance.GameId = 100;
        IGL.Configuration.CommonConfiguration.Instance.PlayerId = "TestingTesting";        

        _listener = new IGL.Client.ServiceBusListener();
        
        IGL.Client.ServiceBusListener.OnGameEventReceived += ServiceBusListener_OnGameEventReceived;
        IGL.Client.ServiceBusListener.OnListenError += ServiceBusListener_OnListenError;

        IGL.Client.ServiceBusWriter.OnSubmitError += ServiceBusWriter_OnSubmitError;
        IGL.Client.ServiceBusWriter.OnSubmitSuccess += ServiceBusWriter_OnSubmitSuccess;
        
        _messageQueue = new Queue<string>();
    }

    private void ServiceBusWriter_OnSubmitSuccess(object sender, IGL.MessageEventArgs e)
    {
        _messageQueue.Enqueue("Sent message to service bus without error");        
    }

    private void ServiceBusWriter_OnSubmitError(object sender, ErrorEventArgs e)
    {
        _messageQueue.Enqueue("Failed to send message to service bus.");
    }

    private void ServiceBusListener_OnListenError(object sender, System.IO.ErrorEventArgs e)
    {
        _messageQueue.Enqueue(string.Format("Error:{0}", e.GetException().Message));
    }

    private void ServiceBusListener_OnGameEventReceived(object sender, IGL.GamePacketArgs e)
    {
        if (e.GamePacket.GameEvent.Properties["Instance"] == null ||
           e.GamePacket.GameEvent.Properties["Instance"] != _instance.ToString())
            return;

        var sentTime = DateTime.Parse(e.GamePacket.GameEvent.Properties["Created"]);

        _messageQueue.Enqueue(string.Format("Packet:{0} Round Trip in {1} milliseconds.", 
                        e.GamePacket.PacketNumber,
                        DateTime.UtcNow.Subtract(sentTime).TotalMilliseconds));
    }
    
    void FixedUpdate()
    {
        if (_listenForMessages == true)
        {
            _listener.ListenForMessages();
        }

        if(_messageQueue.Count > 0)
        {
            // messages need to be added in the main thread
            MessageDisplay.text = _messageQueue.Dequeue() + Environment.NewLine + MessageDisplay.text;
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

        var wasSuccessful = IGL.Client.ServiceBusWriter.SubmitGameEvent("Echo", 1, gameEvent);

        if (!wasSuccessful)
        {
            MessageDisplay.text = "Failed to submit message to service bus." + Environment.NewLine + MessageDisplay.text;
        }        
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
        StartCoroutine(SubmitGameEvent());
    }    
   
}