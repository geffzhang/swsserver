using Newtonsoft.Json.Linq;
using WebSocketService.Mqtt;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;

namespace WebSocketService.Sys
{
    public abstract class BasicMQTTRouter<T> : IMqttSessionProvider<T>, IRouter<T> where T : IMqttSession
    {
        private ConcurrentDictionary<string, ControllerMethod> methods = new ConcurrentDictionary<string, ControllerMethod>(StringComparer.OrdinalIgnoreCase);
        private IMqttStorageProvider storageProvider;

        public ISerializer Serializer { get; set; }

        public SessionManager<T> Sessions { get; set; }

        public BasicMQTTRouter(ISerializer serializer, IMqttStorageProvider storageProvider)
        {
            this.Sessions = new SessionManager<T>();
            this.Serializer = serializer;
            this.storageProvider = storageProvider;
        }

        public abstract T Create(IChannel channel);

        public void AddControllersFromAssemblies(params Assembly[] assemblies)
        {
            assemblies
                .Select(a => a.GetTypesDerivedFrom<IController>(true))
                .ForEach(types => types.ForEach(AddController));
        }

        public virtual void Route(string message, T session)
        {
            try
            {
                var parsedMessage = Serializer.Deserialize<IncomingMessage>(message);
                ControllerMethod method;
                if (!methods.TryGetValue(parsedMessage.Fn, out method))
                {
                    session.Write(new ErrorMessage(HttpStatusCode.NotFound, "Could not find a handler for message '" + parsedMessage.Fn + "'."));
                }
                else
                {
                    method.Invoke(parsedMessage.Data, session);
                }
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException as HttpException;
                session.Write(inner != null ? new ErrorMessage(inner.GetHttpCode(), inner.Message) : new ErrorMessage((ex.InnerException ?? ex).Message));
            }
            catch (HttpException ex)
            {
                session.Write(new ErrorMessage(ex.Message));
            }
            catch (Exception ex)
            {
                session.Write(new ErrorMessage(ex.Message));
            }
        }

        public virtual void Route(byte[] messageData, T session)
        {
            try
            {
                MqttMessage incoming = MqttMessage.CreateFrom(messageData);
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
                        session.Write(unsubAck);
                        break;
                    case MqttMessageType.PublishAck:
                        string messageId = session.PublishAcknowledged(((MqttPublishAckMessage)incoming).VariableHeader.MessageIdentifier);
                        if (messageId != null)
                            storageProvider.ReleaseMessage(messageId);
                        break;
                    case MqttMessageType.PublishReceived:
                        messageId = session.PublishReceived(((MqttPublishReceivedMessage)incoming).VariableHeader.MessageIdentifier);
                        if (messageId != null)
                           storageProvider.ReleaseMessage(messageId);
                        break;
                    case MqttMessageType.PublishRelease:
                        session.RemoveQoS2(((MqttPublishReleaseMessage)incoming).VariableHeader.MessageIdentifier);
                        var pubComp = new MqttPublishCompleteMessage().WithMessageIdentifier(((MqttPublishReleaseMessage)incoming).VariableHeader.MessageIdentifier);
                        session.Write(pubComp);
                        break;
                    case MqttMessageType.PublishComplete:
                        session.PublishCompleted(((MqttPublishCompleteMessage)incoming).VariableHeader.MessageIdentifier);
                        break;
                }
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException as HttpException;
                session.Write(inner != null ? new ErrorMessage(inner.GetHttpCode(), inner.Message) : new ErrorMessage((ex.InnerException ?? ex).Message));
            }
            catch (HttpException ex)
            {
                session.Write(new ErrorMessage(ex.Message));
            }
            catch (Exception ex)
            {
                session.Write(new ErrorMessage(ex.Message));
            }
        }

        internal void PublishReceived(IMqttSession session, MqttPublishMessage publishMessage)
        {
            if (publishMessage.Header.Qos == MqttQos.BestEffort && session.HasQoS2(publishMessage.VariableHeader.MessageIdentifier))
            {
                var pubRec = new MqttPublishReceivedMessage()
                    .WithMessageIdentifier(publishMessage.VariableHeader.MessageIdentifier);
                session.Write(pubRec);
            }
            if (publishMessage.Header.Retain)
            {
                this.storageProvider.PutRetained(publishMessage.VariableHeader.TopicName , publishMessage.Payload.Message.ToArray());
            }
            PublishMessage(GetSubscriptions(publishMessage.VariableHeader.TopicName), publishMessage.VariableHeader.TopicName, publishMessage.Payload.Message.ToArray());

            switch(publishMessage.Header.Qos){
                case MqttQos.AtLeastOnce:
                    var puback = new MqttPublishAckMessage().WithMessageIdentifier(publishMessage.VariableHeader.MessageIdentifier);
                    session.Write(puback);
                    break;
                case MqttQos.AtMostOnce:
                    session.StoreQoS2(publishMessage.VariableHeader.MessageIdentifier);
                    var pubRec  = new MqttPublishReleaseMessage().WithMessageIdentifier(publishMessage.VariableHeader.MessageIdentifier);
                    session.Write(pubRec);
                    break;
                case MqttQos.BestEffort:
                default:
                    break;
            }
        }

