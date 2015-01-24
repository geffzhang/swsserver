using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using WebSocketService.Sys;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketService.Mqtt;
using MsgPack.Serialization;

namespace CSharpClient
{

    public class PublishParameter
    {
        public string PublishType { get; set; }

        public string Content { get; set; }
    }

    public static class Helper
    { 
        public static string GeneratePublishCommand<T>(T o)
        {
            PublishParameter param = new PublishParameter() { PublishType = typeof(T).Name, Content = Serialize2XML<T>(o) };
            IncomingMessage data = new IncomingMessage() { Fn = "PublishSubscribe.Publish", Data = JToken.FromObject(param) };
            return Serialize(data);
        }

        public static byte[] GenerateSubscribeCommand(string topic)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                MqttSubscribeMessage data = new MqttSubscribeMessage().ToTopic(topic);
                data.WriteTo(stream);
                return stream.ToArray();
            }
        }

        public static byte[] GeneratePublishCommand<T>(T o, string topic,short messageId)
        {
            byte[] publishData = null;
            using (MemoryStream stream = new MemoryStream())
            {
                var serializer = SerializationContext.Default.GetSerializer<T>();
                serializer.Pack(stream,o) ;
                stream.Position = 0;
                publishData = stream.ToArray();
            }

            using (MemoryStream stream = new MemoryStream())
            {
                MqttPublishMessage data = new  MqttPublishMessage()
                    .ToTopic(topic)
                    .WithMessageIdentifier(messageId)
                    .WithQos(MqttQos.BestEffort)
                    .PublishData(publishData);
                data.WriteTo(stream);
                return stream.ToArray();
            }
        }


        public static string Serialize(object o)
        {
            JsonSerializer　serializer = new JsonSerializer();
            using (var stw = new StringWriter())
            {
                using (var jw = new JsonTextWriter(stw))
                {
                    serializer.Serialize(jw, o);
                    jw.Close();
                    return stw.ToString();
                }
            }
        }

        public static string Serialize2XML<T>(object o)
        {
            using (StringWriter sw = new StringWriter())
            {
                XmlSerializer xz = new XmlSerializer(typeof(T));
                xz.Serialize(sw, o);
                return sw.ToString();
            }
        }
    }
}
