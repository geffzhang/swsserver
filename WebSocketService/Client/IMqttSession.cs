﻿using WebSocketService.Mqtt;
/*******************************************************************************
 * Copyright 2014 Darren Clark
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 ******************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketService.Sys;

namespace WebSocketService.Mqtt
{
    public class PendingMessage
    {
        public readonly short PacketId;

        protected PendingMessage(short packetId)
        {
            PacketId = packetId;
        }
    }

    public class PendingPubRelMessage : PendingMessage
    {
        public PendingPubRelMessage(short packetId) : base(packetId) { }
    }

    public class PendingPublishMessage :PendingMessage{

        public readonly string MessageId;
        public readonly MqttQos QoS;
        private bool duplicate = false;

        public bool Duplicate { get { return duplicate; } }


        public PendingPublishMessage(short packetId, string messageId, MqttQos qos)
            : base(packetId)
        {
            QoS = qos;
            MessageId = messageId;
        }
    }

    public interface IMqttSession : ISession
    {
        string PublishAcknowledged(short p);

        string PublishReceived(short p);

        Task RemoveQoS2(short p);

        Task PublishCompleted(short p);

        bool HasQoS2(short p);

        Task StoreQoS2(short p);

        void Publish(string messageId, MqttQos qoS);

        PendingMessage NextPending(PendingMessage lastMessage, int timeoutMilliseconds);

    }
}