        private void SubscribeReceived(IMqttSession session,  MqttSubscribeMessage subscribeMessage)
        {
            MqttSubscribeAckMessage subAck = new MqttSubscribeAckMessage().WithMessageIdentifier(subscribeMessage.VariableHeader.MessageIdentifier);
            AddSubscriptions(session, subscribeMessage.Payload.Subscriptions);
            session.Write(subAck);
            PublishMessages(session, storageProvider.GetRetained(subscribeMessage.Payload.Subscriptions), true);
        }     

        private  void PublishMessage(IEnumerable<Tuple<IMqttSession,MqttQos>> subscriptions, String topic, byte[] payload)
        {
            String messageId = null; //Used to for non QoS 0 messages, to store message for sending.
            MqttPublishMessage message = null; //Used for QoS 0 messages, to send the message. Can reuse since there is no message identifier.
            foreach (Tuple<IMqttSession, MqttQos> subscription in subscriptions)
            {
                //Persist a reference and queue if QoS > 0
                if (subscription.Item2 != MqttQos.BestEffort)
                {
                    //Save the message if we haven't already.
                    if (messageId == null)
                    {
                        messageId = storageProvider.StoreMessage(new InFlightMessage(topic, payload));
                    }
                    else
                    {
                        storageProvider.ReferenceMessage(messageId);
                    }
                    subscription.Item1.Publish(messageId, subscription.Item2);
                }
                else
                {

                    if (message == null)
                        message = new MqttPublishMessage().WithQos(MqttQos.AtMostOnce);
                    try
                    {
                        subscription.Item1.Write(message);
                    }
                    catch (Exception)
                    {
                        //Close socket?
                    }

                }
            }
        }


        private void PublishMessages(IMqttSession session, IEnumerable<Tuple<string, MqttQos, byte[]>> messages, bool retained)
        {
            foreach (var message in messages)
            {
                string messageId = null;
                short? packetId = null;
                //QOS 1 or 2, store in storage, and in session.
                if (message.Item2 != MqttQos.BestEffort)
                {
                    messageId = storageProvider.StoreMessage(new InFlightMessage(message.Item1, message.Item3));
                    session.Publish(messageId, message.Item2);
                }
                else
                {
                    //QoS 0 just publish, that way the session can keep a straight up queue and not block QoS 0 messages from
                    //intervening.
                    MqttPublishMessage publishMessage = new MqttPublishMessage().WithQos(message.Item2).ToTopic(message.Item1).PublishData(message.Item3);
                    publishMessage.Header.Retain = retained;
                    publishMessage.Header.Duplicate = false;                  
                    session.Write(publishMessage);
                }
            }
        }

        public virtual void Error(Exception ex, T session)
        {
            Console.WriteLine(ex.ToString());
        }

        public virtual void Remove(T session)
        {
            this.Sessions.Remove(session);
        }

