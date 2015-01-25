using SuperWebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketService.Mqtt;

//http://www.w3-tutorials.com/asp-net/item/21-pub-sub-websocket-server 
namespace WebSocketService.Server
{
    public class PubSubWebSocketServer : WebSocketServer
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public Dictionary<string, List<SubscriberConstrains>> Subscriptions { get; set; }

        public Dictionary<string, List<MqttSubscriberConstrains>> MqTTSubscriptions { get; set; }
 
        public PubSubWebSocketServer()
        {
 
            this.Subscriptions = new Dictionary<string, List<SubscriberConstrains>>();
            this.SessionClosed += CPWebSocketServer_SessionClosed;
            this.NewMessageReceived += CPWebSocketServer_NewMessageReceived;
            this.NewDataReceived += PubSubWebSocketServer_NewDataReceived;
 
        }

        void PubSubWebSocketServer_NewDataReceived(WebSocketSession session, byte[] value)
        {
            MqttMessage incoming = MqttMessage.CreateFrom(value);
            switch (incoming.Header.MessageType)
            {
                case MqttMessageType.Publish:
                    PublishReceived(session, (MqttPublishMessage)incoming);
                    break;
                case MqttMessageType.Subscribe:
                    SubscribeReceived(session, (MqttSubscribeMessage)incoming);
                    break;
                case MqttMessageType.Unsubscribe:
                    RemoveSubscriptions(session, ((MqttUnsubscribeMessage)incoming).Payload.Subscriptions);
                    var unsubAck = new MqttUnsubscribeAckMessage().WithMessageIdentifier(((MqttPublishReceivedMessage)incoming).VariableHeader.MessageIdentifier);
                    using (var messageStream = new MemoryStream())
                    {
                        unsubAck.WriteTo(messageStream);
                        ArraySegment<byte> bytes = new ArraySegment<byte>(messageStream.ToArray());
                        session.Send(bytes);
                    }
                    break;
                case MqttMessageType.PublishAck:
                    //string messageId = session.PublishAcknowledged(((MqttPublishAckMessage)incoming).VariableHeader.MessageIdentifier);
                    //if (messageId != null)
                    //    storageProvider.ReleaseMessage(messageId);
                    break;
                case MqttMessageType.PublishReceived:
                    //messageId = session.PublishReceived(((MqttPublishReceivedMessage)incoming).VariableHeader.MessageIdentifier);
                    //if (messageId != null)
                    //    storageProvider.ReleaseMessage(messageId);
                    break;
                case MqttMessageType.PublishRelease:
                    //session.RemoveQoS2(((MqttPublishReleaseMessage)incoming).VariableHeader.MessageIdentifier);
                    //var pubComp = new MqttPublishCompleteMessage().WithMessageIdentifier(((MqttPublishReleaseMessage)incoming).VariableHeader.MessageIdentifier);
                    //session.Write(pubComp);
                    break;
                case MqttMessageType.PublishComplete:
                    //session.PublishCompleted(((MqttPublishCompleteMessage)incoming).VariableHeader.MessageIdentifier);
                    break;
            }
        }

        internal void PublishReceived(WebSocketSession session, MqttPublishMessage publishMessage)
        {
            try
            {
                string topic = publishMessage.VariableHeader.TopicName;
                List<MqttSubscriberConstrains> clients = new List<MqttSubscriberConstrains>();

                if (this.MqTTSubscriptions.ContainsKey(topic))
                {
                    var subscribers = (List<MqttSubscriberConstrains>)this.MqTTSubscriptions[topic];
                    foreach (var item in subscribers)
                    {
                        if (!clients.Exists(el => el.SubsciberId == item.SubsciberId))
                        {
                            clients.Add(item);
                        }
                    }
                    foreach (var client in clients)
                    {
                        var websocketsession = this.GetAppSessionByID(client.SubsciberId);
                        websocketsession.Send(new ArraySegment<Byte>(publishMessage.Payload.Message.ToArray()));
                    }
                }

                switch (publishMessage.Header.Qos)
                {
                    case MqttQos.AtLeastOnce:                        
                        var puback = new MqttPublishAckMessage().WithMessageIdentifier(publishMessage.VariableHeader.MessageIdentifier);
                        using (var messageStream = new MemoryStream())
                        {
                            puback.WriteTo(messageStream);
                            ArraySegment<byte> bytes = new ArraySegment<byte>(messageStream.ToArray());
                            session.Send(bytes);
                        }
                        break;
                    case MqttQos.AtMostOnce:
                        //session.StoreQoS2(publishMessage.VariableHeader.MessageIdentifier);
                        //var pubRec = new MqttPublishReleaseMessage().WithMessageIdentifier(publishMessage.VariableHeader.MessageIdentifier);
                        //session.Write(pubRec);
                        break;
                    case MqttQos.BestEffort:
                        var pubRec = new MqttPublishReceivedMessage().WithMessageIdentifier(publishMessage.VariableHeader.MessageIdentifier);
                        using (var messageStream = new MemoryStream())
                        {
                            pubRec.WriteTo(messageStream);
                            ArraySegment<byte> bytes = new ArraySegment<byte>(messageStream.ToArray());
                            session.Send(bytes);
                        }
                        break;
                    default:
                        break;
                }

            }
            catch (Exception ex)
            {
                log.Error("Error on Publich ", ex);



            }
            
        }

