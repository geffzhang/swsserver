using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocket4Net;

namespace WebSocketService.Client
{
    public class WebSocketClient : IDisposable
    {
        private IConnectionProcessor processor;
        private WebSocket4Net.WebSocket socket;
        private string locker = "";
        private int retryMs = 500;
        private Action retry;
        private Timer timer;

        private bool IsDisposed { get { return this.retry == null; } }

        public WebSocketClient(
            string uri, 
            IConnectionProcessor processor, 
            WebSocketCredential credential = null)
        {
           
            var cookies = new List<KeyValuePair<string, string>>();

            if (credential != null) 
            {
                var token = credential.ToString();
                if (token == null) 
                    throw new NullReferenceException("The credential must be a non-null string.");
                cookies.Add(new KeyValuePair<string, string>(credential.Type, token));
            }

            this.socket = new WebSocket4Net.WebSocket(uri: uri, cookies: cookies);
            this.socket.Open();

            this.processor = processor;
            this.processor.Client = this;
            this.retry = this.BeginRetry;

            this.socket.Opened += (o, e) =>
            {
                this.EndRetry();
                this.processor.Opened();
            };

            this.socket.Closed += (o, e) =>
            {
                this.processor.Closed();
                Lock(() => this.retry());
            };

            this.socket.MessageReceived += (o, e) => this.processor.MessageReceived(e.Message);

            this.socket.DataReceived  += (o, e) => this.processor.MessageReceived(e.Data);

            this.socket.Error += (o, e) =>
            {
                this.processor.Error(e.Exception);
                Lock(() => this.retry());
            };

           
        }

        public WebSocketState State
        {
            get
            {
                return this.socket.State;
            }
        }

        public void Send(string message)
        {
            Lock(() => socket.Send(message));
        }

        public void Send(byte[] message)
        {
            Lock(() => socket.Send(message, 0, message.Length));
        }

        public void Dispose()
        {
            Lock(() => {
                this.retry = null;

                if (socket != null)
                {
                    socket.Close();
                    socket = null;
                }

                if (timer != null)
                {
                    timer.Dispose();
                    timer = null;
                }
            }, false);
        }

        private void Open()
        {
            
            Lock(
                () =>
                {
                    if (socket.State != WebSocket4Net.WebSocketState.Connecting)
                    {                       
                        socket.Open();
                    }
                });
        }

        private void BeginRetry()
        {
            Console.WriteLine("Retrying in " + retryMs + "ms");
            Lock(() =>
            {
                if (this.timer != null) return;

                this.timer = new Timer(
                o =>
                {
                    Lock(() =>
                    {
                        this.timer.Dispose();
                        this.timer = null;
                        this.retryMs = Math.Min(15000, this.retryMs + 500);
                        this.Open();
                    });
                },
                this,
                retryMs,
                Timeout.Infinite);
            });
        }

        private void EndRetry()
        {
            this.retryMs = 500;
        }

        private void Lock(Action fn, bool isNotDisposed = true)
        {
            lock (locker)
            {
                if (isNotDisposed == !this.IsDisposed)
                {
                    fn();
                }
            }
        }
    }
}