        private void AddController(Type t)
        {
            var inst = (IController)t.CreateInstance();
            var preFilters = inst.GetType().GetCustomAttributes<BeforeExecuteAttribute>(true);
            var postFilters = inst.GetType().GetCustomAttributes<AfterExecuteAttribute>(true);

            foreach (var method in t.GetMethods().Where(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length == 2 && typeof(ISession).IsAssignableFrom(parameters[1].ParameterType);
            }))
            {
                methods.TryAdd(t.Name.Replace("Controller", string.Empty) + "." + method.Name, new ControllerMethod(inst, method, preFilters, postFilters));
            }
        }

        private class ControllerMethod
        {
            private MethodInfo method;
            private Type dataType;
            private IController instance;
            private IEnumerable<BeforeExecuteAttribute> preFilters;
            private IEnumerable<AfterExecuteAttribute> postFilters;

            public ControllerMethod(IController instance, MethodInfo method, IEnumerable<BeforeExecuteAttribute> preFilters, IEnumerable<AfterExecuteAttribute> postFilters)
            {
                this.instance = instance;
                this.method = method;
                this.dataType = method.GetParameters()[0].ParameterType;

                this.preFilters = method.GetCustomAttributes<BeforeExecuteAttribute>(true).Union(preFilters);
                this.postFilters = method.GetCustomAttributes<AfterExecuteAttribute>(true).Union(postFilters);
            }

            public void Invoke(JToken data, ISession session)
            {
                var model = data.ToObject(dataType);

                if (preFilters != null) 
                    PreFilter(model, session);
                method.Invoke(instance, new object[] { model, session });
                if (postFilters != null) 
                    PostFilter(model, session);
            }

            private void PostFilter(object model, ISession session)
            {
                postFilters.ForEach(filter => filter.AfterExecute(model, session));
            }

            private void PreFilter(object model, ISession session)
            {
                preFilters.ForEach(filter => filter.BeforeExecute(model, session));
            }
        }

        private class SubscriptionNode
        {
            public Dictionary<String, SubscriptionNode> children;
            public Dictionary<IMqttSession, MqttQos> subscribers;
        }

        private SubscriptionNode subscriptionRoot = new SubscriptionNode() { children = new Dictionary<string, SubscriptionNode>() };

        public Task CloseSession(IMqttSession session)
        {
            //TODO: Clean up subscriptions if cleansession
            return Util.CompletedTask;
        }

        public IEnumerable<Tuple<IMqttSession, MqttQos>> GetSubscriptions(string topic)
        {
            List<SubscriptionNode> current = new List<SubscriptionNode>(new SubscriptionNode[] { subscriptionRoot });
            List<Tuple<IMqttSession, MqttQos>> results = new List<Tuple<IMqttSession, MqttQos>>();
            foreach (string segment in topic.Split('/'))
            {
                if (current.Count == 0) break;
                List<SubscriptionNode> next = new List<SubscriptionNode>();
                foreach (SubscriptionNode test in current)
                {
                    SubscriptionNode node;
                    if (test.children.TryGetValue("#", out node))
                    {
                        foreach (KeyValuePair<IMqttSession, MqttQos> subscriber in node.subscribers)
                        {
                            results.Add(new Tuple<IMqttSession, MqttQos>(subscriber.Key, subscriber.Value));
                        }
                    }
                    if (test.children.TryGetValue("+", out node))
                    {
                        next.Add(node);
                    }
                    if (test.children.TryGetValue(segment, out node))
                    {
                        next.Add(node);
                    }
                }
                current = next;
            }
            foreach (SubscriptionNode node in current)
            {
                foreach (KeyValuePair<IMqttSession, MqttQos> subscriber in node.subscribers)
                {
                    results.Add(new Tuple<IMqttSession, MqttQos>(subscriber.Key, subscriber.Value));
                }
            }
            return Util.RunSynchronously<IEnumerable<Tuple<IMqttSession, MqttQos>>>(() => results).Result;
        }


        private SubscriptionNode LookupNode(string topicFilter)
        {
            SubscriptionNode node = subscriptionRoot;
            foreach (string segment in topicFilter.Split('/'))
            {
                if (node.children == null) node.children = new Dictionary<string, SubscriptionNode>();
                SubscriptionNode next;
                if (!node.children.TryGetValue(segment, out next))
                {
                    next = new SubscriptionNode();
                    node.children.Add(segment, next);
                }
                node = next;
            }
            return node;
        }

        public Task<IReadOnlyCollection<MqttQos>> AddSubscriptions(IMqttSession session, IReadOnlyDictionary<string, MqttQos> subscriptions)
        {
            List<MqttQos> result = new List<MqttQos>();
            foreach (KeyValuePair<string, MqttQos> subscription in subscriptions)
            {
                SubscriptionNode node = LookupNode(subscription.Key);
                if (node.subscribers == null) node.subscribers = new Dictionary<IMqttSession, MqttQos>();
                node.subscribers[session] = subscription.Value;
                result.Add(subscription.Value);
            }
            return Util.RunSynchronously<IReadOnlyCollection<MqttQos>>(() => result);
        }


        public Task RemoveSubscriptions(IMqttSession session, IEnumerable<string> topicFilters)
        {
            foreach (string topicFilter in topicFilters)
            {
                SubscriptionNode node = LookupNode(topicFilter);
                if (node.subscribers != null && node.subscribers.Remove(session))
                {
                    //TODO: clean up dead branches, recursively.
                    //    if (node.subscribers.Count == 0 && node.children.Count == 0)
                    //    {
                    //        int lastSlash = topicFilter.LastIndexOf('/')
                    //        string parentTopic = topicFilter.Substring(0, topicFilter.LastIndexOf('/'));
                    //        SubscriptionNode parentNode = LookupNode(parentTopic);
                    //        parentNode.children.Remove()
                    //    }
                }
            }
            return Util.CompletedTask;
        }
    }
}
