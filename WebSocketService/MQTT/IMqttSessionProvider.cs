﻿/*******************************************************************************
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
    public interface IMqttSessionProvider<T> 
        : ISessionManager<T> where T : IMqttSession
    {
        Task CloseSession(IMqttSession session);

        Task<IReadOnlyCollection<MqttQos>> AddSubscriptions(IMqttSession session, IReadOnlyDictionary<String, MqttQos> subscriptions);

        IEnumerable<Tuple<IMqttSession, MqttQos>> GetSubscriptions(string topic);

        Task RemoveSubscriptions(IMqttSession session, IEnumerable<String> topicFilters);
    }
}
