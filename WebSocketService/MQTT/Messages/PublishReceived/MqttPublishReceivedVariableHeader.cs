/* 
 * nMQTT, a .Net MQTT v3 client implementation.
 * http://wiki.github.com/markallanson/nmqtt
 * 
 * Copyright (c) 2009 Mark Allanson (mark@markallanson.net) & Contributors
 *
 * Licensed under the MIT License. You may not use this file except 
 * in compliance with the License. You may obtain a copy of the License at
 *
 *     http://www.opensource.org/licenses/mit-license.php
*/

using System;
using System.IO;

namespace WebSocketService.Mqtt
{
    /// <summary>
    ///     Implementation of the variable header for an MQTT Publish Received message.
    /// </summary>
    public sealed class MqttPublishReceivedVariableHeader : MqttVariableHeader
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MqttPublishReceivedVariableHeader" /> class.
        /// </summary>
        public MqttPublishReceivedVariableHeader() {}

        /// <summary>
        ///     Initializes a new instance of the <see cref="MqttPublishReceivedVariableHeader" /> class.
        /// </summary>
        /// <param name="headerStream">A stream containing the header of the message.</param>
        public MqttPublishReceivedVariableHeader(Stream headerStream) {
            ReadFrom(headerStream);
        }

        /// <summary>
        ///     Returns the read flags for the publish message (topic, messageid)
        /// </summary>
        protected override ReadWriteFlags ReadFlags {
            get { return ReadWriteFlags.MessageIdentifier; }
        }

        /// <summary>
        ///     Returns the read flags for the publish message (topic, messageid)
        /// </summary>
        protected override ReadWriteFlags WriteFlags {
            get {
                // Read and write flags are identical for Publish Messages
                return ReadFlags;
            }
        }

        /// <summary>
        ///     Returns a <see cref="T:System.String" /> that represents the current <see cref="T:System.Object" />.
        /// </summary>
        /// <returns>
        ///     A <see cref="T:System.String" /> that represents the current <see cref="T:System.Object" />.
        /// </returns>
        public override string ToString() {
            return
                String.Format("PublishReceived Variable Header: MessageIdentifier={0}", MessageIdentifier);
        }
    }
}