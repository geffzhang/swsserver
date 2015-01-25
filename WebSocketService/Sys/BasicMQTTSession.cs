using WebSocketService.Mqtt;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WebSocketService.Mqtt.ExtensionMethods;
using System;

namespace WebSocketService.Sys
{
    public class BasicMQTTSession : IMqttSession
    {
        private HashSet<short> pendingQoS2 = new HashSet<short>();
        private Queue<PendingMessage> pendingMessages = new Queue<PendingMessage>();
        private TaskCompletionSource<PendingMessage> pendingMessageCompletionSource;
        
        private IBroadcaster broadcaster;

        public IBroadcaster Broadcaster
        {
            get { return broadcaster; }
        }

        private ISerializer serializer;

        public string ClientId { get; set; }

        public IChannel Channel { get; set; }

        public BasicMQTTSession(
            string userId, 
            IChannel channel, 
            IBroadcaster broadcaster, 
            ISerializer serializer)
        {
            this.ClientId = userId;
            this.Channel = channel;
            this.broadcaster = broadcaster;
            this.serializer = serializer;
        }

        public virtual void Close()
        {
            this.Channel.Close();
        }

        public void Broadcast(IEnumerable<string> toUserIds, object message)
        {
            this.broadcaster.Broadcast(toUserIds, this.serializer.Serialize(message));
        }

        public void Write(object obj)
        {
            if (obj is MqttMessage)
            {
                MqttMessage message = obj as MqttMessage;
                using (var messageStream = new MemoryStream())
                {
                    message.WriteTo(messageStream);
                    ArraySegment<byte> bytes = new ArraySegment<byte>(messageStream.StreamToByteArray());
                    this.Channel.Write(bytes);
                }
            }
            else
            {
                this.Channel.Write(this.serializer.Serialize(obj));
            }
        }

        Task IMqttSession.StoreQoS2(short packetId)
        {
            pendingQoS2.Add(packetId);
            return Util.CompletedTask;
        }

        bool IMqttSession.HasQoS2(short packetId)
        {
            return Util.RunSynchronously<bool>(() => pendingQoS2.Contains(packetId)).Result;
        }

        Task IMqttSession.RemoveQoS2(short packetId)
        {
            pendingQoS2.Remove(packetId);
            return Util.CompletedTask;
        }

        private PendingMessage lastMessage;
        private T DequeueMessage<T>(short packetId) where T : PendingMessage
        {
            lastMessage = pendingMessages.Dequeue();
            Debug.Assert(lastMessage is T && lastMessage.PacketId == packetId);
            //Signal we still have messages if someone is waiting.
            if (pendingMessages.Count > 0 && pendingMessageCompletionSource != null)
            {
                TaskCompletionSource<PendingMessage> source = pendingMessageCompletionSource;
                pendingMessageCompletionSource = null;
                source.SetResult(pendingMessages.Peek());
            }
            return (T)lastMessage;
        }

        private Task<string> HandlePubResponse(short packetId)
        {

            PendingPublishMessage pendingMessage = DequeueMessage<PendingPublishMessage>(packetId);
            string result = pendingMessage == null ? null : ((PendingPublishMessage)pendingMessage).MessageId;
            return Util.RunSynchronously<string>(() => result);
        }

        string IMqttSession.PublishAcknowledged(short packetId)
        {
            return HandlePubResponse(packetId).Result;
        }

        string IMqttSession.PublishReceived(short packetId)
        {
            return HandlePubResponse(packetId).Result;
        }

        Task IMqttSession.PublishCompleted(short packetId)
        {
            PendingPubRelMessage pendingMessage = DequeueMessage<PendingPubRelMessage>(packetId);
            Debug.Assert(1 == packetId);
            return Util.CompletedTask;
        }

        PendingMessage IMqttSession.NextPending(PendingMessage lastMessage, int timeoutMilliseconds)
        {
            if (pendingMessages.Count > 0 && pendingMessages.Peek() != lastMessage)
            {
                return pendingMessages.Peek();
            }
            else
            {
                if (pendingMessageCompletionSource == null)
                    pendingMessageCompletionSource = new TaskCompletionSource<PendingMessage>();
                else
                    Debug.Assert(false);
                TaskCompletionSource<PendingMessage> currentCompletion = pendingMessageCompletionSource;
                if (pendingMessages.Count == 0)
                {
                    return pendingMessageCompletionSource.Task.Result;
                }
                else
                {
                    Task delayTask = Task.Delay(timeoutMilliseconds);
                    Task<PendingMessage> nextTask = pendingMessageCompletionSource.Task;
                    Task result = Task.WhenAny(delayTask, nextTask);
                    if (result == delayTask)
                    {
                        if (pendingMessages.Count > 0 && pendingMessages.Peek() == lastMessage)
                        {
                            pendingMessageCompletionSource = null;
                            return lastMessage;
                        }
                    }
                    return nextTask.Result;
                }
            }
        }


        void IMqttSession.Publish(string messageId, MqttQos qos)
        {
            var publish = new PendingPublishMessage(1, messageId, qos);
            //WARNING: Something you may not expect
            //SetResult will transfer control to a waiter if there is one. We need to get our queue straight before that happens
            //Or else whack shit goes wrong. So don't go thinking "it's prettier to see if the queue is empty before queueing".
            //Not that this was ever coded that way or anything...
            pendingMessages.Enqueue(publish);
            //If this is the first message, signal.
            if (pendingMessages.Count == 1 && pendingMessageCompletionSource != null)
            {
                TaskCompletionSource<PendingMessage> source = pendingMessageCompletionSource;
                pendingMessageCompletionSource = null;
                source.SetResult(publish);
            }
            if (qos == MqttQos.BestEffort)
            {
                pendingMessages.Enqueue(new PendingPubRelMessage(1));
            }
        }
    }
}
