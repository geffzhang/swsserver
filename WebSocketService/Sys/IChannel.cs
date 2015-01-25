using System;
using System.Collections.Generic;
using System.Net;

namespace WebSocketService.Sys
{
    /// <summary>
    /// Abstracts an incoming request/conection.
    /// </summary>
    public interface IChannel
    {
        /// <summary>
        /// Gets the metadata (e.g. cookies) for the channel
        /// </summary>
        IMetadataCollection Metadata { get; }

        /// <summary>
        /// Gets the remote address
        /// </summary>
	    EndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Sends data over the channel
        /// </summary>
        /// <param name="message">The message to be sent</param>
        void Write(string message);

        /// <summary>
        /// Sends data over the channel
        /// </summary>
        /// <param name="message">The message to be sent</param>
        void Write(ArraySegment<byte> message);

        /// <summary>
        /// Closes this channel.
        /// </summary>
        void Close();
    }
}