        private void SubscribeReceived(WebSocketSession session, MqttSubscribeMessage subscribeMessage)
        {
            MqttSubscribeAckMessage subAck = new MqttSubscribeAckMessage().WithMessageIdentifier(subscribeMessage.VariableHeader.MessageIdentifier);
            AddSubscriptions(session, subscribeMessage.Payload.Subscriptions);
            using (var messageStream = new MemoryStream())
            {
                subAck.WriteTo(messageStream);
                ArraySegment<byte> bytes = new ArraySegment<byte>(messageStream.ToArray());
                session.Send(bytes);
            }
        }

        public void AddSubscriptions(WebSocketSession session, IReadOnlyDictionary<string, MqttQos> subscriptions)
        {
            List<MqttQos> result = new List<MqttQos>();
            foreach (KeyValuePair<string, MqttQos> subscription in subscriptions)
            {
                if (this.MqTTSubscriptions.ContainsKey(subscription.Key))
                {
                    var subscribers = (List<MqttSubscriberConstrains>)this.MqTTSubscriptions[subscription.Key];
                    subscribers.Add(new MqttSubscriberConstrains(session.SessionID, subscription.Value));
                }
            }
        }


        public void RemoveSubscriptions(WebSocketSession session, IEnumerable<string> topicFilters)
        {
            foreach (string topicFilter in topicFilters)
            {
                if (this.MqTTSubscriptions.ContainsKey(topicFilter))
                {
                    var subscribers = (List<MqttSubscriberConstrains>)this.MqTTSubscriptions[topicFilter];

                    foreach (var item in subscribers)
                    {
                        if (item.SubsciberId == session.SessionID)
                        {
                            subscribers.Remove(item);
                        }
                    }
 
                } 
            }
        }
         
        public void SetSupportedTopics(List<string> topics)
        {
            foreach (var topic in topics)
            {
                this.Subscriptions.Add(topic, new List<SubscriberConstrains>()); 
                log.Info("Start listening for topic = " + topic); 
            }
 
        }
 
        void CPWebSocketServer_NewMessageReceived(WebSocketSession session, string value)
        {
 
            var clientRequest = ClientRequest.TryParse(value);
 
            log.Info("Request Received = " + value);
 
            if (clientRequest == null)
            {
                session.Send("Request not valid");
                return;
            }
 
            switch (clientRequest.RequestType.ToUpper())
            {
                case RequestType.SUBSCRIBE:
 
                    bool subscribed = this.SubscribedToTopic(clientRequest, session.SessionID);
 
                    if (subscribed)
                        session.Send("Successfully Subscribed to " + clientRequest.Topic);
                    else
                        session.Send("Error: Topic " + clientRequest.Topic + " Not Supported ");
 
                    break;
 
 
                case RequestType.UNSUBSCRIBE:
 
                    bool unsubscribed = this.UnsubribeFromTopic(clientRequest, session.SessionID);
 
                    if (unsubscribed)
                        session.Send("Successfully UnSubscribed from " + clientRequest.Topic);
                    else
                        session.Send("Error: UnSubscribed from topic " + clientRequest.Topic + " faild.");
 
                    break;
 
                case RequestType.PUBLISH:
 
                    this.Publish(clientRequest, value);
 
                    break;
 
            }
 
 
        }
 
 
        void CPWebSocketServer_SessionClosed(WebSocketSession session, SuperSocket.SocketBase.CloseReason value)
        {
 
            try
            {
                foreach (var subscriptions in this.Subscriptions)
                {
                    var subscribers = (List<SubscriberConstrains>)subscriptions.Value;
                    subscribers.RemoveAll(item => item.SubsciberId == session.SessionID);
                }
            }
            catch (Exception ex)
            {
 
                log.Error("Error on SessionClosed ", ex);
            }
            //remove subscriber resources
 
 
        }
 
 
        public bool SubscribedToTopic(ClientRequest clientRequest, string clientId)
        {
 
 
            try
            {
                if (this.Subscriptions.ContainsKey(clientRequest.Topic))
                {
                    var subscribers = (List<SubscriberConstrains>)this.Subscriptions[clientRequest.Topic];
                    subscribers.Add(new SubscriberConstrains(clientId, clientRequest.Constrains));
 
                    return true;
                }
 
                return false;
            }
            catch (Exception ex)
            {
 
                log.Error("Error on SubscribedToTopic ", ex);
                return false;
            }
 
 
 
 
        }
 
 
 
        public bool UnsubribeFromTopic(ClientRequest clientRequest, string clientId)
        {
 
            try
            {
                if (this.Subscriptions.ContainsKey(clientRequest.Topic))
                {
                    var subscribers = (List<SubscriberConstrains>)this.Subscriptions[clientRequest.Topic];
 
                    foreach (var item in subscribers)
                    {
 
                        if (item.SubsciberId == clientId)
                        {
 
 
 
                            if (clientRequest.Constrains != null && item.Constrains != null)
                                foreach (var constrain in clientRequest.Constrains)
                                {
                                    item.Constrains.RemoveAll(con => con.Equals(constrain));
 
                                }
 
 
                        }
 
 
                    }
 
                    //if no contrains exist then remove the subscriber
                    subscribers.RemoveAll(item => (item.Constrains == null || item.Constrains.Count == 0) && item.SubsciberId == clientId);
 
 
 
                    return true;
                }
 
 
                return false;
 
            }
            catch (Exception ex)
            {
                log.Error("Error on UnsubribeFromTopic ", ex);
                return false;
 
            }
 
        }
 
 
        public void Publish(ClientRequest clientRequest, string msg)
        {
 
            try
            {
                var clients = new List<string>();
 
                if (this.Subscriptions.ContainsKey(clientRequest.Topic))
                {
 
                    var subscribers = (List<SubscriberConstrains>)this.Subscriptions[clientRequest.Topic];
 
                    foreach (var item in subscribers)
                    {
                        //currently support only 1 constrain 
 
                        if (clientRequest.Constrains != null)
                        {
                            var items = (List<Constrain>)item.Constrains.FindAll(con => con.Equals(clientRequest.Constrains[0]));
 
                            if (items.Count > 0 && !clients.Exists(el => el == item.SubsciberId))
                                clients.Add(item.SubsciberId);
 
                        }
                        else
                        {
                            if (!clients.Exists(el => el == item.SubsciberId))
                                clients.Add(item.SubsciberId);
                        }
 
                    }
 
                    this.SendToAll(clients, msg);
 
 
                }
 
            }
            catch (Exception ex)
            {
                log.Error("Error on Publich ", ex);
 
 
 
            }
 
        }
 
 
        public void SendToAll(List<string> subscribers, string msg)
        {
 
            try
            {
                foreach (var sub in subscribers)
                {
 
                    this.GetAppSessionByID(sub).Send(msg);
 
                }
            }
            catch (Exception ex)
            {
                log.Error("Error on SendToAll ", ex);
 
            }
 
 
        }
 
    }
 
 
    public class SubscriberConstrains
    { 
        public string SubsciberId { get; set; }
        public List<Constrain> Constrains { get; set; }
 
        public SubscriberConstrains(string subsciberId, List<Constrain> constrains)
        {
             this.SubsciberId = subsciberId;
            this.Constrains = constrains; 
        }
 
    }

    public class MqttSubscriberConstrains
    {
        public string SubsciberId { get; set; }
        public MqttQos Qos { get; set; }
        public MqttSubscriberConstrains(string subsciberId, MqttQos qos)
        {
            this.SubsciberId = subsciberId;
            this.Qos = qos;
        }

    }
 
}